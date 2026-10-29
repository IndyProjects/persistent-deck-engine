using System;

/// Physical overhand: the deck is held in the right hand and the left hand pulls chunks off the top,
/// each landing on top of the previous ones. <see cref="ChunkSizes"/> must sum to the deck size.
/// For a full deck the result matches DeckEngine.OverhandShuffle exactly.
public sealed class OverhandOperation : GameOperation
{
    public int[] ChunkSizes { get; }
    public OverhandOperation(int[] chunkSizes) => ChunkSizes = chunkSizes;

    public override void Execute(TableTransaction tx)
    {
        int n = tx.Table.Count(ZoneId.Deck);
        if (ChunkSizes == null || ChunkSizes.Length == 0)
            throw new ArgumentException("ChunkSizes must not be empty.", nameof(ChunkSizes));
        int total = 0;
        foreach (int size in ChunkSizes)
        {
            if (size <= 0) throw new ArgumentException("Each chunk size must be greater than 0.", nameof(ChunkSizes));
            total += size;
        }
        if (total != n)
            throw new ArgumentException($"Chunk sizes must sum to the deck size {n} (got {total}).", nameof(ChunkSizes));
        RequireEmptyHands(tx.Table);

        tx.Move(CardMove.TopToTop(ZoneId.Deck, n, ZoneId.RightHand));
        foreach (int size in ChunkSizes)
            tx.Move(CardMove.TopToTop(ZoneId.RightHand, size, ZoneId.LeftHand));
        tx.Move(CardMove.TopToTop(ZoneId.LeftHand, n, ZoneId.Deck));
    }
}
