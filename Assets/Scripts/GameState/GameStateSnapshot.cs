/// Immutable copy of game state pushed to history to perserve card order
public readonly struct GameStateSnapshot
{
    public GamePhase Phase       { get; }
    public int[]     Deck        { get; }
    public int[][]   Hands       { get; }
    public int       RoundNumber { get; }

    public GameStateSnapshot(GamePhase phase, int[] deck, int[][] hands, int roundNumber)
    {
        Phase       = phase;
        Deck        = deck;
        Hands       = hands;
        RoundNumber = roundNumber;
    }
}
