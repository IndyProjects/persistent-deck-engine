public sealed class OverhandOperation : GameOperation
{
    public int[] ChunkSizes { get; }
    public OverhandOperation(int[] chunkSizes) => ChunkSizes = chunkSizes;
}
