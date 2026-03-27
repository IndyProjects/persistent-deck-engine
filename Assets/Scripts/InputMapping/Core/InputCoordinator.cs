using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;

/// Stage 2 entry point. Translates completed gesture sessions into GameOperations
/// and applies them via GameStateManager and Set StateManager before gestures
public class InputCoordinator : MonoBehaviour
{
    /// Injected by GameStateManagerBridge at runtime. Must be set for gestures to have any effect.
    public GameStateManager StateManager { get; set; }

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

        if (StateManager == null)
        {
            Debug.LogError("[InputCoordinator] StateManager is null. Add GameStateManagerBridge to the scene.");
            return;
        }

        switch (session.GestureType)
        {
            case GestureType.Cut:
            {
                var r = _cutGen.Generate(session.Events);
                if (Rejected(r)) return;
                LogClamped(r);
                StateManager.Apply(new CutOperation(r.Params.Position));
                break;
            }

            case GestureType.RiffleStart:
            {
                var r = _riffleGen.Generate(session.Events);
                if (Rejected(r)) return;
                LogClamped(r);
                StateManager.Apply(new RiffleOperation(r.Params.SplitIndex, r.Params.DropPattern));
                break;
            }

            case GestureType.OverhandGrab when session is OverhandSession os:
            {
                var r = _overhandGen.GenerateFromSession(os);
                if (Rejected(r)) return;
                StateManager.Apply(new OverhandOperation(r.Params.ChunkSizes));
                break;
            }

            case GestureType.DealStart:
            {
                var r = _dealGen.Generate(session.Events);
                if (Rejected(r)) return;
                LogClamped(r);
                var op = StateManager.Apply(new DealOperation(r.Params.Players, r.Params.CardsEach));
                if (op.Success)
                    _sessionManager.ActivePileCount = PileCount();
                break;
            }

            case GestureType.CollectPile when session is CollectSession cs:
            {
                int[][] piles = BuildPiles();
                var r = _collectGen.GenerateFromSession(cs, piles);
                if (Rejected(r)) return;
                var op = StateManager.Apply(new CollectOperation(r.Params.Piles, r.Params.Order));
                if (op.Success)
                    _sessionManager.ActivePileCount = 1;
                break;
            }
        }
    }

    private int[][] BuildPiles()
    {
        int[][] hands         = StateManager.CurrentHands;
        int[]   remainingDeck = StateManager.CurrentDeck;
        bool    hasRemaining  = remainingDeck != null && remainingDeck.Length > 0;

        int[][] piles = new int[hands.Length + (hasRemaining ? 1 : 0)][];
        for (int i = 0; i < hands.Length; i++)
            piles[i] = hands[i];
        if (hasRemaining)
            piles[hands.Length] = remainingDeck;
        return piles;
    }

    private int PileCount()
    {
        int[] deck = StateManager.CurrentDeck;
        return StateManager.CurrentHands.Length + (deck != null && deck.Length > 0 ? 1 : 0);
    }

    private bool Rejected<T>(ParameterResult<T> r)
    {
        if (r.Status != ParameterStatus.Rejected) return false;
        Debug.LogWarning($"[InputCoordinator] Parameter rejected: {r.Reason}");
        return true;
    }

    private static void LogClamped<T>(ParameterResult<T> r)
    {
        if (r.Status == ParameterStatus.Clamped)
            Debug.Log($"[InputCoordinator] Clamped: {r.Reason}");
    }
}
