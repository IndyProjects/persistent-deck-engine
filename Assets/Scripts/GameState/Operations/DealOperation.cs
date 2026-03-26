public sealed class DealOperation : GameOperation
{
    public int Players   { get; }
    public int CardsEach { get; }

    public DealOperation(int players, int cardsEach)
    {
        Players   = players;
        CardsEach = cardsEach;
    }
}
