using System;
using System.Collections.Generic;
using UnityEngine;

/// Owns the CardTable and is the only way to change it. Every operation is checked against the
/// active rule set, executed as an all-or-nothing transaction of CardMoves, and recorded so it can
/// be undone exactly.
public class GameStateManager
{
    public const int MaxHistorySize = 200;

    public CardTable Table       { get; private set; }
    public IRuleSet  Rules       { get; set; }
    public int       RoundNumber { get; private set; }
    public bool      CanUndo     => _history.Count > 0;
    public GamePhase Phase       => PhaseOf(Table);

    /// Cards in the deck, top first (a copy).
    public int[]   CurrentDeck  => ToArray(Table.Cards(ZoneId.Deck));
    /// Hands for seats 0..HandCount-1 (copies; unoccupied seats below the highest are empty arrays).
    public int[][] CurrentHands => HandsOf(Table);

    /// Fired after any change (apply, undo, reset, load) with a copy of the new state.
    public event Action<GameStateSnapshot>     StateChanged;
    /// Fired after an action is applied (isUndo = false) or reverted (isUndo = true). Carries the
    /// exact moves, so views can animate each card. Not fired for Reset/Load (use StateChanged).
    public event Action<TableAction, bool>     ActionApplied;
    public event Action<GameOperation, string> OperationRejected;

    private readonly List<HistoryEntry> _history = new List<HistoryEntry>();

    private readonly struct HistoryEntry
    {
        public readonly TableAction Action;
        public readonly int         RoundBefore;
        public HistoryEntry(TableAction action, int roundBefore) { Action = action; RoundBefore = roundBefore; }
    }

    public GameStateManager(IRuleSet rules = null)
    {
        Rules = rules ?? new FreePlayRules();
        Table = new CardTable();
    }

    public static GamePhase PhaseOf(CardTable table)
        => table.Count(ZoneId.Deck) == DeckEngine.DeckSize ? GamePhase.Idle : GamePhase.InPlay;

    public OperationResult Apply(GameOperation op)
    {
        var check = CanApplyRules(op);
        if (!check.Success) return Rejected(op, check.Reason);

        var tx = new TableTransaction(Table);
        try
        {
            op.Execute(tx);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
        {
            tx.Rollback();
            return Rejected(op, ex.Message);
        }

        if (tx.Applied.Count == 0)
            return Rejected(op, "Nothing to do.");

        var action = new TableAction(op.Name, new List<CardMove>(tx.Applied));
        _history.Add(new HistoryEntry(action, RoundNumber));
        if (_history.Count > MaxHistorySize)
            _history.RemoveAt(0);

        if (op is CollectOperation && Phase == GamePhase.Idle)
            RoundNumber++;

#if UNITY_EDITOR
        if (!Table.Validate())
            Debug.LogError($"[GameState] INVARIANT VIOLATED after {op.Name}: cards lost or duplicated!");
#endif

        Debug.Log($"[GameState] {action} applied. Phase={Phase}, Round={RoundNumber}");
        ActionApplied?.Invoke(action, false);
        StateChanged?.Invoke(TakeSnapshot());
        return OperationResult.Ok();
    }

    /// Dry run: rules check plus physical feasibility, without changing anything.
    public OperationResult CanApply(GameOperation op)
    {
        var check = CanApplyRules(op);
        if (!check.Success) return check;

        var tx = new TableTransaction(Table.Clone());
        try
        {
            op.Execute(tx);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException)
        {
            return OperationResult.Reject(ex.Message);
        }
        return tx.Applied.Count > 0 ? OperationResult.Ok() : OperationResult.Reject("Nothing to do.");
    }

    public OperationResult Undo()
    {
        if (!CanUndo)
            return OperationResult.Reject("Nothing to undo.");

        var entry = _history[_history.Count - 1];
        _history.RemoveAt(_history.Count - 1);

        entry.Action.Undo(Table);
        RoundNumber = entry.RoundBefore;

        Debug.Log($"[GameState] Undo {entry.Action.Name}. Phase={Phase}, Round={RoundNumber}");
        ActionApplied?.Invoke(entry.Action, true);
        StateChanged?.Invoke(TakeSnapshot());
        return OperationResult.Ok();
    }

    /// Resets to an ordered, face-down deck.
    public void Reset()
    {
        Table       = new CardTable();
        RoundNumber = 0;
        _history.Clear();
        StateChanged?.Invoke(TakeSnapshot());
    }

    public TableSaveData CreateSaveData() => TableSaveData.From(Table, RoundNumber);

    /// Replaces the table with saved state. Undo history is cleared. Throws ArgumentException on invalid data.
    public void Load(TableSaveData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        Table       = data.ToTable();
        RoundNumber = data.roundNumber;
        _history.Clear();
        StateChanged?.Invoke(TakeSnapshot());
    }

    public GameStateSnapshot TakeSnapshot()
        => new GameStateSnapshot(Phase, CurrentDeck, CurrentHands, RoundNumber, Table.Clone());

    private OperationResult CanApplyRules(GameOperation op)
    {
        if (op == null) return OperationResult.Reject("Operation is null.");
        return Rules.Check(op, Table);
    }

    private OperationResult Rejected(GameOperation op, string reason)
    {
        OperationRejected?.Invoke(op, reason);
        Debug.LogWarning($"[GameState] {op?.Name ?? "null"} rejected: {reason}");
        return OperationResult.Reject(reason);
    }

    private static int[][] HandsOf(CardTable table)
    {
        var hands = new int[table.HandCount()][];
        for (int seat = 0; seat < hands.Length; seat++)
            hands[seat] = ToArray(table.Cards(ZoneId.Hand(seat)));
        return hands;
    }

    private static int[] ToArray(IReadOnlyList<int> cards)
    {
        var result = new int[cards.Count];
        for (int i = 0; i < result.Length; i++) result[i] = cards[i];
        return result;
    }
}
