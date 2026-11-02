using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// How the riffle and cut are shown: woven packets pushed together, and a section seen moving to the top.
/// Whatever the presentation does, the game must end up exactly as the shuffle engine says.
public class ShuffleMotionTests
{
    private GameStateManager _sm;
    private TableCardView    _view;
    private SeatedCameraRig  _rig;
    private readonly List<GameObject> _objects = new List<GameObject>();

    [SetUp]
    public void HoldTheDeck()
    {
        _sm = new GameStateManager(new DealerRules());
        _sm.Apply(new PickUpDeckOperation());

        var cards = Track(new GameObject("Cards"));
        _view = cards.AddComponent<TableCardView>();
        _view.Initialise(_sm);
        _rig = Track(new GameObject("Camera", typeof(Camera))).AddComponent<SeatedCameraRig>();
    }

    [TearDown]
    public void CleanUp()
    {
        foreach (var go in _objects) Object.DestroyImmediate(go);
        _objects.Clear();
    }

    private GameObject Track(GameObject go)
    {
        _objects.Add(go);
        return go;
    }

    private static int[] Cards(CardTable t, ZoneId z) => new List<int>(t.Cards(z)).ToArray();
    private Transform CardObject(int card) => _view.transform.GetChild(card);

    // ---- Riffle ----

    private static CardTable MidRiffle(RifflePresentation presentation, out int fromLeft, out int fromRight)
    {
        var table = new CardTable();
        table.Apply(CardMove.TopToTop(ZoneId.Deck, 52, ZoneId.RightHand));
        table.Apply(CardMove.TopToTop(ZoneId.RightHand, 26, ZoneId.LeftHand));
        foreach (var hand in new[] { ZoneId.LeftHand, ZoneId.RightHand })
        {
            var drop = table.Apply(CardMove.BottomToTop(hand, 1, ZoneId.Deck));
            presentation.OnMove(drop, table);
        }
        fromRight = table.Cards(ZoneId.Deck)[0];
        fromLeft  = table.Cards(ZoneId.Deck)[1];
        return table;
    }

    [Test]
    public void Riffle_CardsFromEachHandOverlap_SoTheHalvesAreWoven()
    {
        var presentation = new RifflePresentation();
        var table = MidRiffle(presentation, out int fromLeft, out int fromRight);

        Assert.IsTrue(presentation.TryPose(table, fromLeft, out var left));
        Assert.IsTrue(presentation.TryPose(table, fromRight, out var right));
        Assert.Less(left.Position.x, RifflePresentation.Spot.x);
        Assert.Greater(right.Position.x, RifflePresentation.Spot.x);

        // Inner ends reach past each other, and each layer sits on the one before
        float gap = right.Position.x - left.Position.x;
        Assert.Less(gap, CardMeshBuilder.Length, "the two cards overlap along their length");
        Assert.Greater(gap, CardMeshBuilder.Length * 0.5f, "but only at their ends");
        Assert.Greater(right.Position.y, left.Position.y);
    }

    [Test]
    public void Riffle_Pushing_SquaresTheHalvesIntoOneDeck()
    {
        var presentation = new RifflePresentation();
        var table = MidRiffle(presentation, out int fromLeft, out int fromRight);
        presentation.Pushed = true;

        presentation.TryPose(table, fromLeft, out var left);
        presentation.TryPose(table, fromRight, out var right);
        Assert.AreEqual(left.Position.x, right.Position.x, 1e-5f);
        Assert.AreEqual(CardMeshBuilder.Thickness, right.Position.y - left.Position.y, 1e-5f, "touching, one card thick");
        Assert.AreEqual(0f, Quaternion.Angle(left.Rotation, right.Rotation), 0.01f, "no card left askew");
    }

    [Test]
    public void Riffle_HeldPackets_DontShiftAsTheBottomCardFalls()
    {
        var presentation = new RifflePresentation();
        var table = new CardTable();
        table.Apply(CardMove.TopToTop(ZoneId.Deck, 52, ZoneId.RightHand));
        table.Apply(CardMove.TopToTop(ZoneId.RightHand, 26, ZoneId.LeftHand));
        int top = table.Cards(ZoneId.LeftHand)[0];
        presentation.TryPose(table, top, out var before);

        presentation.OnMove(table.Apply(CardMove.BottomToTop(ZoneId.LeftHand, 1, ZoneId.Deck)), table);
        presentation.TryPose(table, top, out var after);
        Assert.IsTrue(before.Approximately(after));
    }

    [Test]
    public void Riffle_InteractionMatchesTheEngine_AndRecordsOneAction()
    {
        var before = Cards(_sm.Table, ZoneId.RightHand);
        int entries = _sm.History.Entries.Count;
        var riffle = Track(new GameObject("Riffle")).AddComponent<RiffleInteraction>();
        Assert.IsTrue(riffle.Begin(_sm, _view, _rig));

        riffle.PreviewDrops(26, 52);
        Assert.AreEqual(52, _sm.Table.Count(ZoneId.RightHand), "the game hasn't changed during the riffle");
        Assert.IsNotNull(_view.Poser, "still woven until pushed");
        riffle.FinishPush();

        var pattern = new int[52];
        for (int i = 0; i < 52; i++) pattern[i] = 1;
        CollectionAssert.AreEqual(DeckEngine.RiffleShuffle(before, 26, pattern), Cards(_sm.Table, ZoneId.RightHand));
        Assert.IsTrue(_view.Shown.SameAs(_sm.Table));
        Assert.IsNull(_view.Poser, "back to the ordinary layout");
        Assert.AreEqual(1f, _view.TravelScale);
        Assert.IsFalse(riffle.IsActive);
        Assert.AreEqual(entries + 1, _sm.History.Entries.Count, "one riffle in the history");
    }

    [Test]
    public void Riffle_CancelledMidDrop_LeavesTheGameAlone()
    {
        var before = _sm.Table.Clone();
        var riffle = Track(new GameObject("Riffle")).AddComponent<RiffleInteraction>();
        riffle.Begin(_sm, _view, _rig);
        riffle.PreviewDrops(26, 10);
        riffle.Cancel();

        Assert.IsTrue(_sm.Table.SameAs(before));
        Assert.IsTrue(_view.Shown.SameAs(before));
        Assert.IsNull(_view.Poser);
    }

    [Test]
    public void Replay_OfARiffle_ReturnsToThePresent()
    {
        _sm.Apply(new RiffleOperation(26, new[] { 2, 1 }, ZoneId.RightHand));
        int index = _sm.History.LastShuffleIndex();
        _view.PlayReplay(_sm.History.StateBefore(index), _sm.History.Entries[index].Moves, isRiffle: true);
        Assert.IsTrue(_view.Shown.SameAs(_sm.Table));
        Assert.IsNull(_view.Poser);
        _view.EndReplay();
        Assert.IsTrue(_view.Shown.SameAs(_sm.Table));
    }

    // ---- Cut ----

    [Test]
    public void Cut_GameChangesOnlyWhenTheMotionEnds_AsOneCorrectCut()
    {
        var before = Cards(_sm.Table, ZoneId.RightHand);
        int entries = _sm.History.Entries.Count;
        var cut = Track(new GameObject("Cut")).AddComponent<CutInteraction>();
        Assert.IsTrue(cut.Begin(_sm, _view, _rig));

        cut.StartCut(26, 26);
        Assert.IsTrue(cut.IsMoving);
        CollectionAssert.AreEqual(before, Cards(_sm.Table, ZoneId.RightHand), "nothing recorded mid-motion");

        cut.FinishCut();
        CollectionAssert.AreEqual(DeckEngine.CutDeck(before, 26), Cards(_sm.Table, ZoneId.RightHand));
        Assert.IsTrue(_view.Shown.SameAs(_sm.Table));
        Assert.IsFalse(cut.IsActive);
        Assert.AreEqual(entries + 1, _sm.History.Entries.Count);
    }

    [Test]
    public void Cut_SectionVisiblyLeavesTheDeck_AndPassesOverTheTop()
    {
        var bottomCard = Cards(_sm.Table, ZoneId.RightHand)[51];
        var topCard    = Cards(_sm.Table, ZoneId.RightHand)[0];
        var cut = Track(new GameObject("Cut")).AddComponent<CutInteraction>();
        cut.Begin(_sm, _view, _rig);
        _view.SnapAll();
        TableLayout.TryInspectFrame(ZoneId.RightHand, out var anchor, out var frame);

        cut.StartCut(26, 26);
        float start = Time.time;

        // Slid clear along its length (close 0.28 + slide 0.28)
        _view.AnimateTo(start + 0.56f + 0.001f);
        float along = Vector3.Dot(CardObject(bottomCard).position - anchor, frame * Vector3.forward);
        Assert.Greater(along, CardMeshBuilder.Length, "the section has cleared the deck");

        // Lifted above the rest, which has dropped (+ drop 0.2 + lift 0.24), then carried back across (+ 0.28)
        _view.AnimateTo(start + 1.0f + 0.001f);
        Assert.Greater(CardObject(bottomCard).position.y, CardObject(topCard).position.y, "the section is above the rest");
        _view.AnimateTo(start + 1.28f + 0.001f);
        along = Vector3.Dot(CardObject(bottomCard).position - anchor, frame * Vector3.forward);
        Assert.AreEqual(0f, along, 1e-3f, "back over the deck");
        Assert.Greater(CardObject(bottomCard).position.y, CardObject(topCard).position.y);

        cut.FinishCut();
    }

    [Test]
    public void Cut_CancelledMidMotion_LeavesTheGameAlone()
    {
        var before = _sm.Table.Clone();
        var cut = Track(new GameObject("Cut")).AddComponent<CutInteraction>();
        cut.Begin(_sm, _view, _rig);
        cut.StartCut(10, 42);
        cut.Cancel();

        Assert.IsFalse(cut.IsMoving);
        Assert.IsTrue(_sm.Table.SameAs(before));
        Assert.IsTrue(_view.Shown.SameAs(before));
    }

    [Test]
    public void Cut_APulledMiddleBlock_AlsoMatchesTheRules()
    {
        var before = Cards(_sm.Table, ZoneId.RightHand);
        var cut = Track(new GameObject("Cut")).AddComponent<CutInteraction>();
        cut.Begin(_sm, _view, _rig);
        cut.StartCut(10, 5);
        cut.FinishCut();
        var after = Cards(_sm.Table, ZoneId.RightHand);
        CollectionAssert.AreEqual(new[] { before[10], before[11], before[12], before[13], before[14], before[0] },
                                  new[] { after[0], after[1], after[2], after[3], after[4], after[5] });
        Assert.IsTrue(_view.Shown.SameAs(_sm.Table));
    }
}
