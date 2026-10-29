using System;
using System.Collections.Generic;

/// Gathers zones back into the deck. <see cref="Order"/> lists zones top-to-bottom: the first zone's cards
/// end up on top. The deck itself may appear in the list to place it among the others; if it is absent
/// it stays at the bottom. A null order collects every zone in canonical order (hands by seat, piles,
/// discards, held packets) on top of whatever remains in the deck.
/// Face-up cards are then turned face down individually (order within each zone is kept).
public sealed class CollectOperation : GameOperation
{
    public IReadOnlyList<ZoneId> Order { get; }

    public CollectOperation(IReadOnlyList<ZoneId> order = null) => Order = order;

    public static CollectOperation All() => new CollectOperation();

    public override void Execute(TableTransaction tx)
    {
        var table = tx.Table;
        var order = Order != null ? new List<ZoneId>(Order) : DefaultOrder(table);

        var seen = new HashSet<ZoneId>();
        foreach (var zone in order)
            if (!seen.Add(zone)) throw new ArgumentException($"Zone {zone} listed twice in collect order.");

        int deckPos = order.IndexOf(ZoneId.Deck);
        if (deckPos < 0) deckPos = order.Count;

        // Zones above the deck: insert at the top in reverse, so order[0] finishes on top
        for (int i = deckPos - 1; i >= 0; i--)
            Gather(tx, order[i], toBottom: false);
        // Zones below the deck: append in order
        for (int i = deckPos + 1; i < order.Count; i++)
            Gather(tx, order[i], toBottom: true);

        // Re-read the zone each pass: a flip briefly empties a 1-card zone, replacing its list
        for (int i = 0; i < table.Count(ZoneId.Deck); i++)
            if (table.IsFaceUp(table.Cards(ZoneId.Deck)[i]))
                tx.Move(CardMove.FlipInPlace(ZoneId.Deck, i));
    }

    /// Every non-empty zone except the deck, in canonical order.
    public static List<ZoneId> DefaultOrder(CardTable table)
    {
        var zones = table.NonEmptyZones();
        zones.Remove(ZoneId.Deck);
        return zones;
    }

    private static void Gather(TableTransaction tx, ZoneId zone, bool toBottom)
    {
        if (zone == ZoneId.Deck) return;
        int count = tx.Table.Count(zone);
        if (count == 0) return;
        tx.Move(new CardMove(zone, 0, count, ZoneId.Deck, toBottom ? CardMove.Bottom : 0));
    }
}
