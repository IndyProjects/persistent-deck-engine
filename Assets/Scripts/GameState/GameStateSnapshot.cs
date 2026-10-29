/// Copy of game state passed to StateChanged listeners. Safe to keep: nothing in it is shared
/// with the live GameStateManager.
public readonly struct GameStateSnapshot
{
    public GamePhase Phase       { get; }
    public int[]     Deck        { get; }
    public int[][]   Hands       { get; }
    public int       RoundNumber { get; }
    /// Full copy of the table: every zone, position and face.
    public CardTable Table       { get; }

    public GameStateSnapshot(GamePhase phase, int[] deck, int[][] hands, int roundNumber, CardTable table)
    {
        Phase       = phase;
        Deck        = deck;
        Hands       = hands;
        RoundNumber = roundNumber;
        Table       = table;
    }
}
