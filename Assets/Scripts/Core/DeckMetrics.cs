using System;
using System.Collections.Generic;

/// Measures of how well mixed a deck order is, relative to factory order (card 0 on top … 51 at the bottom).
public static class DeckMetrics
{
    /// Expected rising sequences in a uniformly random 52-card deck: (n + 1) / 2.
    public const float RandomRisingSequences = (DeckEngine.DeckSize + 1) / 2f;

    /// Expected count of factory neighbours still adjacent in a random deck: (n − 1) / n ≈ 1.
    public const float RandomAdjacentPairs = (DeckEngine.DeckSize - 1f) / DeckEngine.DeckSize;

    /// Riffles needed to mix a 52-card deck well (Bayer &amp; Diaconis, 1992).
    public const int RifflesToMix = 7;

    /// Number of rising sequences: maximal runs of consecutive factory values (v, v+1, …) that appear in
    /// increasing positions. A factory-fresh deck has 1; each riffle can at most double it, so it is the
    /// standard measure of how much riffling a deck has had.
    public static int RisingSequences(IReadOnlyList<int> order)
    {
        var position = Positions(order);
        int sequences = 1;
        for (int v = 1; v < order.Count; v++)
            if (position[v] < position[v - 1]) sequences++;
        return sequences;
    }

    /// Cards still directly on top of their factory successor (v immediately followed by v+1).
    /// Random decks have about one; a deck that has only been cut keeps nearly all of them.
    public static int AdjacentPairs(IReadOnlyList<int> order)
    {
        int pairs = 0;
        for (int i = 0; i + 1 < order.Count; i++)
            if (order[i + 1] == order[i] + 1) pairs++;
        return pairs;
    }

    private static int[] Positions(IReadOnlyList<int> order)
    {
        if (order == null || order.Count != DeckEngine.DeckSize)
            throw new ArgumentException("Metrics need the full 52-card order.", nameof(order));
        var position = new int[DeckEngine.DeckSize];
        for (int i = 0; i < order.Count; i++) position[order[i]] = i;
        return position;
    }
}
