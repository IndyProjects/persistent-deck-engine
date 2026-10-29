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
    private RiffleRecognizer            _riffleRecognizer;

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
        _riffleRecognizer = new RiffleRecognizer();
        _dispatcher.Register(_riffleRecognizer);
        _dispatcher.Register(new OverhandRecognizer());
        _dispatcher.Register(new DealRecognizer());
        _dispatcher.Register(new CollectRecognizer());

        foreach (var r in _readers) { r.Enable(); _dispatcher.Subscribe(r); }

        _sessionManager = new GestureSessionManager();
        _dispatcher.GestureRecognized += _sessionManager.OnGestureEvent;
        _sessionManager.SessionEnded  += OnSessionEnded;
        // Sessions read live table state when they start, so they always match what is on the table
        _sessionManager.DeckSize  = () => StateManager?.Table.Count(ZoneId.Deck) ?? DeckEngine.DeckSize;
        _sessionManager.PileCount = () => StateManager != null ? CollectablePiles().Count : 1;

        _cutGen      = new CutParamGenerator();
        _riffleGen   = new RiffleParamGenerator();
        _overhandGen = new OverhandParamGenerator();
        _dealGen     = new DealParamGenerator();
        _collectGen  = new CollectParamGenerator();
    }

    private void OnSessionEnded(IGestureSession session)
    {
        // Release the riffle's pointer capture once no riffle session remains
        // (a cancelled session replaced by a fresh RiffleStart keeps it).
        if (session.GestureType == GestureType.RiffleStart && !_sessionManager.IsActive(GestureType.RiffleStart))
            _riffleRecognizer.EndRiffle();

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
                _cutGen.DeckSize = DeckSize();
                var r = _cutGen.Generate(session.Events);
                if (Rejected(r)) return;
                LogClamped(r);
                StateManager.Apply(new CutOperation(r.Params.Position));
                break;
            }

            case GestureType.RiffleStart:
            {
                _riffleGen.DeckSize = DeckSize();
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
                _dealGen.DeckSize = DeckSize();
                var r = _dealGen.Generate(session.Events);
                if (Rejected(r)) return;
                LogClamped(r);
                StateManager.Apply(new DealOperation(r.Params.Players, r.Params.CardsEach));
                break;
            }

            case GestureType.CollectPile when session is CollectSession cs:
            {
                var zones = CollectablePiles();
                var piles = new int[zones.Count][];
                for (int i = 0; i < zones.Count; i++)
                    piles[i] = new List<int>(StateManager.Table.Cards(zones[i])).ToArray();

                var r = _collectGen.GenerateFromSession(cs, piles);
                if (Rejected(r)) return;

                var order = new ZoneId[r.Params.Order.Length];
                for (int i = 0; i < order.Length; i++)
                    order[i] = zones[r.Params.Order[i]];
                StateManager.Apply(new CollectOperation(order));
                break;
            }
        }
    }

    /// Pile indices used by collect gestures: every non-empty zone in canonical order
    /// (hands by seat, table piles, discards, held packets), with the deck last.
    private List<ZoneId> CollectablePiles()
    {
        var zones = CollectOperation.DefaultOrder(StateManager.Table);
        if (StateManager.Table.Count(ZoneId.Deck) > 0)
            zones.Add(ZoneId.Deck);
        return zones;
    }

    private int DeckSize() => StateManager.Table.Count(ZoneId.Deck);

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
