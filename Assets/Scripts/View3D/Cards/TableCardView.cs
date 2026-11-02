using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

/// Identifies a card GameObject (for raycasts / interaction).
public class CardObject : MonoBehaviour
{
    public int CardId { get; internal set; }
}

/// Shows all 52 cards in 3D and animates every change. Actions are replayed move by move on a private
/// copy of the table, so each intermediate state is shown: a riffle visibly splits into two hands and
/// interleaves; a deal sends cards out one at a time. The view never decides anything; it mirrors
/// GameStateManager, with four temporary departures that interactions use:
///   • inspect — spread one stack apart so each card can be picked (cuts, riffle splits)
///   • preview — show moves the player is making live before they are committed as one action
///   • drag    — let the player hold a card under the mouse
///   • replay  — re-enact logged moves from a past state, then return to the present
public class TableCardView : MonoBehaviour, IStateManagerConsumer
{
    [Tooltip("Longest an action may take, however many moves it has (seconds).")]
    [SerializeField] float _maxActionDuration = 1.6f;
    [Tooltip("Longest gap between consecutive moves (seconds).")]
    [SerializeField] float _maxMoveStep = 0.11f;
    [Tooltip("Time for one card to travel to its new pose (seconds).")]
    [SerializeField] float _travelTime = 0.26f;
    [Tooltip("Height a card lifts while changing zone (metres).")]
    [SerializeField] float _arcHeight = 0.05f;
    [Tooltip("Longest a history replay may take (seconds).")]
    [SerializeField] float _maxReplayDuration = 7f;

    public GameStateManager StateManager { private get; set; }

    public TableLayout Layout { get; } = new TableLayout();

    /// The table as currently shown (equals the manager's except during preview or replay). Don't modify.
    public CardTable Shown => _shown;

    private GameStateManager _sm;
    private CardTable        _shown;
    private bool             _skipNextStateChange;

    private readonly Transform[]      _cards    = new Transform[DeckEngine.DeckSize];
    private readonly CardPose[]       _target   = new CardPose[DeckEngine.DeckSize];
    private readonly float[]          _queueEnd = new float[DeckEngine.DeckSize];
    private readonly Queue<Segment>[] _queues   = new Queue<Segment>[DeckEngine.DeckSize];
    private readonly Segment[]        _active   = new Segment[DeckEngine.DeckSize];

    private sealed class Segment
    {
        public float      Start, Duration, Arc;
        public CardPose   To;
        public Vector3    FromPos;
        public Quaternion FromRot;
    }

    private void Start()
    {
        if (StateManager == null)
        {
            Debug.LogError("[TableCardView] StateManager not set. Add GameStateManagerBridge to the scene.");
            return;
        }
        Initialise(StateManager);
    }

    private void OnEnable()  => ThemeService.Changed += ApplyTheme;
    private void OnDisable() => ThemeService.Changed -= ApplyTheme;

    private void OnDestroy()
    {
        Unsubscribe();
        ReleaseThemeAssets();
    }

    // ---- Theme ----

    private Material  _material;
    private Texture2D _generatedAtlas;

    /// Re-skins every card. Card meshes don't change: all themes share CardAtlas's layout.
    public void ApplyTheme(TableTheme theme)
    {
        if (_cards[0] == null) return;
        ReleaseThemeAssets();

        var style = theme.Cards;
        var atlas = style.AtlasOverride;
        if (atlas == null) atlas = _generatedAtlas = CardAtlas.Build(style);
        _material = Materials3D.Cutout($"Cards ({theme.DisplayName})", atlas, style.Smoothness);

        foreach (var card in _cards)
            card.GetComponent<MeshRenderer>().sharedMaterial = _material;
    }

    private void ReleaseThemeAssets()
    {
        DestroySafe(_material);
        DestroySafe(_generatedAtlas);
        _material = null;
        _generatedAtlas = null;
    }

    private static void DestroySafe(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    // ---- Setup ----

    /// Creates the cards (once), snaps them to the current state and starts listening for changes.
    /// Safe to call outside play mode (used by the editor preview capture).
    public void Initialise(GameStateManager sm)
    {
        Unsubscribe();
        _sm = sm;
        CreateCards();

        _shown = sm.Table.Clone();
        Layout.SeatCount = TableLayout.SeatsFor(_shown);
        for (int c = 0; c < DeckEngine.DeckSize; c++)
        {
            _queues[c].Clear();
            _active[c] = null;
            _target[c] = PoseOf(c);
            _cards[c].SetPositionAndRotation(_target[c].Position, _target[c].Rotation);
        }

        _sm.ActionApplied += OnActionApplied;
        _sm.StateChanged  += OnStateChanged;
    }

    private void Unsubscribe()
    {
        if (_sm == null) return;
        _sm.ActionApplied -= OnActionApplied;
        _sm.StateChanged  -= OnStateChanged;
    }

    private void CreateCards()
    {
        if (_cards[0] != null) return;
        for (int c = 0; c < DeckEngine.DeckSize; c++)
        {
            var go = new GameObject(CardNames.Of(c), typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider), typeof(CardObject));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = CardMeshBuilder.Build(c);
            go.GetComponent<BoxCollider>().size = new Vector3(CardMeshBuilder.Width, CardMeshBuilder.Thickness * 4f, CardMeshBuilder.Length);
            go.GetComponent<CardObject>().CardId = c;
            _cards[c]  = go.transform;
            _queues[c] = new Queue<Segment>();
        }
        ApplyTheme(ThemeService.Current);
    }

    /// The card under a screen ray, or −1.
    public static int CardAt(Ray ray, float maxDistance = 6f)
    {
        if (!Physics.Raycast(ray, out var hit, maxDistance)) return -1;
        var card = hit.collider.GetComponent<CardObject>();
        return card != null ? card.CardId : -1;
    }

    // ---- Following the game ----

    private void OnActionApplied(TableAction action, bool isUndo)
    {
        if (_previewCommitted)
        {
            // The interaction already showed these moves live; keep the picture if it matches
            _previewCommitted = false;
            _previewing = false;
            if (_shown.SameAs(_sm.Table))
            {
                _skipNextStateChange = true;
                return;
            }
            Debug.LogWarning("[TableCardView] Preview didn't match the committed action; resyncing.");
            Resync(gather: false);
            _skipNextStateChange = true;
            return;
        }
        if (_previewing || _replaying)
        {
            // Something else changed the table mid-preview/replay: drop it and follow the game
            _previewing = _replaying = false;
            Resync(gather: false);
            _skipNextStateChange = true;
            return;
        }

        var moves = new List<CardMove>(action.Moves.Count);
        if (isUndo)
            for (int i = action.Moves.Count - 1; i >= 0; i--) moves.Add(action.Moves[i].Inverse());
        else
            moves.AddRange(action.Moves);

        float step = Mathf.Min(_maxMoveStep, _maxActionDuration / Mathf.Max(1, moves.Count));
        float now  = Time.time;

        // More seats (e.g. dealing to 6 players) re-spaces every hand around the table
        int seats = TableLayout.SeatsFor(_sm.Table);
        if (seats != Layout.SeatCount)
        {
            Layout.SeatCount = seats;
            foreach (var zone in _shown.NonEmptyZones())
                if (zone.Kind == ZoneKind.Hand) RetargetZone(zone, now, NoneMoved);
        }

        for (int k = 0; k < moves.Count; k++)
            ShowMove(moves[k], now + k * step);

        _skipNextStateChange = true; // the StateChanged that follows describes the state we just animated to
    }

    private void OnStateChanged(GameStateSnapshot _)
    {
        if (_skipNextStateChange)
        {
            _skipNextStateChange = false;
            return;
        }
        // Reset or Load: no moves to replay, so gather every card straight to its new place
        _previewing = _previewCommitted = _replaying = false;
        Resync(gather: true);
    }

    /// Applies one move to the shown table and animates it starting at <paramref name="start"/>.
    private void ShowMove(CardMove move, float start)
    {
        var resolved = _shown.Apply(move);
        var moved = new HashSet<int>();
        var dest = _shown.Cards(resolved.To);
        for (int i = resolved.ToIndex; i < resolved.ToIndex + resolved.Count; i++) moved.Add(dest[i]);

        // Cards that travelled arc through the air; the rest of each affected zone just settles
        RetargetZone(resolved.From, start, moved);
        RetargetZone(resolved.To,   start, moved);
    }

    /// Snaps the shown table back to the manager's and animates every card there.
    private void Resync(bool gather)
    {
        _shown = _sm.Table.Clone();
        Layout.SeatCount = TableLayout.SeatsFor(_shown);
        float now = Time.time;
        for (int c = 0; c < DeckEngine.DeckSize; c++)
        {
            if (c == _dragged) continue;
            _queues[c].Clear();
            _queueEnd[c] = now;
            float delay = gather ? _shown.IndexOf(c) * 0.012f : 0f;
            Schedule(c, now + delay, PoseOf(c), _arcHeight);
        }
    }

    // ---- Preview: moves shown live, committed later as one action ----

    private bool _previewing, _previewCommitted;

    public bool IsPreviewing => _previewing;

    /// Starts showing moves that aren't in the game yet. The shown table equals the game's at this point.
    public void BeginPreview()
    {
        if (_replaying) EndReplay();
        _previewing = true;
        _previewCommitted = false;
    }

    /// Shows one move of the player's interaction immediately.
    public void PreviewMove(CardMove move)
    {
        if (!_previewing) throw new InvalidOperationException("BeginPreview first.");
        ShowMove(move, Time.time);
    }

    /// Ends the preview. Committed: the caller is about to apply the equivalent action, and the view keeps
    /// what it shows (no second animation) as long as the result matches. Not committed: cards animate
    /// back to the game's state.
    public void EndPreview(bool committed)
    {
        if (!_previewing) return;
        if (committed)
        {
            _previewCommitted = true;
            return;
        }
        // Also used when a commit was rejected: forget it and show the game as it really is
        _previewing = _previewCommitted = false;
        Resync(gather: false);
    }

    // ---- Replay: re-enact logged moves from a past state ----

    private bool  _replaying;
    private float _replayEnds;

    public bool IsReplaying => _replaying;

    /// Shows the table as it was in <paramref name="before"/>, plays <paramref name="moves"/> slowly, then
    /// returns to the present. Ignored while previewing.
    public void PlayReplay(CardTable before, IReadOnlyList<CardMove> moves)
    {
        if (_previewing || _sm == null) return;
        _replaying = true;
        _shown = before.Clone();
        float now = Time.time;

        // Gather to the starting picture first
        for (int c = 0; c < DeckEngine.DeckSize; c++)
        {
            _queues[c].Clear();
            _queueEnd[c] = now;
            Schedule(c, now, PoseOf(c), _arcHeight);
        }

        float start = now + _travelTime + 0.35f;
        float step = Mathf.Clamp(_maxReplayDuration / Mathf.Max(1, moves.Count), 0.05f, 0.3f);
        for (int k = 0; k < moves.Count; k++)
            ShowMove(moves[k], start + k * step);

        _replayEnds = start + moves.Count * step + _travelTime + 0.6f;
    }

    public void EndReplay()
    {
        if (!_replaying) return;
        _replaying = false;
        Resync(gather: false);
    }

    // ---- Drag: a card held under the mouse ----

    private int _dragged = -1;

    public int Dragged => _dragged;

    public void BeginDrag(int card)
    {
        _dragged = card;
        _queues[card].Clear();
        _active[card] = null;
    }

    public void DragTo(Vector3 position, Quaternion rotation)
    {
        if (_dragged >= 0) _cards[_dragged].SetPositionAndRotation(position, rotation);
    }

    /// Releases the held card. If an action already queued its next move it flies there from the mouse;
    /// otherwise it returns to where it belongs.
    public void EndDrag()
    {
        if (_dragged < 0) return;
        int card = _dragged;
        _dragged = -1;
        if (_queues[card].Count > 0) return;
        _queueEnd[card] = Time.time;
        Schedule(card, Time.time, PoseOf(card), _arcHeight * 0.5f);
    }

    // ---- Tints ----

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    public static readonly Color SelectedTint = new Color(1f, 0.82f, 0.45f);
    public static readonly Color TrackedTint  = new Color(0.55f, 0.85f, 1f);

    private readonly Dictionary<int, Color> _tints = new Dictionary<int, Color>();
    private MaterialPropertyBlock _block;

    public void SetTint(int card, Color? color)
    {
        if (_cards[card] == null) return;
        var r = _cards[card].GetComponent<MeshRenderer>();
        if (color.HasValue)
        {
            _tints[card] = color.Value;
            _block ??= new MaterialPropertyBlock();
            _block.SetColor(BaseColorId, color.Value);
            r.SetPropertyBlock(_block);
        }
        else
        {
            _tints.Remove(card);
            r.SetPropertyBlock(null);
        }
    }

    /// Removes every tint of one colour (e.g. all selection highlights, keeping the tracked card).
    public void ClearTints(Color color)
    {
        foreach (var card in new List<int>(_tints.Keys))
            if (_tints[card] == color) SetTint(card, null);
    }

    // ---- Inspecting a stack ----

    private ZoneId? _inspected;
    private float   _inspectSpacing, _inspectSlide;
    private int     _selStart = -1, _selCount;

    public bool IsInspecting => _inspected.HasValue;

    /// Spreads a stack apart (cards <paramref name="spacing"/> apart) so each card can be seen and picked.
    public void BeginInspect(ZoneId zone, float spacing, float slide)
    {
        _inspected      = zone;
        _inspectSpacing = spacing;
        _inspectSlide   = slide;
        ClearSelection();
        RetargetZone(zone, Time.time, NoneMoved);
    }

    /// Highlights a section of the inspected stack (indices into the zone); count 0 clears it.
    public void Select(int start, int count)
    {
        if (!_inspected.HasValue || (start == _selStart && count == _selCount)) return;
        ClearTints(SelectedTint);
        _selStart = start;
        _selCount = count;
        var cards = _shown.Cards(_inspected.Value);
        for (int i = Mathf.Max(0, start); i < start + count && i < cards.Count; i++)
            SetTint(cards[i], SelectedTint);
        RetargetZone(_inspected.Value, Time.time, NoneMoved);
    }

    public void ClearSelection() => Select(-1, 0);

    /// Squares the stack back up.
    public void EndInspect()
    {
        if (!_inspected.HasValue) return;
        var zone = _inspected.Value;
        ClearSelection();
        _inspected = null;
        RetargetZone(zone, Time.time, NoneMoved);
    }

    /// Jumps every card to its current target immediately (previews and tests).
    public void SnapAll()
    {
        for (int c = 0; c < DeckEngine.DeckSize; c++)
        {
            _queues[c].Clear();
            _active[c]   = null;
            _queueEnd[c] = 0f;
            _cards[c].SetPositionAndRotation(_target[c].Position, _target[c].Rotation);
        }
    }

    private static readonly HashSet<int> NoneMoved = new HashSet<int>();

    /// Where a card belongs right now: its layout pose, or its spread pose while its stack is inspected.
    private CardPose PoseOf(int card)
    {
        if (!_inspected.HasValue || _shown.LocationOf(card) != _inspected.Value)
            return Layout.Pose(_shown, card);

        int index = _shown.IndexOf(card);
        bool selected = index >= _selStart && index < _selStart + _selCount;
        return TableLayout.InspectPose(_shown, card, _inspectSpacing, selected ? _inspectSlide : 0f);
    }

    private void RetargetZone(ZoneId zone, float start, HashSet<int> moved)
    {
        foreach (int card in _shown.Cards(zone))
        {
            var pose = PoseOf(card);
            if (pose.Approximately(_target[card])) continue;
            Schedule(card, start, pose, moved.Contains(card) ? _arcHeight : 0f);
        }
    }

    private void Schedule(int card, float start, CardPose to, float arc)
    {
        start = Mathf.Max(start, _queueEnd[card]);
        float duration = arc > 0f ? _travelTime : _travelTime * 0.6f;
        _queues[card].Enqueue(new Segment { Start = start, Duration = duration, Arc = arc, To = to });
        _queueEnd[card] = start + duration;
        _target[card]   = to;
    }

    // ---- Animation ----

    private void Update()
    {
        if (_cards[0] == null) return;
        float now = Time.time;

        if (_replaying && now >= _replayEnds) EndReplay();

        for (int c = 0; c < DeckEngine.DeckSize; c++)
        {
            if (c == _dragged) continue;
            var t = _cards[c];
            while (true)
            {
                var seg = _active[c];
                if (seg == null)
                {
                    if (_queues[c].Count == 0 || _queues[c].Peek().Start > now) break;
                    seg = _active[c] = _queues[c].Dequeue();
                    seg.FromPos = t.position;
                    seg.FromRot = t.rotation;
                }

                float p = (now - seg.Start) / seg.Duration;
                if (p >= 1f)
                {
                    t.SetPositionAndRotation(seg.To.Position, seg.To.Rotation);
                    _active[c] = null;
                    continue; // the next segment may already be due
                }

                float e = p * p * (3f - 2f * p); // smoothstep
                var pos = Vector3.Lerp(seg.FromPos, seg.To.Position, e) + Vector3.up * (seg.Arc * Mathf.Sin(Mathf.PI * p));
                t.SetPositionAndRotation(pos, Quaternion.Slerp(seg.FromRot, seg.To.Rotation, e));
                break;
            }
        }
    }
}
