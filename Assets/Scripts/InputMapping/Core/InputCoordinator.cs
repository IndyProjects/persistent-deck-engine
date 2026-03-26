using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;


/// Stage 2 entry point. Wires all input readers, recognizers, session manager and parameter generators. Holds the persistent deck state and dispatches to DeckEngine on each completed, valid gesture session.
public class InputCoordinator : MonoBehaviour
{
    public int[]   CurrentDeck  { get; private set; }
    public int[][] CurrentHands { get; private set; } = Array.Empty<int[]>();

    /// Raised after any DeckEngine operation updates the deck.
    public event Action<int[]> DeckChanged;

    private List<IInputReader> _readers;

    private GestureRecognizerDispatcher _dispatcher;
    private GestureSessionManager       _sessionManager;

    private CutParamGenerator      _cutGen;
    private RiffleParamGenerator   _riffleGen;
    private OverhandParamGenerator _overhandGen;
    private DealParamGenerator     _dealGen;
    private CollectParamGenerator  _collectGen;

    private void Awake()
    {
        EnhancedTouchSupport.Enable();
        CurrentDeck = DeckEngine.CreateOrderedDeck();
        BuildPipeline();
    }

    private void Update()
    {
        foreach (var r in _readers) r.Poll();
    }

    private void OnDestroy()
    {
        foreach (var r in _readers) r.Disable();
        EnhancedTouchSupport.Disable();
    }

    private void BuildPipeline()
    {
        _readers = new List<IInputReader>
        {
            new MouseInputReader(),
            new TouchInputReader(),
            new KeyboardInputReader(),
            new GamepadInputReader()
        };

        _dispatcher = new GestureRecognizerDispatcher();
        _dispatcher.Register(new CutRecognizer());
        _dispatcher.Register(new RiffleRecognizer());
        _dispatcher.Register(new OverhandRecognizer());
        _dispatcher.Register(new DealRecognizer());
        _dispatcher.Register(new CollectRecognizer());

        foreach (var r in _readers) { r.Enable(); _dispatcher.Subscribe(r); }

        _sessionManager = new GestureSessionManager();
        _dispatcher.GestureRecognized += _sessionManager.OnGestureEvent;
        _sessionManager.SessionEnded  += OnSessionEnded;

        _cutGen      = new CutParamGenerator();
        _riffleGen   = new RiffleParamGenerator();
        _overhandGen = new OverhandParamGenerator();
        _dealGen     = new DealParamGenerator();
        _collectGen  = new CollectParamGenerator();
    }

    private void OnSessionEnded(IGestureSession session)
    {
        if (session.IsCancelled)
        {
            Debug.LogWarning($"[InputCoordinator] {session.GestureType} session cancelled.");
            return;
        }

        switch (session.GestureType)
        {
            case GestureType.Cut:
            {
                var result = _cutGen.Generate(session.Events);
                Apply(result, p =>
                {
                    CurrentDeck = DeckEngine.CutDeck(CurrentDeck, p.Position);
                    Debug.Log($"[DeckEngine] CutDeck: position={p.Position}");
                });
                break;
            }

            case GestureType.RiffleStart:
            {
                var result = _riffleGen.Generate(session.Events);
                Apply(result, p =>
                {
                    CurrentDeck = DeckEngine.RiffleShuffle(CurrentDeck, p.SplitIndex, p.DropPattern);
                    Debug.Log($"[DeckEngine] RiffleShuffle: splitIndex={p.SplitIndex}, drops={p.DropPattern.Length}");
                });
                break;
            }

            case GestureType.OverhandGrab when session is OverhandSession os:
            {
                var result = _overhandGen.GenerateFromSession(os);
                Apply(result, p =>
                {
                    CurrentDeck = DeckEngine.OverhandShuffle(CurrentDeck, p.ChunkSizes);
                    Debug.Log($"[DeckEngine] OverhandShuffle: {p.ChunkSizes.Length} chunks");
                });
                break;
            }

            case GestureType.DealStart:
            {
                var result = _dealGen.Generate(session.Events);
                Apply(result, p =>
                {
                    var (hands, remaining) = DeckEngine.DealCards(CurrentDeck, p.Players, p.CardsEach);
                    CurrentHands = hands;
                    CurrentDeck  = remaining;
                    _sessionManager.ActivePileCount = hands.Length + (remaining.Length > 0 ? 1 : 0);
                    Debug.Log($"[DeckEngine] DealCards: {p.Players} players, {p.CardsEach} each, {remaining.Length} remaining");
                });
                break;
            }

            case GestureType.CollectPile when session is CollectSession cs:
            {
                int[][] piles = BuildPiles();
                var result = _collectGen.GenerateFromSession(cs, piles);
                Apply(result, p =>
                {
                    CurrentDeck  = DeckEngine.CollectCards(p.Piles, p.Order);
                    CurrentHands = Array.Empty<int[]>();
                    _sessionManager.ActivePileCount = 1;
                    Debug.Log($"[DeckEngine] CollectCards: {p.Piles.Length} piles, order=[{string.Join(",", p.Order)}]");
                });
                break;
            }
        }
    }

    private void Apply<T>(ParameterResult<T> result, Action<T> action)
    {
        if (result.Status == ParameterStatus.Rejected)
        {
            Debug.LogWarning($"[InputCoordinator] Rejected: {result.Reason}");
            return;
        }
        if (result.Status == ParameterStatus.Clamped)
            Debug.Log($"[InputCoordinator] Clamped: {result.Reason}");

        action(result.Params);

#if UNITY_EDITOR
        if (!DeckEngine.ValidateDeck(CurrentDeck))
            Debug.LogError("[InputCoordinator] INVARIANT VIOLATED: CurrentDeck is not a valid 52-card deck!");
#endif

        DeckChanged?.Invoke(CurrentDeck);
    }

    private int[][] BuildPiles()
    {
        int handCount = CurrentHands.Length;
        bool hasRemaining = CurrentDeck != null && CurrentDeck.Length > 0;
        int total = handCount + (hasRemaining ? 1 : 0);

        int[][] piles = new int[total][];
        for (int i = 0; i < handCount; i++)
            piles[i] = CurrentHands[i];
        if (hasRemaining)
            piles[handCount] = CurrentDeck;

        return piles;
    }
}
