using System;

/// Deals round-robin from the top of the deck: one card to each seat in turn, <see cref="CardsEach"/> rounds.
/// Each card is appended to the seat's hand, so hand[k] is the k-th card that player received
/// (matching DeckEngine.DealCards). Dealing into hands that already hold cards adds to them.
public sealed class DealOperation : GameOperation
{
    public int  Players   { get; }
    public int  CardsEach { get; }
    /// Turn each card over as it is dealt (e.g. community cards).
    public bool FaceUp    { get; }

    public DealOperation(int players, int cardsEach, bool faceUp = false)
    {
        Players   = players;
        CardsEach = cardsEach;
        FaceUp    = faceUp;
    }

    public override void Execute(TableTransaction tx)
    {
        if (Players <= 0)   throw new ArgumentOutOfRangeException(nameof(Players),   "Players must be greater than 0.");
        if (CardsEach <= 0) throw new ArgumentOutOfRangeException(nameof(CardsEach), "CardsEach must be greater than 0.");
        int n = tx.Table.Count(ZoneId.Deck);
        if (Players * CardsEach > n)
            throw new ArgumentException($"Cannot deal {Players * CardsEach} cards from a deck of {n}.");

        for (int round = 0; round < CardsEach; round++)
            for (int seat = 0; seat < Players; seat++)
                tx.Move(new CardMove(ZoneId.Deck, 0, 1, ZoneId.Hand(seat), CardMove.Bottom, FaceUp));
    }
}
