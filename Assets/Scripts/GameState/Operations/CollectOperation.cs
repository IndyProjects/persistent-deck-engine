public sealed class CollectOperation : GameOperation
{
    public int[][] Piles { get; }
    public int[]   Order { get; }

    public CollectOperation(int[][] piles, int[] order)
    {
        Piles = piles;
        Order = order;
    }
}
