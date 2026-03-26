public sealed class RiffleOperation : GameOperation
{
    public int   SplitIndex  { get; }
    public int[] DropPattern { get; }

    public RiffleOperation(int splitIndex, int[] dropPattern)
    {
        SplitIndex  = splitIndex;
        DropPattern = dropPattern;
    }
}
