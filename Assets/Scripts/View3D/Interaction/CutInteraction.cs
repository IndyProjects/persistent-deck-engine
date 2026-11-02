using UnityEngine;
using UnityEngine.InputSystem;

/// Interactive cut. The camera moves to a side-on close-up of the deck (in hand or on the table),
/// the deck spreads apart so every card is a separate layer, and the player picks the section to
/// move to the top with the mouse:
///   • hover a card: it and everything below it are selected (an ordinary cut)
///   • drag across cards: exactly that block is selected (a pulled cut from the middle)
///   • release the mouse to cut; Esc or right-click cancels.
/// The selected section slides out and is tinted, so the player always sees how many cards will move.
public class CutInteraction : TableInteraction
{
    [Tooltip("Gap between cards while the deck is spread for picking (metres).")]
    [SerializeField] float _layerSpacing = 0.004f;
    [Tooltip("How far the selected section slides out of the deck (metres).")]
    [SerializeField] float _selectionSlide = 0.04f;
    [Tooltip("Camera distance to the side of the deck (metres).")]
    [SerializeField] float _viewDistance = 0.34f;

    public int SelStart { get; private set; } = -1;
    public int SelCount { get; private set; }

    private ZoneId  _source;
    private Vector3 _anchor;
    private int     _dragFrom = -1;

    protected override string Prompt =>
        $"Cut: {Mathf.Max(0, SelCount)} card{(SelCount == 1 ? "" : "s")} to the top\n" +
        $"Click a card (it and those below go on top) · drag to pick a block · {Controls.Name(ControlAction.Cancel)} to cancel";

    protected override bool OnBegin()
    {
        var source = HeldOrTable(Sm.Table);
        int n = Sm.Table.Count(source);
        if (n < 2 || !TableLayout.TryInspectFrame(source, out _anchor, out var frame))
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
        View.BeginInspect(source, _layerSpacing, _selectionSlide);
        FocusOnSpread(_anchor, frame, n, _layerSpacing, _viewDistance);
        return true;
    }

    /// Highlights a section directly (previews and tests).
    public void SetSelection(int start, int count)
    {
        SelStart = start;
        SelCount = count;
        View.Select(start, count);
    }

    protected override void OnCancel() => ClearMode();

    private void ClearMode()
    {
        _dragFrom = -1;
        SelStart = -1;
        SelCount = 0;
        View.EndInspect();
    }

    protected override void Tick(Mouse mouse, Keyboard kb)
    {
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
            var op = new CutSectionOperation(SelStart, SelCount, _source);
            View.ClearSelection(); // indices change once the section moves
            // Applied while still spread, so the section is seen moving to the top; then square up
            Commit(op);
            ClearMode();
        }
    }
}
