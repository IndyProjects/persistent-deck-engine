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
    /// Every action taken with this deck (persisted), for card journeys, replays and mix statistics.
    /// Separate from the undo stack: undoing is itself logged.
    public DeckHistory History   { get; private set; }
    public IRuleSet  Rules       { get; set; }
    public int       RoundNumber { get; private set; }
    public bool      CanUndo     => _undo.Count > 0;
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

    private readonly List<HistoryEntry> _undo = new List<HistoryEntry>();

    private readonly struct HistoryEntry
    {
        public readonly TableAction Action;
        public readonly int         RoundBefore;
        public readonly string      RulesStateBefore;

        public HistoryEntry(TableAction action, int roundBefore, string rulesStateBefore)
        {
            Action           = action;
            RoundBefore      = roundBefore;
            RulesStateBefore = rulesStateBefore;
        }
    }

    private IStatefulRuleSet StatefulRules => Rules as IStatefulRuleSet;

    public GameStateManager(IRuleSet rules = null)
    {
        Rules   = rules ?? new FreePlayRules();
        Table   = new CardTable();
        History = new DeckHistory(Table);
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

        if (tx.Applied.Count == 0 && !op.AllowNoMoves)
            return Rejected(op, "Nothing to do.");

        var action = new TableAction(op.Name, new List<CardMove>(tx.Applied));
        _undo.Add(new HistoryEntry(action, RoundNumber, StatefulRules?.SaveState()));
        History.Record(new DeckHistory.Entry(op.Name, RoundNumber, DeckHistory.KindOf(op), action.Moves));
        if (_undo.Count > MaxHistorySize)
            _undo.RemoveAt(0);

        if (op is CollectOperation && Phase == GamePhase.Idle)
            RoundNumber++;
        StatefulRules?.OnApplied(op, Table);

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
        return tx.Applied.Count > 0 || op.AllowNoMoves ? OperationResult.Ok() : OperationResult.Reject("Nothing to do.");
    }

    public OperationResult Undo()
    {
        if (!CanUndo)
            return OperationResult.Reject("Nothing to undo.");

        var entry = _undo[_undo.Count - 1];
        _undo.RemoveAt(_undo.Count - 1);

        entry.Action.Undo(Table);
        var inverse = new List<CardMove>(entry.Action.Moves.Count);
        for (int i = entry.Action.Moves.Count - 1; i >= 0; i--) inverse.Add(entry.Action.Moves[i].Inverse());
        History.Record(new DeckHistory.Entry($"Undo {entry.Action.Name}", RoundNumber, ActionKind.Undo, inverse));
        RoundNumber = entry.RoundBefore;
        StatefulRules?.LoadState(entry.RulesStateBefore);

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
        History     = new DeckHistory(Table);
        _undo.Clear();
        StatefulRules?.LoadState(null);
        StateChanged?.Invoke(TakeSnapshot());
    }

    public TableSaveData CreateSaveData()
    {
        var data = TableSaveData.From(Table, RoundNumber);
        data.rulesState = StatefulRules?.SaveState();
        data.history    = History.ToSave();
        return data;
    }

    /// Replaces the table with saved state. Undo history is cleared. Throws ArgumentException on invalid data.
    public void Load(TableSaveData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        Table       = data.ToTable();
        RoundNumber = data.roundNumber;
        _undo.Clear();
        History = RestoreHistory(data, Table);
        StatefulRules?.LoadState(data.rulesState);
        StateChanged?.Invoke(TakeSnapshot());
    }

    /// A saved history is kept only if it leads exactly to the saved table; otherwise the log restarts.
    private static DeckHistory RestoreHistory(TableSaveData data, CardTable table)
    {
        if (data.history == null) return new DeckHistory(table);
        try
        {
            return DeckHistory.FromSave(data.history, table);
        }
        catch (ArgumentException ex)
        {
            Debug.LogWarning($"[GameState] Saved history ignored: {ex.Message}");
            return new DeckHistory(table);
        }
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
