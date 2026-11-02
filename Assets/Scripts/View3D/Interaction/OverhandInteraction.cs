using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Interactive overhand shuffle. The deck sits in the right hand; the player holds the left mouse button
/// (or the Overhand-slide key) to slide cards off the top — the growing chunk is highlighted — and
/// releases to drop that chunk onto the left hand. The Drop-the-rest key drops everything that's left. When the right hand is empty the
/// shuffled packet returns to the right hand and the whole shuffle is recorded as one OverhandOperation.
public class OverhandInteraction : TableInteraction
{
    [Tooltip("Seconds the button must be held before more cards slide off.")]
    [SerializeField] float _streamDelay = 0.2f;
    [Tooltip("Cards per second added to the chunk while held.")]
    [SerializeField] float _streamRate  = 10f;

    private int   _n, _pending;
    private float _next;
    private bool  _waitForRelease;
    private readonly List<int> _chunks = new List<int>();

    private static readonly ZoneId Hand = ZoneId.RightHand;

    private int Remaining => View.Shown.Count(Hand);

    protected override string Prompt =>
        $"Overhand — right hand {Remaining} · left hand {View.Shown.Count(ZoneId.LeftHand)}" +
        (_pending > 0 ? $" · sliding {_pending}" : "") + "\n" +
        $"Hold left mouse / {Controls.Name(ControlAction.OverhandSlide)} to slide cards off the top · release to drop them · " +
        $"{Controls.Name(ControlAction.OverhandDropRest)} drops the rest · {Controls.Name(ControlAction.Cancel)} to cancel";

    protected override bool OnBegin()
    {
        int n = Sm.Table.Count(Hand);
        if (n < 1)
        {
            Message = "Pick up the deck first (click it or press E).";
            return false;
        }
        var allowed = Sm.CanApply(new OverhandOperation(new[] { n }, Hand));
        if (!allowed.Success)
        {
            Message = allowed.Reason;
            return false;
        }

        _n = n;
        _pending = 0;
        _chunks.Clear();
        View.BeginPreview();
        FocusOnHands();
        var mouse = Mouse.current;
        _waitForRelease = mouse != null && mouse.leftButton.isPressed; // started from a click
        return true;
    }

    protected override void OnCancel()
    {
        View.ClearTints(TableCardView.SelectedTint);
        View.EndPreview(false);
    }

    protected override void Tick(Mouse mouse, Keyboard kb)
    {
        bool held = (mouse != null && mouse.leftButton.isPressed) || Controls.Held(ControlAction.OverhandSlide);
        if (_waitForRelease)
        {
            if (held) return;
            _waitForRelease = false;
        }

        if (Controls.Pressed(ControlAction.OverhandDropRest))
        {
            SetPending(Remaining);
            DropChunk();
            return;
        }

        bool pressed = (mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerOverUI()) ||
                       Controls.Pressed(ControlAction.OverhandSlide);
        float now = Time.time;

        if (pressed)
        {
            SetPending(1);
            _next = now + _streamDelay;
        }
        else if (held && _pending > 0 && now >= _next)
        {
            SetPending(_pending + 1);
            _next = Mathf.Max(_next + 1f / _streamRate, now - 1f / _streamRate);
        }
        else if (!held && _pending > 0)
        {
            DropChunk();
        }
    }

    /// Highlights the top <paramref name="count"/> cards of the right hand: the chunk about to drop.
    private void SetPending(int count)
    {
        count = Mathf.Clamp(count, 0, Remaining);
        if (count == _pending) return;
        _pending = count;
        View.ClearTints(TableCardView.SelectedTint);
        var cards = View.Shown.Cards(Hand);
        for (int i = 0; i < _pending; i++) View.SetTint(cards[i], TableCardView.SelectedTint);
    }

    private void DropChunk()
    {
        if (_pending <= 0) return;
        View.ClearTints(TableCardView.SelectedTint);
        View.PreviewMove(CardMove.TopToTop(Hand, _pending, ZoneId.LeftHand));
        _chunks.Add(_pending);
        _pending = 0;

        if (Remaining == 0)
        {
            // The shuffled packet goes back to the right hand, as the operation itself does
            View.PreviewMove(CardMove.TopToTop(ZoneId.LeftHand, _n, Hand));
            View.EndPreview(committed: true);
            var result = Commit(new OverhandOperation(_chunks.ToArray(), Hand));
            if (!result.Success) View.EndPreview(false);
        }
    }
}
