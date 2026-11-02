using System;

/// Operations for shedding games (Blackjack and similar): play cards onto the discard pile, draw from
/// the draw pile (the deck zone), turn up the first card, and the LAST CARD call.

/// Plays one card, or a run, from a seat's hand onto the discard pile face up, in the given order
/// (the last card ends on top). <see cref="ChosenSuit"/> is the suit named when the last card is an Ace.
public sealed class PlayCardsOperation : GameOperation
{
    public int   Seat       { get; }
    public int[] Cards      { get; }
    public int   ChosenSuit { get; }

    public PlayCardsOperation(int seat, int[] cards, int chosenSuit = -1)
    {
        Seat = seat;
        Cards = cards;
        ChosenSuit = chosenSuit;
    }

    public override string Name => Cards != null && Cards.Length == 1
        ? $"Seat {Seat} plays {CardNames.Of(Cards[0])}"
        : $"Seat {Seat} plays a run of {Cards?.Length ?? 0}";

    public override void Execute(TableTransaction tx)
    {
        if (Cards == null || Cards.Length == 0) throw new ArgumentException("Choose at least one card to play.");
        var hand = ZoneId.Hand(Seat);
        foreach (int card in Cards)
        {
            MoveCardOperation.CheckCard(card);
            if (tx.Table.LocationOf(card) != hand)
                throw new InvalidOperationException($"{CardNames.Of(card)} isn't in seat {Seat}'s hand.");
            tx.Move(new CardMove(hand, tx.Table.IndexOf(card), 1, ZoneId.Discard(), 0, flip: !tx.Table.IsFaceUp(card)));
        }
    }
}

/// Draws cards from the top of the draw pile into a seat's hand. If the pile runs out, the discard pile
/// (all but its top card) is turned over to become the new draw pile. Drawing nothing is allowed — the
/// turn still passes — when every card is already in someone's hand.
public sealed class DrawCardsOperation : GameOperation
{
    public int Seat  { get; }
    public int Count { get; }

    public DrawCardsOperation(int seat, int count)
    {
        Seat = seat;
        Count = count;
    }

    public override bool AllowNoMoves => true;

    public override string Name => $"Seat {Seat} picks up {Count}";

    public override void Execute(TableTransaction tx)
    {
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count), "Draw at least one card.");
        Draw(tx, Seat, Count);
    }

    /// Shared with CatchLastCardOperation: draws up to <paramref name="count"/> cards, recycling the discards.
    internal static void Draw(TableTransaction tx, int seat, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (tx.Table.Count(ZoneId.Deck) == 0) TurnOverDiscards(tx);
            if (tx.Table.Count(ZoneId.Deck) == 0) return; // nothing left anywhere
            int top = tx.Table.Cards(ZoneId.Deck)[0];
            tx.Move(new CardMove(ZoneId.Deck, 0, 1, ZoneId.Hand(seat), CardMove.Bottom, flip: tx.Table.IsFaceUp(top)));
        }
    }

    /// Turns the discard pile (keeping its top card) over to form a new face-down draw pile.
    private static void TurnOverDiscards(TableTransaction tx)
    {
        int under = tx.Table.Count(ZoneId.Discard()) - 1;
        if (under <= 0) return;
        tx.Move(new CardMove(ZoneId.Discard(), 1, under, ZoneId.Deck, 0, flip: true));
    }
}

/// Turns the top card of the draw pile face up to start the discard pile.
public sealed class StartDiscardOperation : GameOperation
{
    public override string Name => "Turn up the first card";

    public override void Execute(TableTransaction tx)
    {
        if (tx.Table.Count(ZoneId.Deck) == 0) throw new InvalidOperationException("There's no draw pile to start from.");
        if (tx.Table.Count(ZoneId.Discard()) > 0) throw new InvalidOperationException("The discard pile has already started.");
        int top = tx.Table.Cards(ZoneId.Deck)[0];
        tx.Move(new CardMove(ZoneId.Deck, 0, 1, ZoneId.Discard(), 0, flip: !tx.Table.IsFaceUp(top)));
    }
}

/// A seat calls "LAST CARD!". Moves no cards; the rules remember the call.
public sealed class AnnounceLastCardOperation : GameOperation
{
    public int Seat { get; }
    public AnnounceLastCardOperation(int seat) => Seat = seat;

    public override bool AllowNoMoves => true;
    public override string Name => $"Seat {Seat} calls LAST CARD";
    public override void Execute(TableTransaction tx) { }
}

/// <see cref="Catcher"/> catches <see cref="Target"/> holding one card without having called LAST CARD:
/// the target picks up two.
public sealed class CatchLastCardOperation : GameOperation
{
    public const int Penalty = 2;

    public int Catcher { get; }
    public int Target  { get; }

    public CatchLastCardOperation(int catcher, int target)
    {
        Catcher = catcher;
        Target = target;
    }

    public override bool AllowNoMoves => true;
    public override string Name => $"Seat {Catcher} catches seat {Target}: pick up {Penalty}";
    public override void Execute(TableTransaction tx) => DrawCardsOperation.Draw(tx, Target, Penalty);
}
