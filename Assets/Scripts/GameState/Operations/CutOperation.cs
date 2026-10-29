using System;

/// Takes the top <see cref="Position"/> cards of the deck and puts them on the bottom.
public sealed class CutOperation : GameOperation
{
    public int Position { get; }
    public CutOperation(int position) => Position = position;

    public override void Execute(TableTransaction tx)
    {
        int n = tx.Table.Count(ZoneId.Deck);
        if (Position < 1 || Position > n - 1)
            throw new ArgumentOutOfRangeException(nameof(Position), $"Cut position must be between 1 and {n - 1}.");

        tx.Move(CardMove.TopToBottom(ZoneId.Deck, Position, ZoneId.Deck));
    }
}
