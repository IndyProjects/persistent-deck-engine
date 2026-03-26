public sealed class CutOperation : GameOperation
{
    public int Position { get; }
    public CutOperation(int position) => Position = position;
}
