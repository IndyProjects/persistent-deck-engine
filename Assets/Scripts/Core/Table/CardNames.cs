/// Human-readable names for cards and zones (history, prompts, debugging).
public static class CardNames
{
    private static readonly string[] Ranks = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "Jack", "Queen", "King", "Ace" };
    private static readonly string[] Suits = { "Hearts", "Diamonds", "Clubs", "Spades" };

    public static string Of(int card) => $"{Ranks[DeckEngine.GetRank(card)]} of {Suits[DeckEngine.GetSuit(card)]}";

    /// Seat 0 is the local player.
    public static string Of(ZoneId zone)
    {
        switch (zone.Kind)
        {
            case ZoneKind.Deck:    return "the deck";
            case ZoneKind.Hand:    return zone.Index == 0 ? "your hand" : $"seat {zone.Index}'s hand";
            case ZoneKind.Pile:    return $"table pile {zone.Index + 1}";
            case ZoneKind.Discard: return zone.Index == 0 ? "the discard" : $"discard {zone.Index + 1}";
            case ZoneKind.Held:    return zone.Index == 0 ? "the dealer's left hand" : "the dealer's right hand";
            default:               return zone.ToString();
        }
    }
}
