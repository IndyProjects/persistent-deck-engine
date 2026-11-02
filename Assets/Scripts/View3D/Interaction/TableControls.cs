using UnityEngine;
using UnityEngine.InputSystem;

/// The player's hands-on controls for a round, with context hints at the bottom of the screen.
/// Keys come from Controls (rebindable; defaults under the left hand around WASD):
///
///   Deck on the table   click it (or E) to pick it up
///   Holding the deck    R riffle · F overhand · C cut · E put down
///   Dealing             Space deals a packet to the next player · scroll or 1–5 sets the packet size
///   After the deal      drag a card from your hand onto the table (Left Shift = face down); drag the
///                       top card of a pile back to your hand; X collects everything
///   Any time            Z undo · Tab deck history · Esc controls
///
/// Shuffles and cuts hand over to their interactions; everything goes through GameStateManager, so the
/// rules decide what's allowed and the HUD buttons stay in sync.
public class TableControls : MonoBehaviour, IStateManagerConsumer
{
    [Tooltip("Height above the felt a dragged card floats (metres).")]
    [SerializeField] float _dragHeight = 0.06f;

    public GameStateManager StateManager { private get; set; }

    private GameStateManager   _sm;
    private TableCardView      _view;
    private Camera             _camera;
    private HistoryPanel       _history;
    private ControlsPanel      _controls;
    private CutInteraction      _cut;
    private RiffleInteraction   _riffle;
    private OverhandInteraction _overhand;
    private TableInteraction[]  _interactions;
    private ICardGameController _game;

    private int        _dragCard = -1;
    private ZoneId     _dragFrom;
    private ZoneId?    _dropZone;
    private GameObject _marker;

    private string _message;
    private float  _messageUntil;

    private void Start()
    {
        _sm       = StateManager;
        _view     = Find<TableCardView>();
        _history  = Find<HistoryPanel>();
        _cut      = Find<CutInteraction>();
        _riffle   = Find<RiffleInteraction>();
        _overhand = Find<OverhandInteraction>();
        _controls = Find<ControlsPanel>();
        if (_controls == null) _controls = gameObject.AddComponent<ControlsPanel>();
        _interactions = new TableInteraction[] { _cut, _riffle, _overhand };
        foreach (var behaviour in GetComponents<MonoBehaviour>())
            if (behaviour is ICardGameController game) { _game = game; break; }
    }

    // Explicit null check: Unity's fake-null "missing component" objects defeat ??
    private T Find<T>() where T : Object
    {
        var local = GetComponent<T>();
        return local != null ? local : FindAnyObjectByType<T>();
    }

    private bool InteractionActive
    {
        get
        {
            foreach (var i in _interactions) if (i != null && i.IsActive) return true;
            return false;
        }
    }

    private DealerRules Dealer => (_sm?.Rules as IDealingRuleSet)?.Dealing;

    private void Update()
    {
        if (_sm == null || _view == null || InteractionActive || ControlsPanel.AnyOpen) return;
        if (_camera == null) _camera = Camera.main;
        var kb = Keyboard.current;
        var mouse = Mouse.current;

        if (Controls.Pressed(ControlAction.History) && _history != null) _history.Toggle();
        if (_history != null && _history.IsOpen)
        {
            CancelDrag();
            return; // the history panel owns clicks while open
        }

        if (_dragCard >= 0)
        {
            UpdateDrag(mouse, kb);
            return;
        }

        if (Controls.Pressed(ControlAction.Cancel))
        {
            _controls.Open();
            return;
        }

        // Once a game's play starts it owns the cards; the table keeps undo and collecting
        if (_game != null && _game.OwnsInput)
        {
            if (Controls.Pressed(ControlAction.Undo)) Report(_sm.Undo());
            else if (Controls.Pressed(ControlAction.Collect)) Apply(CollectOperation.All());
            return;
        }
        if (kb != null) HandleKeys(kb);
        if (mouse != null) HandleMouse(mouse);
    }

    // ---- Keyboard ----

    private void HandleKeys(Keyboard kb)
    {
        if (Controls.Pressed(ControlAction.Undo)) Report(_sm.Undo());
        else if (Controls.Pressed(ControlAction.PickUpPutDown))
        {
            var pickUp = new PickUpDeckOperation();
            Apply(_sm.CanApply(pickUp).Success ? (GameOperation)pickUp : new PutDownDeckOperation());
        }
        else if (Controls.Pressed(ControlAction.Cut))      Begin(_cut);
        else if (Controls.Pressed(ControlAction.Riffle))   Begin(_riffle);
        else if (Controls.Pressed(ControlAction.Overhand)) Begin(_overhand);
        else if (Controls.Pressed(ControlAction.Deal))     Deal();
        else if (Controls.Pressed(ControlAction.Collect))  Apply(CollectOperation.All());

        // Packet size: number keys (fixed, left side of the keyboard) or the scroll wheel
        if (Dealer != null)
        {
            for (int k = 1; k <= 9; k++)
                if (kb[(Key)((int)Key.Digit1 + k - 1)].wasPressedThisFrame) Dealer.PacketSize = k;
            float scroll = Mouse.current != null ? Mouse.current.scroll.ReadValue().y : 0f;
            if (scroll > 0.1f) Dealer.PacketSize++;
            else if (scroll < -0.1f) Dealer.PacketSize--;
        }
    }

    private void Begin(TableInteraction interaction)
    {
        if (interaction == null) return;
        if (!interaction.TryBegin()) Show(interaction.LastMessage ?? "Not possible right now.");
    }

    private void Deal()
    {
        if (Dealer == null)
        {
            Show("Dealing packets needs the dealer rules.");
            return;
        }
        Apply(Dealer.NextPacket(_sm.Table));
    }

    // ---- Mouse: pick up the deck, drag cards ----

    private void HandleMouse(Mouse mouse)
    {
        if (!mouse.leftButton.wasPressedThisFrame || _camera == null) return;
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

        int card = TableCardView.CardAt(_camera.ScreenPointToRay(mouse.position.ReadValue()));
        if (card < 0) return;

        var zone = _sm.Table.LocationOf(card);
        if (zone == ZoneId.Deck)
        {
            var pickUp = new PickUpDeckOperation();
            if (_sm.CanApply(pickUp).Success) { Apply(pickUp); return; }
        }

        bool draggable = zone == ZoneId.Hand(0) ||
                         ((zone.Kind == ZoneKind.Pile || zone.Kind == ZoneKind.Discard) && _sm.Table.IndexOf(card) == 0);
        if (!draggable) return;

        var probe = _sm.CanApply(new MoveCardOperation(card, zone == ZoneId.Hand(0) ? ZoneId.Pile(0) : ZoneId.Hand(0)));
        if (!probe.Success)
        {
            Show(probe.Reason);
            return;
        }
        _dragCard = card;
        _dragFrom = zone;
        _view.BeginDrag(card);
    }

    private void UpdateDrag(Mouse mouse, Keyboard kb)
    {
        if (mouse == null || _camera == null)
        {
            CancelDrag();
            return;
        }
        bool faceDown = Controls.Held(ControlAction.FaceDown);

        // Follow the mouse on a plane just above the felt
        var ray = _camera.ScreenPointToRay(mouse.position.ReadValue());
        var plane = new Plane(Vector3.up, new Vector3(0f, TableLayout.SurfaceY + _dragHeight, 0f));
        if (plane.Raycast(ray, out float distance))
        {
            var point = ray.GetPoint(distance);
            var rotation = faceDown ? Quaternion.AngleAxis(180f, Vector3.forward) : Quaternion.identity;
            _view.DragTo(point, rotation);

            _dropZone = TableLayout.TryDropZone(point, out var zone, out var anchor) && zone != _dragFrom
                ? zone : (ZoneId?)null;
            ShowMarker(_dropZone.HasValue, anchor);
        }

        if (!mouse.leftButton.wasReleasedThisFrame) return;

        ShowMarker(false, default);
        int card = _dragCard;
        _dragCard = -1;
        if (_dropZone.HasValue)
        {
            var to = _dropZone.Value;
            bool wantFaceUp = to != ZoneId.Hand(0) && !faceDown;   // your own hand is kept face down
            bool flip = wantFaceUp != _sm.Table.IsFaceUp(card);
            int toIndex = to == ZoneId.Hand(0) ? CardMove.Bottom : 0; // fan end / top of the pile
            Apply(new MoveCardOperation(card, to, toIndex, flip));
        }
        _view.EndDrag();
        _dropZone = null;
    }

    private void CancelDrag()
    {
        if (_dragCard < 0) return;
        _dragCard = -1;
        _dropZone = null;
        ShowMarker(false, default);
        _view.EndDrag();
    }

    /// A card-shaped highlight on the felt where a dragged card will land.
    private void ShowMarker(bool show, Vector3 anchor)
    {
        if (!show)
        {
            if (_marker != null) _marker.SetActive(false);
            return;
        }
        if (_marker == null)
        {
            _marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _marker.name = "Drop Marker";
            Destroy(_marker.GetComponent<Collider>());
            _marker.transform.localScale = new Vector3(CardMeshBuilder.Width * 1.12f, 0.0008f, CardMeshBuilder.Length * 1.12f);
            _marker.GetComponent<MeshRenderer>().sharedMaterial =
                Materials3D.Lit("Drop Marker", TableCardView.SelectedTint * 0.9f, smoothness: 0.6f);
        }
        _marker.SetActive(true);
        _marker.transform.position = anchor + Vector3.up * 0.0006f;
    }

    // ---- Feedback ----

    private void Apply(GameOperation op) => Report(_sm.Apply(op));

    private void Report(OperationResult result)
    {
        if (!result.Success) Show(result.Reason);
    }

    private void Show(string message)
    {
        _message = message;
        _messageUntil = Time.time + 3f;
    }

    private void OnGUI()
    {
        if (_sm == null || InteractionActive || ControlsPanel.AnyOpen || (_history != null && _history.IsOpen)) return;
        if (_game != null && _game.OwnsInput) return; // the game shows its own prompt
        string hint = Hint();
        if (_message != null && Time.time < _messageUntil) hint += $"\n<color=#ffb070>{_message}</color>";
        PromptBox.Draw(hint);
    }

    private string Hint()
    {
        string K(ControlAction a) => Controls.Name(a);
        string always = $"{K(ControlAction.Undo)} undo · {K(ControlAction.History)} history · {K(ControlAction.Cancel)} controls";
        if (_dragCard >= 0)
            return $"Drop on a table spot, the discard, or back in your hand · hold {K(ControlAction.FaceDown)} to place face down";

        var dealer = Dealer;
        string shuffles = $"{K(ControlAction.Riffle)} riffle · {K(ControlAction.Overhand)} overhand · {K(ControlAction.Cut)} cut";
        if (dealer == null)
            return $"Drag cards to play · {shuffles} · {always}";

        switch (dealer.PhaseOf(_sm.Table))
        {
            case RoundPhase.Gathered:
                return $"Click the deck (or press {K(ControlAction.PickUpPutDown)}) to pick it up · {always}";
            case RoundPhase.Shuffling:
                return $"{shuffles} · {K(ControlAction.Deal)} deals {dealer.PacketSize} (number keys / scroll to change) · " +
                       $"{K(ControlAction.PickUpPutDown)} put down\n{always}";
            case RoundPhase.Dealing:
                return $"{K(ControlAction.Deal)}: deal {Mathf.Min(dealer.PacketSize, dealer.Remaining(_sm.Table, dealer.NextSeat))} " +
                       $"to seat {dealer.NextSeat} (packet size {dealer.PacketSize}: number keys / scroll) · {always}";
            default:
                return $"Drag cards from your hand to the table ({K(ControlAction.FaceDown)} = face down) · drag a pile's top card to your hand\n" +
                       $"{K(ControlAction.PickUpPutDown)} put the deck down · {K(ControlAction.Collect)} collect · {always}";
        }
    }
}
