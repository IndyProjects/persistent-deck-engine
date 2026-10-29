using System;
using System.Collections.Generic;
using NUnit.Framework;

public class CardTableTests
{
    private static int[] Cards(CardTable t, ZoneId z) => new List<int>(t.Cards(z)).ToArray();

    [Test]
    public void NewTable_HasOrderedFaceDownDeck()
    {
        var t = new CardTable();
        CollectionAssert.AreEqual(DeckEngine.CreateOrderedDeck(), Cards(t, ZoneId.Deck));
        for (int c = 0; c < 52; c++)
        {
            Assert.IsFalse(t.IsFaceUp(c));
            Assert.AreEqual(ZoneId.Deck, t.LocationOf(c));
            Assert.AreEqual(c, t.IndexOf(c));
        }
        Assert.IsTrue(t.Validate());
        CollectionAssert.AreEqual(new[] { ZoneId.Deck }, t.NonEmptyZones());
    }

    [Test]
    public void Move_TopToTop_KeepsPacketOrder()
    {
        var t = new CardTable();
        t.Apply(CardMove.TopToTop(ZoneId.Deck, 3, ZoneId.Pile(0)));
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, Cards(t, ZoneId.Pile(0)));
        Assert.AreEqual(3, t.Cards(ZoneId.Deck)[0]);
        Assert.AreEqual(ZoneId.Pile(0), t.LocationOf(1));
        Assert.IsTrue(t.Validate());
    }

    [Test]
    public void Move_BottomSentinels_ResolveToConcreteIndices()
    {
        var t = new CardTable();
        var resolved = t.Apply(CardMove.BottomToTop(ZoneId.Deck, 2, ZoneId.Pile(0)));
        Assert.AreEqual(50, resolved.FromIndex);
        Assert.AreEqual(0, resolved.ToIndex);
        CollectionAssert.AreEqual(new[] { 50, 51 }, Cards(t, ZoneId.Pile(0)));

        resolved = t.Apply(CardMove.TopToBottom(ZoneId.Deck, 1, ZoneId.Pile(0)));
        Assert.AreEqual(2, resolved.ToIndex);
        CollectionAssert.AreEqual(new[] { 50, 51, 0 }, Cards(t, ZoneId.Pile(0)));
    }

    [Test]
    public void Move_WithinSameZone_IndexMeasuredAfterRemoval()
    {
        var t = new CardTable();
        t.Apply(CardMove.TopToBottom(ZoneId.Deck, 2, ZoneId.Deck)); // a cut of 2
        Assert.AreEqual(2, t.Cards(ZoneId.Deck)[0]);
        Assert.AreEqual(0, t.Cards(ZoneId.Deck)[50]);
        Assert.AreEqual(1, t.Cards(ZoneId.Deck)[51]);
        Assert.IsTrue(t.Validate());
    }

    [Test]
    public void Flip_ReversesPacketAndTogglesFaces()
    {
        var t = new CardTable();
        t.Apply(CardMove.TopToTop(ZoneId.Deck, 3, ZoneId.Pile(0), flip: true));
        CollectionAssert.AreEqual(new[] { 2, 1, 0 }, Cards(t, ZoneId.Pile(0)));
        Assert.IsTrue(t.IsFaceUp(0) && t.IsFaceUp(1) && t.IsFaceUp(2));
        Assert.IsFalse(t.IsFaceUp(3));
    }

    [Test]
    public void FlipInPlace_TurnsOneCardWithoutMovingIt()
    {
        var t = new CardTable();
        t.Apply(CardMove.FlipInPlace(ZoneId.Deck, 5));
        Assert.IsTrue(t.IsFaceUp(5));
        Assert.AreEqual(5, t.IndexOf(5));
        Assert.AreEqual(52, t.Count(ZoneId.Deck));
    }

    [Test]
    public void FlipInPlace_OnSingleCardZone_Works()
    {
        var t = new CardTable();
        t.Apply(CardMove.TopToTop(ZoneId.Deck, 1, ZoneId.Discard()));
        t.Apply(CardMove.FlipInPlace(ZoneId.Discard(), 0));
        Assert.IsTrue(t.IsFaceUp(0));
        Assert.AreEqual(ZoneId.Discard(), t.LocationOf(0));
        Assert.IsTrue(t.Validate());
    }

    [Test]
    public void EmptiedZones_ArePruned()
    {
        var t = new CardTable();
        t.Apply(CardMove.TopToTop(ZoneId.Deck, 52, ZoneId.LeftHand));
        Assert.AreEqual(0, t.Count(ZoneId.Deck));
        CollectionAssert.AreEqual(new[] { ZoneId.LeftHand }, t.NonEmptyZones());
        Assert.IsTrue(t.Validate());
    }

    private static readonly CardMove[] InvalidMoveCases =
    {
        new CardMove(ZoneId.Deck, 0, 0, ZoneId.Pile(0), 0),     // count < 1
        new CardMove(ZoneId.Deck, 0, 53, ZoneId.Pile(0), 0),    // more than the zone holds
        new CardMove(ZoneId.Deck, 51, 2, ZoneId.Pile(0), 0),    // range runs past the end
        new CardMove(ZoneId.Deck, 0, 1, ZoneId.Pile(0), 1),     // insert beyond an empty destination
        new CardMove(ZoneId.Deck, 0, 1, ZoneId.Deck, 52),       // same zone: only 51 remain after removal
        new CardMove(ZoneId.Pile(3), 0, 1, ZoneId.Deck, 0),     // empty source
    };

    [TestCaseSource(nameof(InvalidMoveCases))]
    public void InvalidMoves_ThrowAndLeaveTableUntouched(CardMove move)
    {
        var t = new CardTable();
        Assert.Throws<ArgumentException>(() => t.Apply(move));
        CollectionAssert.AreEqual(DeckEngine.CreateOrderedDeck(), Cards(t, ZoneId.Deck));
        Assert.IsTrue(t.Validate());
    }

    [Test]
    public void Inverse_ExactlyUndoesAnyResolvedMove()
    {
        var moves = new[]
        {
            new CardMove(ZoneId.Deck, 10, 5, ZoneId.Hand(0), 0),
            new CardMove(ZoneId.Deck, 3, 4, ZoneId.Deck, 20, flip: true),
            new CardMove(ZoneId.Deck, CardMove.Bottom, 7, ZoneId.Hand(0), CardMove.Bottom, flip: true),
            CardMove.FlipInPlace(ZoneId.Hand(0), 2),
        };
        var t = new CardTable();
        var applied = new List<CardMove>();
        foreach (var m in moves) applied.Add(t.Apply(m));

        for (int i = applied.Count - 1; i >= 0; i--) t.Apply(applied[i].Inverse());

        CollectionAssert.AreEqual(DeckEngine.CreateOrderedDeck(), Cards(t, ZoneId.Deck));
        for (int c = 0; c < 52; c++) Assert.IsFalse(t.IsFaceUp(c));
        Assert.IsTrue(t.Validate());
    }

    [Test]
    public void Transaction_Rollback_RestoresTable()
    {
        var t  = new CardTable();
        var tx = new TableTransaction(t);
        tx.Move(CardMove.TopToTop(ZoneId.Deck, 5, ZoneId.Hand(1), flip: true));
        tx.Move(CardMove.TopToBottom(ZoneId.Deck, 10, ZoneId.Deck));
        tx.Rollback();
        CollectionAssert.AreEqual(DeckEngine.CreateOrderedDeck(), Cards(t, ZoneId.Deck));
        Assert.IsFalse(t.IsFaceUp(0));
        Assert.AreEqual(0, tx.Applied.Count);
    }

    [Test]
    public void Clone_IsIndependent()
    {
        var t = new CardTable();
        var c = t.Clone();
        c.Apply(CardMove.TopToTop(ZoneId.Deck, 1, ZoneId.Pile(0), flip: true));
        Assert.AreEqual(52, t.Count(ZoneId.Deck));
        Assert.IsFalse(t.IsFaceUp(0));
        Assert.IsTrue(c.IsFaceUp(0));
    }

    [Test]
    public void FromZones_RejectsMissingOrDuplicateCards()
    {
        var missing = new[] { new KeyValuePair<ZoneId, int[]>(ZoneId.Deck, new[] { 0, 1, 2 }) };
        Assert.Throws<ArgumentException>(() => CardTable.FromZones(missing, null));

        var deck = DeckEngine.CreateOrderedDeck();
        var dupe = new[]
        {
            new KeyValuePair<ZoneId, int[]>(ZoneId.Deck, deck),
            new KeyValuePair<ZoneId, int[]>(ZoneId.Pile(0), new[] { 5 }),
        };
        Assert.Throws<ArgumentException>(() => CardTable.FromZones(dupe, null));
    }

    [Test]
    public void HandCount_IsHighestOccupiedSeatPlusOne()
    {
        var t = new CardTable();
        Assert.AreEqual(0, t.HandCount());
        t.Apply(CardMove.TopToTop(ZoneId.Deck, 1, ZoneId.Hand(2)));
        Assert.AreEqual(3, t.HandCount());
    }

    [Test]
    public void ZoneId_RejectsNegativeIndex_AndSortsCanonically()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ZoneId(ZoneKind.Pile, -1));
        var zones = new List<ZoneId> { ZoneId.LeftHand, ZoneId.Pile(0), ZoneId.Hand(1), ZoneId.Deck, ZoneId.Hand(0) };
        zones.Sort();
        CollectionAssert.AreEqual(new[] { ZoneId.Deck, ZoneId.Hand(0), ZoneId.Hand(1), ZoneId.Pile(0), ZoneId.LeftHand }, zones);
    }
}
