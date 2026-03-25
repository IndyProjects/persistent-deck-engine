using System;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;


/// Stage 2 entry point. Wires all input readers, recognizers, session manager and parameter generators. Holds the persistent deck state and dispatches to DeckEngine on each completed, valid gesture session.
public class InputCoordinator : MonoBehaviour
{
    // State

    public int[]   CurrentDeck  { get; private set; }
    public int[][] CurrentHands { get; private set; } = Array.Empty<int[]>();

    /// Raised after any DeckEngine operation updates the deck.
    public event Action<int[]> DeckChanged;

    // Pipeline pieces

    private MouseInputReader    _mouse;
    private TouchInputReader    _touch;
    private KeyboardInputReader _keyboard;
    private GamepadInputReader  _gamepad;

    private GestureRecognizerDispatcher _dispatcher;
    private GestureSessionManager       _sessionManager;

    private CutParamGenerator      _cutGen;
    private RiffleParamGenerator   _riffleGen;
    private OverhandParamGenerator _overhandGen;
    private DealParamGenerator     _dealGen;
    private CollectParamGenerator  _collectGen;

    // Unity lifecycle

    private void Awake()
    {
        EnhancedTouchSupport.Enable();
        CurrentDeck = DeckEngine.CreateOrderedDeck();
        BuildPipeline();
    }

    private void Update()
    {
        _mouse.Poll();
        _touch.Poll();
        _keyboard.Poll();
        _gamepad.Poll();
    }

    private void OnDestroy()
    {
        _mouse.Disable();
        _touch.Disable();
        _keyboard.Disable();
        _gamepad.Disable();
        EnhancedTouchSupport.Disable();
    }

    //construction


    private void BuildPipeline()
    {
        // Readers
        _mouse    = new MouseInputReader();
        _touch    = new TouchInputReader();
        _keyboard = new KeyboardInputReader();
        _gamepad  = new GamepadInputReader();

        _mouse.Enable();
        _touch.Enable();
        _keyboard.Enable();
        _gamepad.Enable();

        // Recognizers
        _dispatcher = new GestureRecognizerDispatcher();
        _dispatcher.Register(new CutRecognizer());
        _dispatcher.Register(new RiffleRecognizer());
        _dispatcher.Register(new OverhandRecognizer());
        _dispatcher.Register(new DealRecognizer());
        _dispatcher.Register(new CollectRecognizer());

        _dispatcher.Subscribe(_mouse);
        _dispatcher.Subscribe(_touch);
        _dispatcher.Subscribe(_keyboard);
        _dispatcher.Subscribe(_gamepad);

        // Session manager
        _sessionManager = new GestureSessionManager();
        _dispatcher.GestureRecognized += _sessionManager.OnGestureEvent;
        _sessionManager.SessionEnded  += OnSessionEnded;

        // Generators
        _cutGen      = new CutParamGenerator();
        _riffleGen   = new RiffleParamGenerator();
        _overhandGen = new OverhandParamGenerator();
        _dealGen     = new DealParamGenerator();
        _collectGen  = new CollectParamGenerator();
    }

    // Session dispatch

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

            case GestureType.RiffleStart when session is RiffleSession rs:
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
                // Build deck: hands + remaining deck 
                int[][] piles = BuildPiles();
                _collectGen.CurrentPiles = piles;
                var result = _collectGen.GenerateFromSession(cs);
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


    /// Assembles piles from CurrentHands + remaining CurrentDeck for CollectCards.
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
