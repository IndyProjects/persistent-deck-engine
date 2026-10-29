using System.Collections.Generic;
using UnityEngine;

/// Identifies a card GameObject (for raycasts / interaction).
public class CardObject : MonoBehaviour
{
    public int CardId { get; internal set; }
}

/// Shows all 52 cards in 3D and animates every change. Actions are replayed move by move on a private
/// copy of the table, so each intermediate state is shown: a riffle visibly splits into two hands and
/// interleaves; a deal sends cards out one at a time. The view never decides anything; it only mirrors
/// GameStateManager.
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

    public GameStateManager StateManager { private get; set; }

    public TableLayout Layout { get; } = new TableLayout();

    private GameStateManager _sm;
    private CardTable        _shown;
    private bool             _skipNextStateChange;

    private readonly Transform[]      _cards  = new Transform[DeckEngine.DeckSize];
    private readonly CardPose[]       _target = new CardPose[DeckEngine.DeckSize];
    private readonly float[]          _queueEnd = new float[DeckEngine.DeckSize];
    private readonly Queue<Segment>[] _queues = new Queue<Segment>[DeckEngine.DeckSize];
    private readonly Segment[]        _active = new Segment[DeckEngine.DeckSize];

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
            _target[c] = Layout.Pose(_shown, c);
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
            var go = new GameObject(CardName(c), typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider), typeof(CardObject));
            go.transform.SetParent(transform, false);
            go.GetComponent<MeshFilter>().sharedMesh = CardMeshBuilder.Build(c);
            go.GetComponent<BoxCollider>().size = new Vector3(CardMeshBuilder.Width, CardMeshBuilder.Thickness * 4f, CardMeshBuilder.Length);
            go.GetComponent<CardObject>().CardId = c;
            _cards[c]  = go.transform;
            _queues[c] = new Queue<Segment>();
        }
        ApplyTheme(ThemeService.Current);
    }

    // ---- Change handling ----

    private void OnActionApplied(TableAction action, bool isUndo)
    {
        var moves = new List<CardMove>(action.Moves.Count);
        if (isUndo)
            for (int i = action.Moves.Count - 1; i >= 0; i--) moves.Add(action.Moves[i].Inverse());
        else
            moves.AddRange(action.Moves);

        float step = Mathf.Min(_maxMoveStep, _maxActionDuration / Mathf.Max(1, moves.Count));
        float now  = Time.time;
        var moved  = new HashSet<int>();

        // More seats (e.g. dealing to 6 players) re-spaces every hand around the table
        int seats = TableLayout.SeatsFor(_sm.Table);
        if (seats != Layout.SeatCount)
        {
            Layout.SeatCount = seats;
            foreach (var zone in _shown.NonEmptyZones())
                if (zone.Kind == ZoneKind.Hand) RetargetZone(zone, now, moved);
        }

        for (int k = 0; k < moves.Count; k++)
        {
            var move = moves[k];
            _shown.Apply(move);

            moved.Clear();
            var dest = _shown.Cards(move.To);
            for (int i = move.ToIndex; i < move.ToIndex + move.Count; i++) moved.Add(dest[i]);

            // Cards that travelled arc through the air; the rest of each affected zone just settles
            float start = now + k * step;
            RetargetZone(move.From, start, moved);
            RetargetZone(move.To,   start, moved);
        }

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
        _shown = _sm.Table.Clone();
        Layout.SeatCount = TableLayout.SeatsFor(_shown);
        float now = Time.time;
        for (int c = 0; c < DeckEngine.DeckSize; c++)
        {
            _queues[c].Clear();
            _queueEnd[c] = now;
            var pose = Layout.Pose(_shown, c);
            Schedule(c, now + _shown.IndexOf(c) * 0.012f, pose, _arcHeight);
        }
    }

    private void RetargetZone(ZoneId zone, float start, HashSet<int> moved)
    {
        foreach (int card in _shown.Cards(zone))
        {
            var pose = Layout.Pose(_shown, card);
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

        for (int c = 0; c < DeckEngine.DeckSize; c++)
        {
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

    private static readonly string[] RankNames = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "Jack", "Queen", "King", "Ace" };
    private static readonly string[] SuitNames = { "Hearts", "Diamonds", "Clubs", "Spades" };

    private static string CardName(int c) => $"{RankNames[DeckEngine.GetRank(c)]} of {SuitNames[DeckEngine.GetSuit(c)]}";
}
