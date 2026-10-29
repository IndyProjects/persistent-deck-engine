using System.Collections.Generic;

/// Applies a sequence of moves to a CardTable as one all-or-nothing unit.
/// If any move fails, <see cref="Rollback"/> restores the table exactly by applying inverses in reverse.
public class TableTransaction
{
    public CardTable Table { get; }

    private readonly List<CardMove> _applied = new List<CardMove>();
    public IReadOnlyList<CardMove> Applied => _applied;

    public TableTransaction(CardTable table) => Table = table;

    /// Applies a move immediately (later moves see its result) and records the resolved form.
    public void Move(CardMove move) => _applied.Add(Table.Apply(move));

    public void Rollback()
    {
        for (int i = _applied.Count - 1; i >= 0; i--)
            Table.Apply(_applied[i].Inverse());
        _applied.Clear();
    }
}

/// One completed player action (deal, cut, play a card, …) and the exact moves it made.
/// Undoing applies the inverses of <see cref="Moves"/> in reverse order.
public sealed class TableAction
{
    public string                  Name  { get; }
    public IReadOnlyList<CardMove> Moves { get; }

    public TableAction(string name, IReadOnlyList<CardMove> moves)
    {
        Name  = name;
        Moves = moves;
    }

    public void Undo(CardTable table)
    {
        for (int i = Moves.Count - 1; i >= 0; i--)
            table.Apply(Moves[i].Inverse());
    }

    public override string ToString() => $"{Name} ({Moves.Count} moves)";
}
