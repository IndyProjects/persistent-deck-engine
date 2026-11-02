using System;

/// Takes the top <see cref="Position"/> cards of the packet and puts them on the bottom.
/// Works on the deck on the table or on a packet held in a hand.
public sealed class CutOperation : GameOperation
{
    public int    Position { get; }
    public ZoneId Source   { get; }

    public CutOperation(int position) : this(position, ZoneId.Deck) { }

    public CutOperation(int position, ZoneId source)
    {
        Position = position;
        Source   = source;
    }

    public override void Execute(TableTransaction tx)
    {
        int n = tx.Table.Count(Source);
        if (Position < 1 || Position > n - 1)
            throw new ArgumentOutOfRangeException(nameof(Position), $"Cut position must be between 1 and {n - 1}.");

        tx.Move(CardMove.TopToBottom(Source, Position, Source));
    }
}

/// Pulls a contiguous section out of the packet and places it on top. Taking everything from
/// <see cref="Start"/> to the bottom is an ordinary cut (same as CutOperation(Start)); a section from
/// the middle is a pulled cut. The section keeps its own order.
public sealed class CutSectionOperation : GameOperation
{
    /// Index of the section's first (top-most) card; 0 = top of the packet.
    public int    Start  { get; }
    public int    Count  { get; }
    public ZoneId Source { get; }

    public CutSectionOperation(int start, int count, ZoneId source)
    {
        Start  = start;
        Count  = count;
        Source = source;
    }

    public override string Name => $"Cut {Count} to top";

    public override void Execute(TableTransaction tx)
    {
        int n = tx.Table.Count(Source);
        if (Count < 1)
            throw new ArgumentOutOfRangeException(nameof(Count), "Select at least one card.");
        if (Start < 1)
            throw new ArgumentOutOfRangeException(nameof(Start), "That section is already on top.");
        if (Start + Count > n)
            throw new ArgumentOutOfRangeException(nameof(Start), $"The section runs past the bottom of the {n}-card packet.");

        tx.Move(new CardMove(Source, Start, Count, Source, 0));
    }
}
