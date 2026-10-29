using System;
using NUnit.Framework;

/// DeckEngine is the reference implementation of each shuffle; operation tests check the
/// move-based versions against it.
public class DeckEngineTests
{
    private static int[] Ordered => DeckEngine.CreateOrderedDeck();

    private static int[] Ones(int n)
    {
        var a = new int[n];
        for (int i = 0; i < n; i++) a[i] = 1;
        return a;
    }

    [Test]
    public void CreateOrderedDeck_IsSequential()
    {
        var deck = Ordered;
        Assert.AreEqual(52, deck.Length);
        for (int i = 0; i < 52; i++) Assert.AreEqual(i, deck[i]);
    }

    [Test]
    public void ValidateDeck_RejectsBadDecks()
    {
        Assert.IsTrue(DeckEngine.ValidateDeck(Ordered));
        Assert.IsFalse(DeckEngine.ValidateDeck(null));
        Assert.IsFalse(DeckEngine.ValidateDeck(new int[51]));
        var duped = Ordered; duped[1] = 0;
        Assert.IsFalse(DeckEngine.ValidateDeck(duped));
    }

    [Test]
    public void CutDeck_SwapsHalves_WithoutMutatingInput()
    {
        var ordered = Ordered;
        var cut = DeckEngine.CutDeck(ordered, 26);
        Assert.AreEqual(26, cut[0]);
        Assert.AreEqual(25, cut[51]);
        Assert.AreEqual(0, ordered[0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => DeckEngine.CutDeck(ordered, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => DeckEngine.CutDeck(ordered, 52));
    }

    [Test]
    public void RiffleShuffle_DropsFromPacketBottoms_FirstDropLandsAtBottom()
    {
        var riffle = DeckEngine.RiffleShuffle(Ordered, 26, Ones(52));
        Assert.IsTrue(DeckEngine.ValidateDeck(riffle));
        Assert.AreEqual(25, riffle[51], "first drop (left packet bottom) is at the deck bottom");
        Assert.AreEqual(51, riffle[50], "second drop (right packet bottom) is above it");
        Assert.AreEqual(26, riffle[0],  "last drop (right packet top) is on top");
        Assert.AreEqual(0,  riffle[1]);
    }

    [Test]
    public void RiffleShuffle_UnevenPattern_KeepsPacketOrder()
    {
        var r = DeckEngine.RiffleShuffle(Ordered, 26, new[] { 3, 2 });
        Assert.IsTrue(DeckEngine.ValidateDeck(r));
        CollectionAssert.AreEqual(new[] { 23, 24, 25 }, new[] { r[49], r[50], r[51] });
        CollectionAssert.AreEqual(new[] { 50, 51 },     new[] { r[47], r[48] });
    }

    [Test]
    public void RiffleShuffle_OversizedDrop_ClampsToAvailable()
        => Assert.IsTrue(DeckEngine.ValidateDeck(DeckEngine.RiffleShuffle(Ordered, 26, new[] { 52 })));

    [Test]
    public void OverhandShuffle_ReversesChunkOrder()
    {
        var o = DeckEngine.OverhandShuffle(Ordered, new[] { 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 2 });
        Assert.IsTrue(DeckEngine.ValidateDeck(o));
        Assert.AreEqual(50, o[0]);
        Assert.Throws<ArgumentException>(() => DeckEngine.OverhandShuffle(Ordered, new[] { 10, 10 }));
    }

    [Test]
    public void DealCards_RoundRobinFromTop()
    {
        var (hands, remaining) = DeckEngine.DealCards(Ordered, 4, 5);
        Assert.AreEqual(4, hands.Length);
        Assert.AreEqual(5, hands[0].Length);
        Assert.AreEqual(32, remaining.Length);
        Assert.AreEqual(0, hands[0][0]);
        Assert.AreEqual(1, hands[1][0]);
        Assert.AreEqual(4, hands[0][1]);
        Assert.AreEqual(20, remaining[0]);
    }

    [Test]
    public void CollectCards_ConcatenatesInOrder()
    {
        var piles = new int[4][];
        for (int p = 0; p < 4; p++)
        {
            piles[p] = new int[13];
            for (int i = 0; i < 13; i++) piles[p][i] = p * 13 + i;
        }
        var collected = DeckEngine.CollectCards(piles, new[] { 3, 2, 1, 0 });
        Assert.AreEqual(39, collected[0]);
        Assert.AreEqual(12, collected[51]);
    }

    [Test]
    public void ShuffleChain_PreservesAllCards()
    {
        var d = DeckEngine.CutDeck(Ordered, 17);
        d = DeckEngine.RiffleShuffle(d, 26, new[] { 2, 3, 2, 3, 2, 3, 2, 3, 2, 3, 2, 3, 2, 3, 2, 3, 2, 3 });
        d = DeckEngine.OverhandShuffle(d, new[] { 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4 });
        Assert.IsTrue(DeckEngine.ValidateDeck(d));
    }

    [Test]
    public void SuitAndRank_Encoding()
    {
        Assert.AreEqual(0, DeckEngine.GetSuit(0));
        Assert.AreEqual(1, DeckEngine.GetSuit(13));
        Assert.AreEqual(2, DeckEngine.GetSuit(26));
        Assert.AreEqual(3, DeckEngine.GetSuit(39));
        Assert.AreEqual(0,  DeckEngine.GetRank(0),  "2 of Hearts");
        Assert.AreEqual(12, DeckEngine.GetRank(12), "Ace of Hearts");
    }
}
