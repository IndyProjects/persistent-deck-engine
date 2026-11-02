using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// Logic behind the hands-on controls: drop patterns, packet sizes, drop targets, and the card view's
/// preview → commit path that interactions rely on.
public class InteractionTests
{
    private static int[] Cards(CardTable t, ZoneId z) => new List<int>(t.Cards(z)).ToArray();

    // ---- Riffle drops ----

    [TestCase(new[] { true, false, true, false }, new[] { 1, 1, 1, 1 })]
    [TestCase(new[] { true, true, false, true }, new[] { 2, 1, 1 })]
    [TestCase(new[] { false, false, true }, new[] { 0, 2, 1 })]
    [TestCase(new bool[0], new[] { 0 })]
    public void PatternFromDrops_MergesRunsAndStartsLeft(bool[] drops, int[] expected)
        => CollectionAssert.AreEqual(expected, RiffleOperation.PatternFromDrops(drops));

    [TestCase(1), TestCase(2), TestCase(3)]
    public void SingleDrops_MatchTheRiffleOperation(int seed)
    {
        // Simulate a player dropping one card at a time in a random rhythm
        var rng = new System.Random(seed);
        int split = 20 + rng.Next(12);
        var t = new CardTable();
        t.Apply(CardMove.TopToTop(ZoneId.Deck, 52, ZoneId.RightHand));
        t.Apply(CardMove.TopToTop(ZoneId.RightHand, split, ZoneId.LeftHand));
        var drops = new List<bool>();
        while (t.Count(ZoneId.LeftHand) + t.Count(ZoneId.RightHand) > 0)
        {
            bool left = rng.Next(2) == 0;
            var hand = left ? ZoneId.LeftHand : ZoneId.RightHand;
            if (t.Count(hand) == 0) continue;
            t.Apply(CardMove.BottomToTop(hand, 1, ZoneId.Deck));
            drops.Add(left);
        }

        var pattern = RiffleOperation.PatternFromDrops(drops);
        CollectionAssert.AreEqual(DeckEngine.RiffleShuffle(DeckEngine.CreateOrderedDeck(), split, pattern), Cards(t, ZoneId.Deck));
    }

    // ---- Dealing rhythm ----

    [Test]
    public void PacketSize_IsClamped_AndTrimmedToWhatTheSeatNeeds()
    {
        var rules = new DealerRules(4, 5);
        var sm = new GameStateManager(rules);
        rules.PacketSize = 9;
        Assert.AreEqual(5, rules.PacketSize);
        rules.PacketSize = 0;
        Assert.AreEqual(1, rules.PacketSize);

        sm.Apply(new PickUpDeckOperation());
        rules.PacketSize = 3;
        Assert.IsTrue(sm.Apply(rules.NextPacket(sm.Table)).Success); // seat 1 → 3
        for (int i = 0; i < 3; i++) sm.Apply(rules.NextPacket(sm.Table)); // seats 2, 3, 0 → 3
        var packet = rules.NextPacket(sm.Table);
        Assert.AreEqual(2, packet.Count, "seat 1 only needs 2 more");
        Assert.IsTrue(sm.Apply(packet).Success);
    }

    // ---- Drop targets ----

    [Test]
    public void DropZones_SnapToNearbyPilesDiscardAndHand()
    {
        Assert.IsTrue(TableLayout.TryDropZone(TableLayout.PileAnchor(2) + new Vector3(0.03f, 0f, -0.02f), out var zone, out _));
        Assert.AreEqual(ZoneId.Pile(2), zone);

        Assert.IsTrue(TableLayout.TryDropZone(TableLayout.DiscardAnchorFor(0), out zone, out _));
        Assert.AreEqual(ZoneId.Discard(), zone);

        Assert.IsTrue(TableLayout.TryDropZone(new Vector3(0f, TableLayout.SurfaceY, -0.7f), out zone, out _));
        Assert.AreEqual(ZoneId.Hand(0), zone);

        Assert.IsFalse(TableLayout.TryDropZone(new Vector3(0f, TableLayout.SurfaceY, 0.8f), out _, out _), "far from any spot");
    }

    // ---- Preview → commit in the card view ----

    private static (GameStateManager sm, TableCardView view, GameObject go) HeldDeckView()
    {
        var sm = new GameStateManager(new DealerRules());
        sm.Apply(new PickUpDeckOperation());
        var go = new GameObject("Cards");
        var view = go.AddComponent<TableCardView>();
        view.Initialise(sm);
        return (sm, view, go);
    }

    [Test]
    public void PreviewedRiffle_CommitsAsOneMatchingAction()
    {
        var (sm, view, go) = HeldDeckView();
        try
        {
            int entriesBefore = sm.History.Entries.Count;
            view.BeginPreview();
            view.PreviewMove(CardMove.TopToTop(ZoneId.RightHand, 22, ZoneId.LeftHand));
            var drops = new List<bool>();
            bool left = false;
            while (view.Shown.Count(ZoneId.LeftHand) + view.Shown.Count(ZoneId.RightHand) > 0)
            {
                left = !left;
                var hand = left ? ZoneId.LeftHand : ZoneId.RightHand;
                if (view.Shown.Count(hand) == 0) continue;
                view.PreviewMove(CardMove.BottomToTop(hand, 1, ZoneId.Deck));
                drops.Add(left);
            }
            view.PreviewMove(CardMove.TopToTop(ZoneId.Deck, 52, ZoneId.RightHand));
            Assert.AreEqual(52, sm.Table.Count(ZoneId.RightHand), "nothing applied to the game during the preview");

            view.EndPreview(committed: true);
            var result = sm.Apply(new RiffleOperation(22, RiffleOperation.PatternFromDrops(drops), ZoneId.RightHand));
            Assert.IsTrue(result.Success);
            Assert.IsFalse(view.IsPreviewing);
            Assert.IsTrue(view.Shown.SameAs(sm.Table), "view kept the previewed result");
            Assert.AreEqual(entriesBefore + 1, sm.History.Entries.Count, "recorded as one action");
            Assert.AreEqual(ActionKind.Riffle, sm.History.Entries[entriesBefore].Kind);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void CancelledPreview_LeavesTheGameUntouched()
    {
        var (sm, view, go) = HeldDeckView();
        try
        {
            var before = sm.Table.Clone();
            view.BeginPreview();
            view.PreviewMove(CardMove.TopToTop(ZoneId.RightHand, 5, ZoneId.LeftHand));
            view.EndPreview(committed: false);
            Assert.IsFalse(view.IsPreviewing);
            Assert.IsTrue(view.Shown.SameAs(before));
            Assert.IsTrue(sm.Table.SameAs(before));
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void Replay_ShowsThePast_ThenReturnsToThePresent()
    {
        var (sm, view, go) = HeldDeckView();
        try
        {
            sm.Apply(new OverhandOperation(new[] { 10, 20, 22 }, ZoneId.RightHand));
            int index = sm.History.LastShuffleIndex();
            view.PlayReplay(sm.History.StateBefore(index), sm.History.Entries[index].Moves);
            Assert.IsTrue(view.IsReplaying);
            Assert.IsTrue(view.Shown.SameAs(sm.Table), "after replaying the last action the picture matches the present");

            view.EndReplay();
            Assert.IsFalse(view.IsReplaying);
            Assert.IsTrue(view.Shown.SameAs(sm.Table));
        }
        finally { Object.DestroyImmediate(go); }
    }
}
