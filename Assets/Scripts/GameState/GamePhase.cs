/// Derived from the table, not stored: Idle when all 52 cards are in the deck.
/// Only ClassicRules restricts operations by phase; in free play it is informational.
public enum GamePhase
{
    Idle,   // All 52 cards in the deck
    InPlay  // Some cards are out of the deck (hands, table, held)
}
