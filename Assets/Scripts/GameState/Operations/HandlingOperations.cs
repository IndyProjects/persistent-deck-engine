using System;

/// Picks the deck up off the table into one hand (the dealer's grip for shuffling and dealing).
public sealed class PickUpDeckOperation : GameOperation
{
    public ZoneId Hand { get; }

    public PickUpDeckOperation() : this(ZoneId.RightHand) { }

    public PickUpDeckOperation(ZoneId hand)
    {
        if (hand.Kind != ZoneKind.Held) throw new ArgumentException("Pick the deck up into a hand (Held zone).", nameof(hand));
        Hand = hand;
    }

    public override void Execute(TableTransaction tx)
    {
        int n = tx.Table.Count(ZoneId.Deck);
        if (n == 0) throw new InvalidOperationException("There is no deck on the table to pick up.");
        if (tx.Table.Count(Hand) > 0) throw new InvalidOperationException("That hand is already holding cards.");
        tx.Move(CardMove.TopToTop(ZoneId.Deck, n, Hand));
    }
}

/// Puts a held packet down on the table's deck spot (on top of anything already there).
public sealed class PutDownDeckOperation : GameOperation
{
    public ZoneId Hand { get; }

    public PutDownDeckOperation() : this(ZoneId.RightHand) { }

    public PutDownDeckOperation(ZoneId hand)
    {
        if (hand.Kind != ZoneKind.Held) throw new ArgumentException("Put down from a hand (Held zone).", nameof(hand));
        Hand = hand;
    }

    public override void Execute(TableTransaction tx)
    {
        int n = tx.Table.Count(Hand);
        if (n == 0) throw new InvalidOperationException("That hand isn't holding anything.");
        tx.Move(CardMove.TopToTop(Hand, n, ZoneId.Deck));
    }
}

/// Deals one packet of <see cref="Count"/> cards from the top of <see cref="From"/> to a seat — the
/// building block for dealing "one at a time", "two at a time", or any rhythm the dealer chooses.
/// Cards are appended to the hand in the order received, so the packet's top card comes first.
public sealed class DealPacketOperation : GameOperation
{
    public int    Seat   { get; }
    public int    Count  { get; }
    public ZoneId From   { get; }
    public bool   FaceUp { get; }

    public DealPacketOperation(int seat, int count) : this(seat, count, ZoneId.RightHand) { }

    public DealPacketOperation(int seat, int count, ZoneId from, bool faceUp = false)
    {
        Seat   = seat;
        Count  = count;
        From   = from;
        FaceUp = faceUp;
    }

    public override string Name => $"Deal {Count} to seat {Seat}";

    public override void Execute(TableTransaction tx)
    {
        if (Seat < 0)  throw new ArgumentOutOfRangeException(nameof(Seat), "Seat must be >= 0.");
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count), "Deal at least one card.");
        int available = tx.Table.Count(From);
        if (Count > available)
            throw new ArgumentException($"Cannot deal {Count} card(s): only {available} left in {From}.");

        if (FaceUp)
            // Turn each card over individually so the packet keeps its order
            for (int i = 0; i < Count; i++)
                tx.Move(new CardMove(From, 0, 1, ZoneId.Hand(Seat), CardMove.Bottom, flip: true));
        else
            tx.Move(new CardMove(From, 0, Count, ZoneId.Hand(Seat), CardMove.Bottom));
    }
}
