using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// The player-chosen cut: section-to-top logic, rules, and picking a card layer with the mouse.
public class CutTests
{
    private static int[] Cards(CardTable t, ZoneId z) => new List<int>(t.Cards(z)).ToArray();

    private static CardTable Held()
    {
        var t = new CardTable();
        t.Apply(CardMove.TopToTop(ZoneId.Deck, 52, ZoneId.RightHand));
        return t;
    }

    [TestCase(1), TestCase(17), TestCase(51)]
    public void SectionToBottom_IsAnOrdinaryCut(int start)
    {
        var t = Held();
        new CutSectionOperation(start, 52 - start, ZoneId.RightHand).Execute(new TableTransaction(t));
        CollectionAssert.AreEqual(DeckEngine.CutDeck(DeckEngine.CreateOrderedDeck(), start), Cards(t, ZoneId.RightHand));
    }

    [Test]
    public void MiddleSection_GoesOnTop_KeepingItsOrder()
    {
        var t = new CardTable();
        new CutSectionOperation(10, 5, ZoneId.Deck).Execute(new TableTransaction(t));
        var deck = Cards(t, ZoneId.Deck);
        CollectionAssert.AreEqual(new[] { 10, 11, 12, 13, 14, 0, 1 }, new[] { deck[0], deck[1], deck[2], deck[3], deck[4], deck[5], deck[6] });
        Assert.AreEqual(15, deck[15]);
        Assert.IsTrue(t.Validate());
    }

    [TestCase(0, 10, TestName = "Section already on top")]
    [TestCase(5, 0,  TestName = "Empty section")]
    [TestCase(40, 13, TestName = "Runs past the bottom")]
    public void InvalidSections_Throw(int start, int count)
    {
        var t = new CardTable();
        Assert.Throws<System.ArgumentOutOfRangeException>(() =>
            new CutSectionOperation(start, count, ZoneId.Deck).Execute(new TableTransaction(t)));
    }

    [Test]
    public void DealerRules_AllowSectionCutsOnlyWhileShufflingInHand()
    {
        var rules = new DealerRules();
        var sm = new GameStateManager(rules);
        Assert.IsFalse(sm.Apply(new CutSectionOperation(20, 32, ZoneId.Deck)).Success, "deck still on the table");

        sm.Apply(new PickUpDeckOperation());
        Assert.IsTrue(sm.Apply(new CutSectionOperation(20, 32, ZoneId.RightHand)).Success);
        Assert.IsTrue(sm.Apply(new CutSectionOperation(5, 3, ZoneId.RightHand)).Success, "pulled cut from the middle");

        sm.Apply(rules.NextPacket(1));
        Assert.IsFalse(sm.Apply(new CutSectionOperation(5, 3, ZoneId.RightHand)).Success, "locked once dealing starts");
    }

    // ---- Picking a layer with a ray ----

    private const float Spacing = 0.004f;

    private static Ray Horizontal(Vector3 anchor, float height)
        => new Ray(anchor + new Vector3(0.3f, height, 0f), Vector3.left);

    [Test]
    public void LayerAt_MapsHeightToCard_TopIsIndexZero()
    {
        var anchor = new Vector3(0.1f, 0.8f, -0.5f);
        float half = CardMeshBuilder.Thickness * 0.5f;

        Assert.AreEqual(51, TableLayout.LayerAt(Horizontal(anchor, half), anchor, 52, Spacing), "bottom card");
        Assert.AreEqual(0,  TableLayout.LayerAt(Horizontal(anchor, half + 51 * Spacing), anchor, 52, Spacing), "top card");
        Assert.AreEqual(26, TableLayout.LayerAt(Horizontal(anchor, half + 25 * Spacing), anchor, 52, Spacing));
    }

    [Test]
    public void LayerAt_WorksFromAnAngle_AndClampsOutsideTheStack()
    {
        var anchor = new Vector3(0f, 0.8f, 0f);
        // Looking down at 30° towards the point 10 cards up from the bottom
        var target = anchor + Vector3.up * (CardMeshBuilder.Thickness * 0.5f + 10 * Spacing);
        var eye = target + new Vector3(0.3f, 0.3f * Mathf.Tan(30f * Mathf.Deg2Rad), 0.1f);
        Assert.AreEqual(41, TableLayout.LayerAt(new Ray(eye, target - eye), anchor, 52, Spacing));

        Assert.AreEqual(0,  TableLayout.LayerAt(Horizontal(anchor, 5f), anchor, 52, Spacing), "above the stack");
        Assert.AreEqual(51, TableLayout.LayerAt(Horizontal(anchor, -5f), anchor, 52, Spacing), "below the stack");
        Assert.AreEqual(-1, TableLayout.LayerAt(Horizontal(anchor, 0f), anchor, 0, Spacing), "empty");
    }

    [Test]
    public void InspectPose_SpreadsCards_AndSlidesTheSelection()
    {
        var t = Held();
        TableLayout.TryInspectFrame(ZoneId.RightHand, out var anchor, out var frame);
        var top    = TableLayout.InspectPose(t, t.Cards(ZoneId.RightHand)[0],  Spacing, 0f);
        var bottom = TableLayout.InspectPose(t, t.Cards(ZoneId.RightHand)[51], Spacing, 0f);
        Assert.AreEqual(51 * Spacing, top.Position.y - bottom.Position.y, 1e-5f);

        var slid = TableLayout.InspectPose(t, t.Cards(ZoneId.RightHand)[0], Spacing, 0.04f);
        Assert.AreEqual(0.04f, Vector3.Dot(slid.Position - top.Position, frame * Vector3.forward), 1e-5f);
    }

    [Test]
    public void CardView_InspectAndSelect_MoveCardsToSpreadPoses()
    {
        var sm = new GameStateManager();
        sm.Apply(new PickUpDeckOperation());
        var go = new GameObject("Cards");
        try
        {
            var view = go.AddComponent<TableCardView>();
            view.Initialise(sm);
            view.BeginInspect(ZoneId.RightHand, Spacing, 0.04f);
            view.Select(30, 22);
            view.SnapAll();

            int topCard = sm.Table.Cards(ZoneId.RightHand)[0];
            int selCard = sm.Table.Cards(ZoneId.RightHand)[30];
            var expectedTop = TableLayout.InspectPose(sm.Table, topCard, Spacing, 0f);
            var expectedSel = TableLayout.InspectPose(sm.Table, selCard, Spacing, 0.04f);
            Assert.That(Vector3.Distance(expectedTop.Position, go.transform.GetChild(topCard).position), Is.LessThan(1e-4f));
            Assert.That(Vector3.Distance(expectedSel.Position, go.transform.GetChild(selCard).position), Is.LessThan(1e-4f));

            view.EndInspect();
            view.SnapAll();
            Assert.IsFalse(view.IsInspecting);
            var squared = view.Layout.Pose(sm.Table, topCard);
            Assert.That(Vector3.Distance(squared.Position, go.transform.GetChild(topCard).position), Is.LessThan(1e-4f));
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }
}
