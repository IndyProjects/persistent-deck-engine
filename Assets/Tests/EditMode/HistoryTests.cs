using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// The deck's memory (DeckHistory), mix measurements, and how the manager records and persists them.
public class HistoryTests
{
    private static int[] Cards(CardTable t, ZoneId z) => new List<int>(t.Cards(z)).ToArray();

    // ---- Mix measurements ----

    [Test]
    public void Metrics_FreshDeck_CutDeck_AndOneRiffle()
    {
        var fresh = DeckEngine.CreateOrderedDeck();
        Assert.AreEqual(1,  DeckMetrics.RisingSequences(fresh));
        Assert.AreEqual(51, DeckMetrics.AdjacentPairs(fresh));

        var cut = DeckEngine.CutDeck(fresh, 26);
        Assert.AreEqual(2,  DeckMetrics.RisingSequences(cut));
        Assert.AreEqual(50, DeckMetrics.AdjacentPairs(cut), "a cut breaks only one neighbour pair");

        var riffled = DeckEngine.RiffleShuffle(fresh, 26, new[] { 1 });
        Assert.AreEqual(2, DeckMetrics.RisingSequences(riffled), "one riffle makes at most two rising sequences");
        Assert.AreEqual(0, DeckMetrics.AdjacentPairs(riffled));

        Assert.Throws<System.ArgumentException>(() => DeckMetrics.RisingSequences(new[] { 0, 1 }));
    }

    [Test]
    public void Metrics_RisingSequencesGrowWithRiffles()
    {
        var deck = DeckEngine.CreateOrderedDeck();
        var rng = new System.Random(1);
        int previous = 1;
        for (int r = 0; r < 4; r++)
        {
            var pattern = new int[52];
            for (int i = 0; i < pattern.Length; i++) pattern[i] = rng.Next(1, 4);
            deck = DeckEngine.RiffleShuffle(deck, 24 + rng.Next(5), pattern);
            int now = DeckMetrics.RisingSequences(deck);
            Assert.That(now, Is.LessThanOrEqualTo(previous * 2), "a riffle at most doubles rising sequences");
            previous = now;
        }
        Assert.That(previous, Is.GreaterThan(4));
    }

    // ---- CardTable equality ----

    [Test]
    public void SameAs_ComparesZonesOrderAndFaces()
    {
        var a = new CardTable();
        var b = new CardTable();
        Assert.IsTrue(a.SameAs(b));

        b.Apply(CardMove.FlipInPlace(ZoneId.Deck, 3));
        Assert.IsFalse(a.SameAs(b), "face differs");
        b.Apply(CardMove.FlipInPlace(ZoneId.Deck, 3));
        Assert.IsTrue(a.SameAs(b));

        b.Apply(CardMove.TopToBottom(ZoneId.Deck, 1, ZoneId.Deck));
        Assert.IsFalse(a.SameAs(b), "order differs");
        Assert.IsFalse(a.SameAs(null));
    }

    // ---- DeckHistory ----

    private static GameStateManager PlayedManager()
    {
        var sm = new GameStateManager();
        sm.Apply(new PickUpDeckOperation());
        sm.Apply(new RiffleOperation(26, new[] { 2, 1, 3 }, ZoneId.RightHand));
        sm.Apply(new CutOperation(10, ZoneId.RightHand));
        sm.Apply(new DealPacketOperation(1, 2, ZoneId.RightHand));
        sm.Apply(new MoveCardOperation(sm.Table.Cards(ZoneId.Hand(1))[0], ZoneId.Pile(0), flip: true));
        return sm;
    }

    [Test]
    public void Manager_RecordsEveryAction_WithKinds()
    {
        var sm = PlayedManager();
        var e = sm.History.Entries;
        Assert.AreEqual(5, e.Count);
        CollectionAssert.AreEqual(
            new[] { ActionKind.Handling, ActionKind.Riffle, ActionKind.Cut, ActionKind.Deal, ActionKind.Play },
            new[] { e[0].Kind, e[1].Kind, e[2].Kind, e[3].Kind, e[4].Kind });
        Assert.AreEqual(2, sm.History.LastShuffleIndex());
        Assert.AreEqual(1, sm.History.CountSinceCollect(ActionKind.Riffle));
    }

    [Test]
    public void StateBefore_RebuildsAnyPointInTime()
    {
        var sm = PlayedManager();
        Assert.IsTrue(sm.History.StateBefore(0).SameAs(new CardTable()));
        Assert.IsTrue(sm.History.StateBefore(sm.History.Entries.Count).SameAs(sm.Table));

        var beforeCut = sm.History.StateBefore(2);
        Assert.AreEqual(52, beforeCut.Count(ZoneId.RightHand));
        var expected = DeckEngine.RiffleShuffle(DeckEngine.CreateOrderedDeck(), 26, new[] { 2, 1, 3 });
        CollectionAssert.AreEqual(expected, Cards(beforeCut, ZoneId.RightHand));
    }

    [Test]
    public void Undo_IsLoggedAsItsOwnEntry()
    {
        var sm = PlayedManager();
        sm.Undo();
        var last = sm.History.Entries[sm.History.Entries.Count - 1];
        Assert.AreEqual(ActionKind.Undo, last.Kind);
        StringAssert.StartsWith("Undo", last.Name);
        Assert.IsTrue(sm.History.StateBefore(sm.History.Entries.Count).SameAs(sm.Table));
    }

    [Test]
    public void JourneyOf_FollowsOneCardThroughShufflesDealsAndPlays()
    {
        var sm = PlayedManager();
        int played = sm.Table.Cards(ZoneId.Pile(0))[0];
        var journey = sm.History.JourneyOf(played);

        var last = journey[journey.Count - 1];
        Assert.AreEqual(ZoneId.Hand(1), last.From);
        Assert.AreEqual(ZoneId.Pile(0), last.To);
        Assert.IsTrue(last.FaceUpAfter && !last.FaceUpBefore);
        Assert.AreEqual(ZoneId.Deck, journey[0].From, "it started in the deck");
        Assert.AreEqual(0, journey[0].EntryIndex, "first moved when the deck was picked up");

        var text = HistoryPanel.Describe(sm, played);
        StringAssert.Contains(CardNames.Of(played), text);
        StringAssert.Contains("table pile 1", text);
    }

    [Test]
    public void History_TrimsOldEntries_KeepingReplayExact()
    {
        var sm = new GameStateManager();
        sm.Apply(new PickUpDeckOperation());
        for (int i = 0; i < DeckHistory.MaxEntries + 20; i++)
            sm.Apply(new CutOperation(1 + i % 50, ZoneId.RightHand));

        Assert.AreEqual(DeckHistory.MaxEntries, sm.History.Entries.Count);
        Assert.IsTrue(sm.History.StateBefore(sm.History.Entries.Count).SameAs(sm.Table));
    }

    [Test]
    public void History_SurvivesSaveAndLoad()
    {
        var sm = PlayedManager();
        string json = JsonUtility.ToJson(sm.CreateSaveData());

        var loaded = new GameStateManager();
        loaded.Load(JsonUtility.FromJson<TableSaveData>(json));
        Assert.AreEqual(sm.History.Entries.Count, loaded.History.Entries.Count);
        Assert.AreEqual(sm.History.Entries[1].Name, loaded.History.Entries[1].Name);
        Assert.AreEqual(ActionKind.Riffle, loaded.History.Entries[1].Kind);

        int card = loaded.Table.Cards(ZoneId.Pile(0))[0];
        Assert.AreEqual(sm.History.JourneyOf(card).Count, loaded.History.JourneyOf(card).Count);
    }

    [Test]
    public void History_ThatDoesntMatchTheTable_IsDropped()
    {
        var sm = PlayedManager();
        var data = sm.CreateSaveData();
        data.history.entries[1].moves[3] = 7; // corrupt a move count

        var loaded = new GameStateManager();
        UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Saved history ignored"));
        loaded.Load(data);
        Assert.IsTrue(loaded.Table.SameAs(sm.Table), "the table itself still loads");
        Assert.AreEqual(0, loaded.History.Entries.Count, "the log restarts from the loaded table");
    }

    [Test]
    public void Reset_StartsAFreshLog()
    {
        var sm = PlayedManager();
        sm.Reset();
        Assert.AreEqual(0, sm.History.Entries.Count);
        StringAssert.Contains("Nothing yet", HistoryPanel.Describe(sm, -1));
    }
}
