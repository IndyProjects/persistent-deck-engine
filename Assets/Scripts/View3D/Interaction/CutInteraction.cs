using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// Interactive cut. The camera moves to a side-on close-up of the deck (in hand or on the table),
/// the deck spreads apart so every card is a separate layer, and the player picks the section to
/// move to the top with the mouse:
///   • hover a card: it and everything below it are selected (an ordinary cut)
///   • drag across cards: exactly that block is selected (a pulled cut from the middle)
///   • release the mouse to cut; Esc or right-click cancels.
/// The selected section slides out and is tinted, so the player always sees how many cards will move.
/// The cut then plays out with the camera held on it: the section slides clear of the deck, the rest
/// drops down, and the section lifts, passes over the top and is placed on it. The game records it once
/// the motion has finished.
public class CutInteraction : TableInteraction
{
    [Tooltip("Gap between cards while the deck is spread for picking (metres).")]
    [SerializeField] float _layerSpacing = 0.004f;
    [Tooltip("How far the selected section slides out of the deck (metres).")]
    [SerializeField] float _selectionSlide = 0.04f;
    [Tooltip("Camera distance to the side of the deck (metres).")]
    [SerializeField] float _viewDistance = 0.34f;
    [Tooltip("Gap between cards while the cut plays: looser than a squared deck, so the packets read clearly (metres).")]
    [SerializeField] float _packetSpacing = 0.0012f;
    [Tooltip("Scales the speed of the cut (1 = about 1.5 seconds).")]
    [SerializeField] float _motionSpeed = 1f;

    public int SelStart { get; private set; } = -1;
    public int SelCount { get; private set; }

    /// True while the chosen cut is playing out.
    public bool IsMoving { get; private set; }

    private ZoneId     _source;
    private Vector3    _anchor;
    private Quaternion _frame;
    private int        _dragFrom = -1;
    private float      _motionEnds;
    private int        _cutStart, _cutCount;

    protected override string Prompt => IsMoving
        ? $"Placing {_cutCount} card{(_cutCount == 1 ? "" : "s")} on top…"
        : $"Cut: {Mathf.Max(0, SelCount)} card{(SelCount == 1 ? "" : "s")} to the top\n" +
          $"Click a card (it and those below go on top) · drag to pick a block · {Controls.Name(ControlAction.Cancel)} to cancel";

    protected override bool RightClickCancels => !IsMoving;

    protected override bool OnBegin()
    {
        var source = HeldOrTable(Sm.Table);
        int n = Sm.Table.Count(source);
        if (n < 2 || !TableLayout.TryInspectFrame(source, out _anchor, out _frame))
        {
            Message = "There's no deck to cut.";
            return false;
        }
        var allowed = Sm.CanApply(new CutSectionOperation(1, n - 1, source));
        if (!allowed.Success)
        {
            Message = allowed.Reason;
            return false;
        }

        _source = source;
        _dragFrom = -1;
        IsMoving = false;
        View.BeginInspect(source, _layerSpacing, _selectionSlide);
        FocusOnSpread(_anchor, _frame, n, _layerSpacing, _viewDistance);
        return true;
    }

    /// Highlights a section directly (previews and tests).
    public void SetSelection(int start, int count)
    {
        SelStart = start;
        SelCount = count;
        View.Select(start, count);
    }

    protected override void OnCancel()
    {
        if (IsMoving)
        {
            IsMoving = false;
            View.ClearTints(TableCardView.SelectedTint);
            View.EndPreview(false);
        }
        else View.EndInspect();
        _dragFrom = -1;
        SelStart = -1;
        SelCount = 0;
    }

    protected override void Tick(Mouse mouse, Keyboard kb)
    {
        if (IsMoving)
        {
            if (Time.time >= _motionEnds) FinishCut();
            return;
        }
        if (mouse == null) return;

        int n = Sm.Table.Count(_source);
        int hover = TableLayout.LayerAt(Cam.ScreenPointToRay(mouse.position.ReadValue()), _anchor, n, _layerSpacing);

        if (mouse.leftButton.wasPressedThisFrame && !PointerOverUI())
            _dragFrom = hover;

        // Dragging selects exactly the cards crossed; otherwise the hovered card down to the bottom
        if (_dragFrom >= 0 && hover != _dragFrom)
            SetSelection(Mathf.Min(_dragFrom, hover), Mathf.Abs(hover - _dragFrom) + 1);
        else
            SetSelection(hover, n - hover);

        if (_dragFrom >= 0 && mouse.leftButton.wasReleasedThisFrame)
        {
            _dragFrom = -1;
            if (SelStart <= 0)
            {
                Message = "That section is already on top — pick lower down.";
                return;
            }
            StartCut(SelStart, SelCount);
        }
    }

    /// Plays the chosen cut. Nothing reaches the game until the motion finishes (FinishCut).
    public void StartCut(int start, int count)
    {
        _cutStart = start;
        _cutCount = count;
        Message = null;
        IsMoving = true;

        var table = View.Shown;
        var cards = new List<int>(table.Cards(_source));
        int n = cards.Count, rest = n - count;

        View.EndInspectQuietly();
        View.BeginPreview();
        View.ApplySilently(new CardMove(_source, start, count, _source, 0));
        for (int i = start; i < start + count; i++) View.SetTint(cards[i], TableCardView.SelectedTint);

        // Durations of each step, in order
        float speed = Mathf.Max(0.1f, _motionSpeed);
        float close = 0.28f / speed, slideOut = 0.28f / speed, drop = 0.2f / speed;
        float lift = 0.24f / speed, across = 0.28f / speed, lower = 0.2f / speed;

        float h = _packetSpacing;
        float clear = CardMeshBuilder.Length * 1.15f;
        float above = h * rest + 0.02f;

        float t = Time.time;
        float slideAt = t + close, dropAt = slideAt + slideOut, liftAt = dropAt + drop;
        float acrossAt = liftAt + lift, lowerAt = acrossAt + across;
        _motionEnds = lowerAt + lower;

        for (int i = 0; i < n; i++)
        {
            int card = cards[i];
            bool faceUp = table.IsFaceUp(card);
            float layer = h * (n - 1 - i);

            if (i >= start && i < start + count)
            {
                float inSection = h * (count - 1 - (i - start)); // height within the section
                View.ScheduleCard(card, t,        Pose(layer, _selectionSlide, faceUp), close);
                View.ScheduleCard(card, slideAt,  Pose(layer, clear, faceUp), slideOut);
                View.ScheduleCard(card, liftAt,   Pose(above + inSection, clear, faceUp), lift);
                View.ScheduleCard(card, acrossAt, Pose(above + inSection, 0f, faceUp), across);
                View.ScheduleCard(card, lowerAt,  Pose(h * rest + inSection, 0f, faceUp), lower);
            }
            else
            {
                int inRest = i < start ? i : i - count; // position among the cards staying behind
                View.ScheduleCard(card, t,      Pose(layer, 0f, faceUp), close);
                View.ScheduleCard(card, dropAt, Pose(h * (rest - 1 - inRest), 0f, faceUp), drop);
            }
        }

        // Hold the camera side-on, level with the deck and framing the path the section takes
        var mid = _anchor + _frame * new Vector3(0f, h * n * 0.5f + 0.015f, clear * 0.5f);
        Rig.Focus(mid + _frame * Vector3.right * (_viewDistance * 0.95f) + Vector3.up * 0.1f, mid);
    }

    /// Records the cut in the game and squares the deck up.
    public void FinishCut()
    {
        if (!IsMoving) return;
        IsMoving = false;
        View.ClearTints(TableCardView.SelectedTint);
        View.EndPreview(committed: true);
        var result = Commit(new CutSectionOperation(_cutStart, _cutCount, _source));
        if (!result.Success) View.EndPreview(false);
        else View.Settle(_source);
        SelStart = -1;
        SelCount = 0;
    }

    private CardPose Pose(float height, float slide, bool faceUp)
        => TableLayout.PacketPose(_anchor, _frame, height, slide, faceUp);
}
