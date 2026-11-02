using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Interactive riffle, done with both hands:
///   1. Split — the deck spreads in a side view; hover to choose how many cards go to the left hand
///      (highlighted), click to split.
///   2. Drop — left mouse button (or the Riffle-left key) drops from the left hand, right mouse button
///      (or the Riffle-right key) from the right. Tap for one card, hold to let cards stream. Cards fall from the bottom of each packet
///      onto the table, exactly as in a real riffle.
///   3. Push — once both hands are empty the two woven halves slide together into one squared deck.
/// The deck then lifts into the right hand and the whole riffle is recorded as one RiffleOperation
/// (split + drop pattern), so undo, history and the deck engine all agree.
public class RiffleInteraction : TableInteraction
{
    [SerializeField] float _layerSpacing  = 0.004f;
    [SerializeField] float _viewDistance  = 0.34f;
    [Tooltip("Seconds a button must be held before cards start streaming.")]
    [SerializeField] float _streamDelay   = 0.18f;
    [Tooltip("Cards per second while a button is held.")]
    [SerializeField] float _streamRate    = 16f;

    private enum Stage { Split, Drop, Push }

    private Stage   _stage;
    private int     _n, _split;
    private Vector3 _anchor;
    private float   _nextLeft, _nextRight;
    private bool    _waitForRelease;
    private readonly List<bool> _drops = new List<bool>();
    private RifflePresentation _presentation;
    private float _pushEnds;

    private static readonly ZoneId Hand = ZoneId.RightHand;

    // In the drop stage the right button is the right hand, so only the Cancel key cancels
    protected override bool RightClickCancels => _stage == Stage.Split;

    protected override string Prompt => _stage == Stage.Push ? "Riffle — pushing the halves together…"
        : _stage == Stage.Split
        ? $"Riffle — split: {_split} cards to your left hand, {_n - _split} stay in your right\n" +
          $"Move to choose where to split · click to split · {Controls.Name(ControlAction.Cancel)} to cancel"
        : $"Riffle — left hand {View.Shown.Count(ZoneId.LeftHand)} · right hand {View.Shown.Count(ZoneId.RightHand)}\n" +
          $"Left mouse / {Controls.Name(ControlAction.RiffleLeft)}: drop from the left · " +
          $"Right mouse / {Controls.Name(ControlAction.RiffleRight)}: drop from the right · hold to stream · " +
          $"{Controls.Name(ControlAction.Cancel)} to cancel";

    protected override bool OnBegin()
    {
        int n = Sm.Table.Count(Hand);
        if (n < 2)
        {
            Message = "Pick up the deck first (click it or press E).";
            return false;
        }
        var allowed = Sm.CanApply(new RiffleOperation(n / 2, new[] { 1 }, Hand));
        if (!allowed.Success)
        {
            Message = allowed.Reason;
            return false;
        }

        _n = n;
        _split = n / 2;
        _stage = Stage.Split;
        _drops.Clear();
        TableLayout.TryInspectFrame(Hand, out _anchor, out var frame);
        View.BeginInspect(Hand, _layerSpacing, 0.03f);
        View.Select(0, _split);
        FocusOnSpread(_anchor, frame, n, _layerSpacing, _viewDistance);
        return true;
    }

    protected override void OnCancel()
    {
        if (_stage == Stage.Split) View.EndInspect();
        else View.EndPreview(false); // also clears the woven presentation
    }

    protected override void Tick(Mouse mouse, Keyboard kb)
    {
        if (_stage == Stage.Split) TickSplit(mouse);
        else if (_stage == Stage.Drop) TickDrop(mouse, kb);
        else if (Time.time >= _pushEnds) FinishPush();
    }

    private void TickSplit(Mouse mouse)
    {
        if (mouse == null) return;
        int hover = TableLayout.LayerAt(Cam.ScreenPointToRay(mouse.position.ReadValue()), _anchor, _n, _layerSpacing);
        _split = Mathf.Clamp(hover + 1, 1, _n - 1); // the hovered card is the bottom of the left packet
        View.Select(0, _split);

        if (mouse.leftButton.wasPressedThisFrame && !PointerOverUI())
            StartDropping();
    }

    /// Splits and drops alternately without input (previews and tests).
    public void PreviewDrops(int split, int drops)
    {
        if (_stage != Stage.Split) return;
        _split = Mathf.Clamp(split, 1, _n - 1);
        StartDropping();
        for (int i = 0; i < drops && _stage == Stage.Drop; i++) Drop(i % 2 == 0);
    }

    private void StartDropping()
    {
        View.EndInspect();
        View.BeginPreview();
        _presentation = new RifflePresentation();
        View.Poser = _presentation;
        View.ArcScale = 0.2f;    // cards fall, they don't get thrown
        View.TravelScale = 0.6f;
        View.PreviewMove(CardMove.TopToTop(Hand, _split, ZoneId.LeftHand));
        FocusOnRiffle();
        _stage = Stage.Drop;
        _waitForRelease = true; // the click that chose the split mustn't also drop a card
    }

    private void TickDrop(Mouse mouse, Keyboard kb)
    {
        bool leftHeld  = (mouse != null && mouse.leftButton.isPressed)  || Controls.Held(ControlAction.RiffleLeft);
        bool rightHeld = (mouse != null && mouse.rightButton.isPressed) || Controls.Held(ControlAction.RiffleRight);
        if (_waitForRelease)
        {
            if (leftHeld) return;
            _waitForRelease = false;
        }

        bool leftPressed  = (mouse != null && mouse.leftButton.wasPressedThisFrame && !PointerOverUI()) || Controls.Pressed(ControlAction.RiffleLeft);
        bool rightPressed = (mouse != null && mouse.rightButton.wasPressedThisFrame) || Controls.Pressed(ControlAction.RiffleRight);
        float now = Time.time;

        Stream(true,  leftPressed,  leftHeld,  ref _nextLeft,  now);
        Stream(false, rightPressed, rightHeld, ref _nextRight, now);
    }

    /// Tap = one card; holding streams cards after a short delay.
    private void Stream(bool left, bool pressed, bool held, ref float next, float now)
    {
        if (!IsActive || _stage != Stage.Drop) return;
        if (pressed)
        {
            Drop(left);
            next = now + _streamDelay;
        }
        else if (held && now >= next)
        {
            Drop(left);
            next = Mathf.Max(next + 1f / _streamRate, now - 1f / _streamRate);
        }
    }

    private void Drop(bool left)
    {
        var hand = left ? ZoneId.LeftHand : ZoneId.RightHand;
        if (View.Shown.Count(hand) == 0) return;

        View.PreviewMove(CardMove.BottomToTop(hand, 1, ZoneId.Deck));
        _drops.Add(left);

        if (View.Shown.Count(ZoneId.LeftHand) == 0 && View.Shown.Count(ZoneId.RightHand) == 0)
            StartPush();
    }

    private void StartPush()
    {
        _stage = Stage.Push;
        _presentation.Pushed = true;
        View.TravelScale = RifflePresentation.PushSeconds / 0.156f; // an unhurried slide, not a snap
        View.Settle(ZoneId.Deck);
        _pushEnds = Time.time + RifflePresentation.PushSeconds;
    }

    /// Lifts the pushed deck into the hand and records the riffle.
    public void FinishPush()
    {
        // Lift the squared deck back into the right hand, as the operation itself does
        View.ClearPresentation();
        View.PreviewMove(CardMove.TopToTop(ZoneId.Deck, _n, Hand));
        View.EndPreview(committed: true);
        var result = Commit(new RiffleOperation(_split, RiffleOperation.PatternFromDrops(_drops), Hand));
        if (!result.Success) View.EndPreview(false);
    }
}
