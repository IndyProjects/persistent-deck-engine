using System;
using System.Collections.Generic;
using UnityEngine;


public class GameStateManager
{
    public const int MaxHistorySize = 20;

    public GamePhase Phase        { get; private set; } = GamePhase.Idle;
    public int[]     CurrentDeck  { get; private set; }
    public int[][]   CurrentHands { get; private set; } = Array.Empty<int[]>();
    public int       RoundNumber  { get; private set; }
    public bool      CanUndo      => _history.Count > 0;

    public event Action<GameStateSnapshot>     StateChanged;
    public event Action<GameOperation, string> OperationRejected;

    private readonly List<GameStateSnapshot> _history = new List<GameStateSnapshot>();

    public GameStateManager()
    {
        CurrentDeck = DeckEngine.CreateOrderedDeck();
    }

    /// Checks phase legality
    public OperationResult Apply(GameOperation op)
    {
        var check = CanApply(op);
        if (!check.Success)
        {
            OperationRejected?.Invoke(op, check.Reason);
            Debug.LogWarning($"[GameState] {op.GetType().Name} rejected: {check.Reason}");
            return check;
        }

        PushHistory();
        Execute(op);

#if UNITY_EDITOR
        if (!DeckEngine.ValidateDeck(CurrentDeck))
            Debug.LogError($"[GameState] INVARIANT VIOLATED after {op.GetType().Name}: deck is invalid!");
#endif

        StateChanged?.Invoke(TakeSnapshot());
        Debug.Log($"[GameState] {op.GetType().Name} applied. Phase={Phase}, Round={RoundNumber}");
        return OperationResult.Ok();
    }

    /// Dry-run phase check
    public OperationResult CanApply(GameOperation op)
    {
        switch (op)
        {
            case CutOperation _:
            case RiffleOperation _:
            case OverhandOperation _:
                if (Phase == GamePhase.InPlay)
                    return OperationResult.Reject("Cannot shuffle while hands are dealt.");
                return OperationResult.Ok();

            case DealOperation _:
                if (Phase == GamePhase.InPlay)
                    return OperationResult.Reject("Cards already dealt. Collect first.");
                return OperationResult.Ok();

            case CollectOperation _:
                if (Phase == GamePhase.Idle)
                    return OperationResult.Reject("No hands to collect.");
                return OperationResult.Ok();

            default:
                return OperationResult.Reject($"Unknown operation type: {op.GetType().Name}");
        }
    }

    public OperationResult Undo()
    {
        if (!CanUndo)
            return OperationResult.Reject("Nothing to undo.");

        var snap     = _history[_history.Count - 1];
        _history.RemoveAt(_history.Count - 1);

        Phase        = snap.Phase;
        CurrentDeck  = snap.Deck;
        CurrentHands = snap.Hands;
        RoundNumber  = snap.RoundNumber;

        StateChanged?.Invoke(TakeSnapshot());
        Debug.Log($"[GameState] Undo. Phase={Phase}, Round={RoundNumber}");
        return OperationResult.Ok();
    }

    /// Resets to an ordered deck
    public void Reset()
    {
        Phase        = GamePhase.Idle;
        CurrentDeck  = DeckEngine.CreateOrderedDeck();
        CurrentHands = Array.Empty<int[]>();
        RoundNumber  = 0;
        _history.Clear();
        StateChanged?.Invoke(TakeSnapshot());
    }

    private void Execute(GameOperation op)
    {
        switch (op)
        {
            case CutOperation cut:
                CurrentDeck = DeckEngine.CutDeck(CurrentDeck, cut.Position);
                break;

            case RiffleOperation riffle:
                CurrentDeck = DeckEngine.RiffleShuffle(CurrentDeck, riffle.SplitIndex, riffle.DropPattern);
                break;

            case OverhandOperation overhand:
                CurrentDeck = DeckEngine.OverhandShuffle(CurrentDeck, overhand.ChunkSizes);
                break;

            case DealOperation deal:
                var (hands, remaining) = DeckEngine.DealCards(CurrentDeck, deal.Players, deal.CardsEach);
                CurrentHands = hands;
                CurrentDeck  = remaining;
                Phase        = GamePhase.InPlay;
                break;

            case CollectOperation collect:
                CurrentDeck  = DeckEngine.CollectCards(collect.Piles, collect.Order);
                CurrentHands = Array.Empty<int[]>();
                Phase        = GamePhase.Idle;
                RoundNumber++;
                break;
        }
    }

    private void PushHistory()
    {
        _history.Add(TakeSnapshot());
        if (_history.Count > MaxHistorySize)
            _history.RemoveAt(0);
    }

    private GameStateSnapshot TakeSnapshot()
        => new GameStateSnapshot(Phase, CurrentDeck, CurrentHands, RoundNumber);
}
