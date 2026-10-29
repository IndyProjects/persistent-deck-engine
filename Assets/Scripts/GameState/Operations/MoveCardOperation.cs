using System;

/// Moves one specific card (by identity, wherever it currently is) to a position in a zone,
/// optionally turning it over. The "play a card" / "pick up a card" primitive for direct manipulation.
public sealed class MoveCardOperation : GameOperation
{
    public int    Card    { get; }
    public ZoneId To      { get; }
    /// Position in the destination; CardMove.Bottom places it at the bottom / end.
    public int    ToIndex { get; }
    public bool   Flip    { get; }

    public MoveCardOperation(int card, ZoneId to, int toIndex = 0, bool flip = false)
    {
        Card    = card;
        To      = to;
        ToIndex = toIndex;
        Flip    = flip;
    }

    public override string Name => Flip ? "Play" : "Move";

    public override void Execute(TableTransaction tx)
    {
        CheckCard(Card);
        tx.Move(new CardMove(tx.Table.LocationOf(Card), tx.Table.IndexOf(Card), 1, To, ToIndex, Flip));
    }

    internal static void CheckCard(int card)
    {
        if (card < 0 || card >= DeckEngine.DeckSize)
            throw new ArgumentOutOfRangeException(nameof(card), $"Card {card} is not in 0–51.");
    }
}

/// Turns one card over where it lies.
public sealed class FlipCardOperation : GameOperation
{
    public int Card { get; }
    public FlipCardOperation(int card) => Card = card;

    public override void Execute(TableTransaction tx)
    {
        MoveCardOperation.CheckCard(Card);
        tx.Move(CardMove.FlipInPlace(tx.Table.LocationOf(Card), tx.Table.IndexOf(Card)));
    }
}

/// Moves a contiguous packet between zones: the general case (cutting to a pile, picking up a trick, …).
public sealed class MovePacketOperation : GameOperation
{
    public CardMove Move { get; }
    public MovePacketOperation(CardMove move) => Move = move;

    public override void Execute(TableTransaction tx) => tx.Move(Move);
}
