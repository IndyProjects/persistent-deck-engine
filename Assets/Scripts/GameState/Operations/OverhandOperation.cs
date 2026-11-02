using System;

/// Physical overhand: one hand holds the packet while the other pulls chunks off the top, each landing
/// on top of the previous ones; the shuffled packet ends where it started (on the table, or back in the
/// holding hand). <see cref="ChunkSizes"/> must sum to the packet size.
/// For a full deck the result matches DeckEngine.OverhandShuffle exactly.
public sealed class OverhandOperation : GameOperation
{
    public int[]  ChunkSizes { get; }
    public ZoneId Source     { get; }

    public OverhandOperation(int[] chunkSizes) : this(chunkSizes, ZoneId.Deck) { }

    public OverhandOperation(int[] chunkSizes, ZoneId source)
    {
        ChunkSizes = chunkSizes;
        Source     = source;
    }

    public override void Execute(TableTransaction tx)
    {
        int n = tx.Table.Count(Source);
        if (ChunkSizes == null || ChunkSizes.Length == 0)
            throw new ArgumentException("ChunkSizes must not be empty.", nameof(ChunkSizes));
        int total = 0;
        foreach (int size in ChunkSizes)
        {
            if (size <= 0) throw new ArgumentException("Each chunk size must be greater than 0.", nameof(ChunkSizes));
            total += size;
        }
        if (total != n)
            throw new ArgumentException($"Chunk sizes must sum to the packet size {n} (got {total}).", nameof(ChunkSizes));
        RequireShuffleSource(tx.Table, Source);

        // From the table the right hand picks the deck up; a held packet stays in its hand
        var holding = Source == ZoneId.Deck ? ZoneId.RightHand : Source;
        var pulling = OtherHand(holding);

        if (Source == ZoneId.Deck)
            tx.Move(CardMove.TopToTop(ZoneId.Deck, n, holding));
        foreach (int size in ChunkSizes)
            tx.Move(CardMove.TopToTop(holding, size, pulling));
        tx.Move(CardMove.TopToTop(pulling, n, Source));
    }
}
