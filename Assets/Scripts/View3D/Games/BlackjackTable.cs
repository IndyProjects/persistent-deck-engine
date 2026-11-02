using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// A game that takes over the player's input once play starts (the dealing round stays with TableControls).
public interface ICardGameController
{
    bool OwnsInput { get; }
}

/// Runs a game of Blackjack at the table once the deal is done: turns up the first card, plays the
/// opponents' turns (BlackjackAI) at a readable pace, catches the player if they forget LAST CARD, and
/// handles the player's own turn:
///   click cards in your hand to select them (any order) and press Play (Space), or drag a single card
///   onto the discard pile · Pick up (Q) or click the draw pile · Call LAST CARD (G) · Catch (V).
/// Aces open a suit chooser (click, or 1–4). A scoreboard shows card counts, calls and wins.
public class BlackjackTable : MonoBehaviour, IStateManagerConsumer, ICardGameController
{
    [Tooltip("Pause before each opponent move, so every play can be followed (seconds).")]
    [SerializeField] float _opponentDelay = 1.0f;
    [Tooltip("How long you have to call LAST CARD after your turn before an opponent catches you.")]
    [SerializeField] float _catchGrace = 1.6f;
    [Tooltip("Chance an opponent forgets to call LAST CARD when going down to one card.")]
    [SerializeField, Range(0f, 1f)] float _opponentForgets = 0.15f;

    public GameStateManager StateManager { private get; set; }

    public bool OwnsInput
    {
        get
        {
            if (_rules == null || _sm == null) return false;
            var phase = _rules.PhaseOf(_sm.Table);
            return phase == BlackjackRules.Phase.Playing || phase == BlackjackRules.Phase.Finished;
        }
    }

    private GameStateManager _sm;
    private BlackjackRules   _rules;
    private TableCardView    _view;
    private HistoryPanel     _history;
    private TableInteraction[] _interactions;
    private Camera           _camera;
    private readonly System.Random _rng = new System.Random();

    private float _nextAction;
    private float _catchableSince = -1f;
    private readonly List<int> _selected = new List<int>();
    private int[] _awaitingSuit;

    private int     _pressCard = -1;
    private Vector2 _pressAt;
    private bool    _dragging;

    private string _message;
    private float  _messageUntil;

    private void Start()
    {
        _sm = StateManager;
        _rules = _sm?.Rules as BlackjackRules;
        if (_rules == null)
        {
            enabled = false; // another game is running
            return;
        }
        _view = GetComponent<TableCardView>();
        if (_view == null) _view = FindAnyObjectByType<TableCardView>();
        _history = FindAnyObjectByType<HistoryPanel>();
        _interactions = FindObjectsByType<TableInteraction>(FindObjectsSortMode.None);
        _sm.StateChanged += OnStateChanged;
        _nextAction = Time.time + _opponentDelay;
    }

    private void OnDestroy()
    {
        if (_sm != null) _sm.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged(GameStateSnapshot _)
    {
        _nextAction = Time.time + _opponentDelay;
        // Keep only selected cards still in your hand
        _selected.RemoveAll(c => _sm.Table.LocationOf(c) != ZoneId.Hand(0));
        RefreshTints();
        if (_rules.Turn != 0) _awaitingSuit = null;
    }

    private bool Busy
    {
        get
        {
            if (ControlsPanel.AnyOpen || (_history != null && _history.IsOpen)) return true;
            foreach (var i in _interactions) if (i != null && i.IsActive) return true;
            return _view != null && (_view.IsPreviewing || _view.IsReplaying);
        }
    }

    private void Update()
    {
        if (_rules == null || Busy) return;
        if (_camera == null) _camera = Camera.main;
        var table = _sm.Table;
        var phase = _rules.PhaseOf(table);

        if (phase == BlackjackRules.Phase.ReadyToStart && Time.time >= _nextAction)
        {
            _sm.Apply(new StartDiscardOperation());
            return;
        }
        if (phase != BlackjackRules.Phase.Playing) return;

        CatchThePlayerIfTheyForgot(table);

        if (_rules.Turn != 0)
        {
            CancelDrag();
            if (Time.time >= _nextAction) OpponentTurn(_rules.Turn);
            return;
        }
        PlayerTurn(table);
    }

    // ---- Opponents ----

    private void OpponentTurn(int seat)
    {
        var table = _sm.Table;
        var move = BlackjackAI.ChooseMove(_rules, table, seat, _rng, out bool wantsToCall);
        if (wantsToCall)
        {
            bool goingOut = move is PlayCardsOperation p && p.Cards.Length == table.Count(ZoneId.Hand(seat));
            if (goingOut || _rng.NextDouble() >= _opponentForgets)
                _sm.Apply(new AnnounceLastCardOperation(seat));
        }
        if (!_sm.Apply(move).Success)
            _sm.Apply(new DrawCardsOperation(seat, _rules.DrawCount)); // never stall the table
        _nextAction = Time.time + _opponentDelay;
    }

    private void CatchThePlayerIfTheyForgot(CardTable table)
    {
        if (!_rules.CanBeCaught(0, table))
        {
            _catchableSince = -1f;
            return;
        }
        if (_catchableSince < 0f) _catchableSince = Time.time;
        if (Time.time - _catchableSince < _catchGrace) return;

        int catcher = _rules.Turn != 0 ? _rules.Turn : 1;
        if (_sm.Apply(new CatchLastCardOperation(catcher, 0)).Success)
            Show($"Seat {catcher} caught you without LAST CARD — pick up {CatchLastCardOperation.Penalty}!");
        _catchableSince = -1f;
    }

    // ---- The player ----

    private void PlayerTurn(CardTable table)
    {
        if (_awaitingSuit != null)
        {
            ChooseSuitKeys();
            return;
        }

        if (Controls.Pressed(ControlAction.PlayCards))         PlaySelected();
        else if (Controls.Pressed(ControlAction.DrawCard))     Draw();
        else if (Controls.Pressed(ControlAction.CallLastCard)) Report(_sm.Apply(new AnnounceLastCardOperation(0)));
        else if (Controls.Pressed(ControlAction.CatchLastCard)) Catch();

        HandleMouse(table);
    }

    private void HandleMouse(CardTable table)
    {
        var mouse = Mouse.current;
        if (mouse == null || _camera == null) return;
        var position = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame && !PointerOverUI())
        {
            int card = TableCardView.CardAt(_camera.ScreenPointToRay(position));
            if (card >= 0 && table.LocationOf(card) == ZoneId.Hand(0))
            {
                _pressCard = card;
                _pressAt = position;
                _dragging = false;
            }
            else if (card >= 0 && table.LocationOf(card) == ZoneId.Deck)
                Draw();
            return;
        }

        if (_pressCard < 0) return;

        if (mouse.leftButton.isPressed)
        {
            if (!_dragging && (position - _pressAt).sqrMagnitude > 12f * 12f)
            {
                _dragging = true;
                _view.BeginDrag(_pressCard);
            }
            if (_dragging) DragTo(position);
            return;
        }

        // Released
        int pressed = _pressCard;
        _pressCard = -1;
        if (!_dragging)
        {
            ToggleSelected(pressed);
            return;
        }
        _dragging = false;
        bool onPile = TryFeltPoint(position, out var point) &&
                      TableLayout.TryDropZone(point, out var zone, out _) && zone == ZoneId.Discard();
        if (onPile) Play(new[] { pressed }); // applied while held, so the card flies from the mouse
        _view.EndDrag();
    }

    private void DragTo(Vector2 screen)
    {
        if (TryFeltPoint(screen, out var point)) _view.DragTo(point + Vector3.up * 0.06f, Quaternion.identity);
    }

    private bool TryFeltPoint(Vector2 screen, out Vector3 point)
    {
        var ray = _camera.ScreenPointToRay(screen);
        var plane = new Plane(Vector3.up, new Vector3(0f, TableLayout.SurfaceY, 0f));
        point = default;
        if (!plane.Raycast(ray, out float distance)) return false;
        point = ray.GetPoint(distance);
        return true;
    }

    private void CancelDrag()
    {
        if (_pressCard < 0) return;
        if (_dragging) _view.EndDrag();
        _pressCard = -1;
        _dragging = false;
    }

    private void ToggleSelected(int card)
    {
        if (!_selected.Remove(card)) _selected.Add(card);
        RefreshTints();
    }

    private void RefreshTints()
    {
        if (_view == null) return;
        _view.ClearTints(TableCardView.SelectedTint);
        foreach (int card in _selected) _view.SetTint(card, TableCardView.SelectedTint);
    }

    private void PlaySelected()
    {
        if (_selected.Count == 0)
        {
            Show("Click cards in your hand to select them, or drag one onto the pile.");
            return;
        }
        var ordered = BlackjackPlays.Order(_rules, _sm.Table, 0, _selected, chosenSuit: 0);
        if (ordered == null)
        {
            Show("Those cards aren't a run: they must be consecutive and all one suit.");
            return;
        }
        Play(ordered);
    }

    private void Play(int[] cards)
    {
        if (DeckEngine.GetRank(cards[cards.Length - 1]) == BlackjackRules.Ace)
        {
            // Check everything except the suit first, so the chooser only opens for a legal play
            var probe = _rules.CheckPlay(new PlayCardsOperation(0, cards, 0), _sm.Table, requireAnnouncement: true);
            if (!probe.Success)
            {
                Show(probe.Reason);
                return;
            }
            _awaitingSuit = cards;
            return;
        }
        Commit(cards, -1);
    }

    private void Commit(int[] cards, int suit)
    {
        var result = _sm.Apply(new PlayCardsOperation(0, cards, suit));
        _awaitingSuit = null;
        if (!result.Success) Show(result.Reason);
        else
        {
            _selected.Clear();
            RefreshTints();
        }
    }

    private void ChooseSuitKeys()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        for (int s = 0; s < 4; s++)
            if (kb[(Key)((int)Key.Digit1 + s)].wasPressedThisFrame)
            {
                Commit(_awaitingSuit, s);
                return;
            }
    }

    private void Draw() => Report(_sm.Apply(new DrawCardsOperation(0, _rules.DrawCount)));

    private void Catch()
    {
        for (int seat = 1; seat < _rules.Players; seat++)
            if (_rules.CanBeCaught(seat, _sm.Table))
            {
                if (_sm.Apply(new CatchLastCardOperation(0, seat)).Success)
                    Show($"Caught seat {seat}! They pick up {CatchLastCardOperation.Penalty}.");
                return;
            }
        Show("No one to catch — everyone on one card has called it.");
    }

    // ---- Feedback ----

    private void Report(OperationResult result)
    {
        if (!result.Success) Show(result.Reason);
    }

    private void Show(string message)
    {
        _message = message;
        _messageUntil = Time.time + 3.5f;
    }

    private static bool PointerOverUI()
        => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

    private void OnGUI()
    {
        if (_rules == null || _sm == null || !OwnsInput || ControlsPanel.AnyOpen) return;
        DrawScoreboard();
        if (_awaitingSuit != null) DrawSuitChooser();

        if (_history != null && _history.IsOpen) return;
        string hint = Hint();
        if (_message != null && Time.time < _messageUntil) hint += $"\n<color=#ffb070>{_message}</color>";
        PromptBox.Draw(hint);
    }

    private string Hint()
    {
        var table = _sm.Table;
        string K(ControlAction a) => Controls.Name(a);
        string always = $"{K(ControlAction.Undo)} undo · {K(ControlAction.History)} history · {K(ControlAction.Cancel)} controls";

        if (_rules.PhaseOf(table) == BlackjackRules.Phase.Finished)
            return $"{BlackjackRules.Who(_rules.Winner)} {(_rules.Winner == 0 ? "win" : "wins")}! " +
                   $"{K(ControlAction.Collect)} collects the cards for another game · {always}";

        string catchable = null;
        for (int seat = 1; seat < _rules.Players; seat++)
            if (_rules.CanBeCaught(seat, table)) catchable = $"Seat {seat} hasn't called LAST CARD — {K(ControlAction.CatchLastCard)} to catch them!\n";

        if (_rules.Turn != 0) return $"{catchable}Seat {_rules.Turn} is playing…";
        if (_awaitingSuit != null) return "Name the suit for your Ace: 1 Hearts · 2 Diamonds · 3 Clubs · 4 Spades";

        int count = table.Count(ZoneId.Hand(0));
        string call = !_rules.HasAnnounced(0) && count <= 3
            ? $" · {K(ControlAction.CallLastCard)} call LAST CARD before going down to one" : "";
        string task = _rules.Pending > 0
            ? $"Stack a {(_rules.PendingKind == BlackjackRules.Penalty.Twos ? "2" : "black jack")} or {K(ControlAction.DrawCard)} pick up {_rules.Pending}"
            : $"Select cards and {K(ControlAction.PlayCards)} to play (or drag one onto the pile) · {K(ControlAction.DrawCard)} pick up";
        return $"{catchable}Your turn: {task}{call}\n{always}";
    }

    private void DrawScoreboard()
    {
        var table = _sm.Table;
        var lines = new System.Text.StringBuilder("<b>BLACKJACK</b>");
        for (int seat = 0; seat < _rules.Players; seat++)
        {
            int count = table.Count(ZoneId.Hand(seat));
            string name = seat == 0 ? "You" : $"Seat {seat}";
            string turn = seat == _rules.Turn && _rules.Winner < 0 ? "> " : "   ";
            string call = _rules.HasAnnounced(seat) && count <= 1 ? "  <color=#ffd060>LAST CARD</color>" : "";
            lines.Append($"\n{turn}{name}: {count} card{(count == 1 ? "" : "s")}{call}   wins {_rules.Wins(seat)}");
        }
        if (_rules.Winner < 0)
            lines.Append($"\nFollow: {BlackjackRules.SuitName(_rules.SuitToFollow(table))}");

        var style = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.UpperLeft, richText = true, wordWrap = false };
        var content = new GUIContent(lines.ToString());
        var size = style.CalcSize(content);
        GUI.Box(new Rect(Screen.width - size.x - 20f, 70f, size.x + 8f, size.y + 4f), content, style);
    }

    private void DrawSuitChooser()
    {
        string[] names = { "Hearts", "Diamonds", "Clubs", "Spades" };
        float w = 130f, h = 44f, gap = 10f;
        float x = (Screen.width - (4 * w + 3 * gap)) * 0.5f, y = Screen.height * 0.45f;
        for (int s = 0; s < 4; s++)
            if (GUI.Button(new Rect(x + s * (w + gap), y, w, h), $"{s + 1}  {names[s]}"))
                Commit(_awaitingSuit, s);
        if (GUI.Button(new Rect((Screen.width - w) * 0.5f, y + h + gap, w, 32f), "Don't play the Ace"))
            _awaitingSuit = null;
    }
}
