using System;
using System.Collections.Generic;
using NUnit.Framework;

/// Each move-based operation must reproduce its DeckEngine reference for a full deck, work on
/// partial decks, and leave the table untouched when it fails.
public class OperationTests
{
    private static int[] Cards(CardTable t, ZoneId z) => new List<int>(t.Cards(z)).ToArray();

    private static CardTable Run(GameOperation op, CardTable table = null)
    {
        table = table ?? new CardTable();
        op.Execute(new TableTransaction(table));
        Assert.IsTrue(table.Validate(), "52-card invariant");
        return table;
    }

    /// Deterministic pseudo-random deck so equivalence is checked on non-trivial orders too.
    private static int[] ScrambledDeck(int seed)
    {
        var deck = DeckEngine.CreateOrderedDeck();
        var rng  = new Random(seed);
        for (int i = deck.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
        return deck;
    }

    [TestCase(1), TestCase(17), TestCase(26), TestCase(51)]
    public void Cut_MatchesDeckEngine(int position)
    {
        var start = ScrambledDeck(position);
        var t = Run(new CutOperation(position), new CardTable(start));
        CollectionAssert.AreEqual(DeckEngine.CutDeck(start, position), Cards(t, ZoneId.Deck));
    }

    private static IEnumerable<TestCaseData> RiffleCases()
    {
        yield return new TestCaseData(26, new[] { 1 });
        yield return new TestCaseData(26, new[] { 3, 2 });
        yield return new TestCaseData(10, new[] { 2, 3, 2, 3, 2, 3, 2, 3, 2, 3, 2, 3 });
        yield return new TestCaseData(40, new[] { 52 });
        yield return new TestCaseData(1,  new[] { 0, 5 });   // left drops nothing on the first turn
        yield return new TestCaseData(51, new[] { 9, 9, 9, 9, 9, 9 });
    }

    [TestCaseSource(nameof(RiffleCases))]
    public void Riffle_MatchesDeckEngine(int split, int[] pattern)
    {
        var start = ScrambledDeck(split);
        var t = Run(new RiffleOperation(split, pattern), new CardTable(start));
        CollectionAssert.AreEqual(DeckEngine.RiffleShuffle(start, split, pattern), Cards(t, ZoneId.Deck));
        Assert.AreEqual(0, t.Count(ZoneId.LeftHand));
        Assert.AreEqual(0, t.Count(ZoneId.RightHand));
    }

    [Test]
    public void Riffle_ProducesExpectedHands_FromManualTestGuide()
    {
        // Guide step D1: split 26, drops of 9, then deal 2×5
        var t = Run(new RiffleOperation(26, new[] { 9, 9, 9, 9, 9, 9 }));
        Run(new DealOperation(2, 5), t);
        CollectionAssert.AreEqual(new[] { 26, 28, 30, 32, 0 }, Cards(t, ZoneId.Hand(0)));
        CollectionAssert.AreEqual(new[] { 27, 29, 31, 33, 1 }, Cards(t, ZoneId.Hand(1)));
    }

    [Test]
    public void Overhand_MatchesDeckEngine()
    {
        var start  = ScrambledDeck(7);
        var chunks = new[] { 5, 5, 5, 5, 5, 5, 5, 5, 5, 5, 2 };
        var t = Run(new OverhandOperation(chunks), new CardTable(start));
        CollectionAssert.AreEqual(DeckEngine.OverhandShuffle(start, chunks), Cards(t, ZoneId.Deck));
    }

    [Test]
    public void Deal_MatchesDeckEngine()
    {
        var start = ScrambledDeck(3);
        var t = Run(new DealOperation(4, 5), new CardTable(start));
        var (hands, remaining) = DeckEngine.DealCards(start, 4, 5);
        for (int seat = 0; seat < 4; seat++)
            CollectionAssert.AreEqual(hands[seat], Cards(t, ZoneId.Hand(seat)));
        CollectionAssert.AreEqual(remaining, Cards(t, ZoneId.Deck));
    }

    [Test]
    public void Deal_FaceUp_TurnsCardsOver()
    {
        var t = Run(new DealOperation(1, 3, faceUp: true));
        foreach (int c in t.Cards(ZoneId.Hand(0))) Assert.IsTrue(t.IsFaceUp(c));
        Assert.IsFalse(t.IsFaceUp(t.Cards(ZoneId.Deck)[0]));
    }

    [Test]
    public void Collect_DefaultOrder_MatchesDeckEngine()
    {
        var t = Run(new DealOperation(2, 5));
        var piles = new[] { Cards(t, ZoneId.Hand(0)), Cards(t, ZoneId.Hand(1)), Cards(t, ZoneId.Deck) };
        Run(CollectOperation.All(), t);
        CollectionAssert.AreEqual(DeckEngine.CollectCards(piles, new[] { 0, 1, 2 }), Cards(t, ZoneId.Deck));
    }

    [Test]
    public void Collect_ExplicitOrder_WithDeckInMiddle_MatchesDeckEngine()
    {
        var t = Run(new DealOperation(3, 4));
        var piles = new[] { Cards(t, ZoneId.Hand(0)), Cards(t, ZoneId.Hand(1)), Cards(t, ZoneId.Hand(2)), Cards(t, ZoneId.Deck) };
        Run(new CollectOperation(new[] { ZoneId.Hand(2), ZoneId.Deck, ZoneId.Hand(0), ZoneId.Hand(1) }), t);
        CollectionAssert.AreEqual(DeckEngine.CollectCards(piles, new[] { 2, 3, 0, 1 }), Cards(t, ZoneId.Deck));
    }

    [Test]
    public void Collect_TurnsFaceUpCardsDown_AndGathersEveryZone()
    {
        var t = Run(new DealOperation(2, 3));
        Run(new MoveCardOperation(t.Cards(ZoneId.Hand(0))[0], ZoneId.Pile(0), flip: true), t);
        Run(new MovePacketOperation(CardMove.TopToTop(ZoneId.Deck, 4, ZoneId.Discard())), t);
        Run(CollectOperation.All(), t);

        Assert.AreEqual(52, t.Count(ZoneId.Deck));
        for (int c = 0; c < 52; c++) Assert.IsFalse(t.IsFaceUp(c));
    }

    [Test]
    public void Collect_RejectsDuplicateZones()
    {
        var t = Run(new DealOperation(2, 3));
        Assert.Throws<ArgumentException>(() =>
            new CollectOperation(new[] { ZoneId.Hand(0), ZoneId.Hand(0) }).Execute(new TableTransaction(t)));
    }

    [Test]
    public void PartialDeck_ShufflesWorkOnWhatRemains()
    {
        var t = Run(new DealOperation(2, 5)); // 42 left
        var before = Cards(t, ZoneId.Deck);

        Run(new CutOperation(20), t);
        Run(new RiffleOperation(21, new[] { 1 }), t);
        Run(new OverhandOperation(new[] { 40, 2 }), t);

        Assert.AreEqual(42, t.Count(ZoneId.Deck));
        CollectionAssert.AreEquivalent(before, Cards(t, ZoneId.Deck));
        CollectionAssert.AreNotEqual(before, Cards(t, ZoneId.Deck));
    }

    [Test]
    public void MoveCard_PlaysSpecificCardFromAHand()
    {
        var t = Run(new DealOperation(2, 5));
        int card = t.Cards(ZoneId.Hand(1))[2];
        Run(new MoveCardOperation(card, ZoneId.Pile(0), flip: true), t);
        Assert.AreEqual(ZoneId.Pile(0), t.LocationOf(card));
        Assert.IsTrue(t.IsFaceUp(card));
        Assert.AreEqual(4, t.Count(ZoneId.Hand(1)));
    }

    [Test]
    public void FlipCard_TurnsCardWhereItLies()
    {
        var t = Run(new FlipCardOperation(30));
        Assert.IsTrue(t.IsFaceUp(30));
        Assert.AreEqual(30, t.IndexOf(30));
    }

    private static IEnumerable<TestCaseData> ImpossibleCases()
    {
        yield return new TestCaseData(new CutOperation(0)).SetName("Cut at 0");
        yield return new TestCaseData(new CutOperation(52)).SetName("Cut at 52");
        yield return new TestCaseData(new RiffleOperation(0, new[] { 1 })).SetName("Riffle split 0");
        yield return new TestCaseData(new RiffleOperation(26, new int[0])).SetName("Riffle empty pattern");
        yield return new TestCaseData(new RiffleOperation(26, new[] { -1 })).SetName("Riffle negative drop");
        yield return new TestCaseData(new OverhandOperation(new[] { 10, 10 })).SetName("Overhand short");
        yield return new TestCaseData(new OverhandOperation(new[] { 52, 0 })).SetName("Overhand zero chunk");
        yield return new TestCaseData(new DealOperation(4, 14)).SetName("Deal too many");
        yield return new TestCaseData(new DealOperation(0, 5)).SetName("Deal no players");
        yield return new TestCaseData(new MoveCardOperation(52, ZoneId.Pile(0))).SetName("Move bad card");
        yield return new TestCaseData(new MovePacketOperation(CardMove.TopToTop(ZoneId.Pile(9), 1, ZoneId.Deck))).SetName("Move from empty");
    }

    [TestCaseSource(nameof(ImpossibleCases))]
    public void ImpossibleOperations_Throw(GameOperation op)
    {
        var t = new CardTable();
        Assert.That(() => op.Execute(new TableTransaction(t)),
            Throws.InstanceOf<ArgumentException>().Or.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public void Shuffles_RefuseWhileHoldingCards()
    {
        var t = Run(new MovePacketOperation(CardMove.TopToTop(ZoneId.Deck, 2, ZoneId.LeftHand)));
        Assert.Throws<InvalidOperationException>(() =>
            new RiffleOperation(20, new[] { 1 }).Execute(new TableTransaction(t)));
    }
}
