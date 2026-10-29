using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class GameStateManagerTests
{
    private static int[] Cards(CardTable t, ZoneId z) => new List<int>(t.Cards(z)).ToArray();

    // ---- Classic rules: the original two-phase flow ----

    [Test]
    public void Classic_InitialState()
    {
        var mgr = new GameStateManager(new ClassicRules());
        Assert.AreEqual(GamePhase.Idle, mgr.Phase);
        Assert.AreEqual(0, mgr.RoundNumber);
        Assert.IsTrue(DeckEngine.ValidateDeck(mgr.CurrentDeck));
        Assert.AreEqual(0, mgr.CurrentHands.Length);
        Assert.IsFalse(mgr.CanUndo);
    }

    [Test]
    public void Classic_ShuffleAndDealOnlyWhileIdle_CollectOnlyInPlay()
    {
        var mgr = new GameStateManager(new ClassicRules());
        Assert.IsFalse(mgr.Apply(CollectOperation.All()).Success, "collect blocked while idle");
        Assert.IsFalse(mgr.Apply(new FlipCardOperation(0)).Success, "play blocked while idle");

        Assert.IsTrue(mgr.Apply(new CutOperation(26)).Success);
        Assert.AreEqual(26, mgr.CurrentDeck[0]);

        Assert.IsTrue(mgr.Apply(new DealOperation(4, 5)).Success);
        Assert.AreEqual(GamePhase.InPlay, mgr.Phase);
        Assert.AreEqual(4, mgr.CurrentHands.Length);
        Assert.AreEqual(32, mgr.CurrentDeck.Length);
        Assert.IsTrue(mgr.Table.Validate());

        Assert.IsFalse(mgr.Apply(new CutOperation(16)).Success);
        Assert.IsFalse(mgr.Apply(new RiffleOperation(16, new[] { 1 })).Success);
        Assert.IsFalse(mgr.Apply(new OverhandOperation(new[] { 32 })).Success);
        Assert.IsFalse(mgr.Apply(new DealOperation(2, 3)).Success, "double deal blocked");
        Assert.AreEqual(GamePhase.InPlay, mgr.Phase);

        Assert.IsTrue(mgr.Apply(CollectOperation.All()).Success);
        Assert.AreEqual(GamePhase.Idle, mgr.Phase);
        Assert.AreEqual(0, mgr.CurrentHands.Length);
        Assert.AreEqual(1, mgr.RoundNumber);
    }

    [Test]
    public void Classic_MultiRound_CountsRounds()
    {
        var mgr = new GameStateManager(new ClassicRules());
        for (int round = 1; round <= 3; round++)
        {
            mgr.Apply(new DealOperation(2, 5));
            mgr.Apply(CollectOperation.All());
            Assert.AreEqual(round, mgr.RoundNumber);
        }
        Assert.IsTrue(DeckEngine.ValidateDeck(mgr.CurrentDeck));
    }

    // ---- Free play ----

    [Test]
    public void FreePlay_AllowsAnythingPhysicallyPossible()
    {
        var mgr = new GameStateManager();
        Assert.IsTrue(mgr.Apply(new DealOperation(2, 5)).Success);
        Assert.IsTrue(mgr.Apply(new CutOperation(10)).Success, "cut the remaining deck");
        Assert.IsTrue(mgr.Apply(new DealOperation(2, 1)).Success, "deal more");
        Assert.AreEqual(6, mgr.CurrentHands[0].Length);
        Assert.IsTrue(mgr.Apply(new MoveCardOperation(mgr.CurrentHands[0][0], ZoneId.Pile(0), flip: true)).Success);
        Assert.IsFalse(mgr.Apply(new CutOperation(60)).Success, "still physically checked");
        Assert.IsTrue(mgr.Table.Validate());
    }

    [Test]
    public void FailedOperation_IsRolledBack_AndNotRecorded()
    {
        var mgr = new GameStateManager();
        mgr.Apply(new MovePacketOperation(CardMove.TopToTop(ZoneId.Deck, 2, ZoneId.LeftHand)));
        var before = mgr.Table.Clone();

        Assert.IsFalse(mgr.Apply(new RiffleOperation(20, new[] { 1 })).Success);
        CollectionAssert.AreEqual(Cards(before, ZoneId.Deck), Cards(mgr.Table, ZoneId.Deck));
        CollectionAssert.AreEqual(Cards(before, ZoneId.LeftHand), Cards(mgr.Table, ZoneId.LeftHand));

        mgr.Undo(); // undoes the packet move, not the failed riffle
        Assert.AreEqual(52, mgr.Table.Count(ZoneId.Deck));
        Assert.IsFalse(mgr.CanUndo);
    }

    [Test]
    public void CanApply_IsADryRun()
    {
        var mgr = new GameStateManager();
        Assert.IsTrue(mgr.CanApply(new DealOperation(2, 5)).Success);
        Assert.IsFalse(mgr.CanApply(new DealOperation(5, 11)).Success);
        Assert.IsFalse(mgr.CanApply(CollectOperation.All()).Success, "nothing to collect");
        Assert.AreEqual(52, mgr.Table.Count(ZoneId.Deck));
        Assert.IsFalse(mgr.CanUndo);
    }

    // ---- Undo ----

    [Test]
    public void Undo_RestoresExactTable_AcrossManyActions()
    {
        var mgr = new GameStateManager();
        var snapshots = new List<CardTable> { mgr.Table.Clone() };
        var ops = new GameOperation[]
        {
            new CutOperation(13),
            new RiffleOperation(26, new[] { 2, 1, 3 }),
            new OverhandOperation(new[] { 10, 20, 22 }),
            new DealOperation(3, 4, faceUp: true),
            new FlipCardOperation(5),
            CollectOperation.All(),
        };
        foreach (var op in ops)
        {
            Assert.IsTrue(mgr.Apply(op).Success, op.Name);
            snapshots.Add(mgr.Table.Clone());
        }

        for (int i = ops.Length - 1; i >= 0; i--)
        {
            Assert.IsTrue(mgr.Undo().Success);
            AssertSameTable(snapshots[i], mgr.Table);
        }
        Assert.IsFalse(mgr.Undo().Success, "nothing left to undo");
    }

    [Test]
    public void Undo_RestoresRoundNumber()
    {
        var mgr = new GameStateManager();
        mgr.Apply(new DealOperation(2, 5));
        mgr.Apply(CollectOperation.All());
        Assert.AreEqual(1, mgr.RoundNumber);
        mgr.Undo();
        Assert.AreEqual(0, mgr.RoundNumber);
        Assert.AreEqual(GamePhase.InPlay, mgr.Phase);
    }

    [Test]
    public void Reset_ClearsEverything()
    {
        var mgr = new GameStateManager();
        mgr.Apply(new DealOperation(2, 5));
        mgr.Apply(CollectOperation.All());
        mgr.Reset();
        Assert.AreEqual(0, mgr.RoundNumber);
        Assert.IsFalse(mgr.CanUndo);
        CollectionAssert.AreEqual(DeckEngine.CreateOrderedDeck(), mgr.CurrentDeck);
    }

    // ---- Events ----

    [Test]
    public void Events_CarryMovesForAnimation()
    {
        var mgr = new GameStateManager();
        var applied = new List<(TableAction action, bool undo)>();
        int stateChanges = 0;
        string rejection = null;
        mgr.ActionApplied     += (a, undo) => applied.Add((a, undo));
        mgr.StateChanged      += _ => stateChanges++;
        mgr.OperationRejected += (_, reason) => rejection = reason;

        mgr.Apply(new DealOperation(2, 3));
        Assert.AreEqual(1, applied.Count);
        Assert.AreEqual("Deal", applied[0].action.Name);
        Assert.AreEqual(6, applied[0].action.Moves.Count);
        Assert.IsFalse(applied[0].undo);

        mgr.Undo();
        Assert.IsTrue(applied[1].undo);
        Assert.AreEqual(2, stateChanges);

        mgr.Apply(new CutOperation(99));
        Assert.IsNotNull(rejection);
        Assert.AreEqual(2, applied.Count);
    }

    [Test]
    public void Snapshot_IsACopy()
    {
        var mgr = new GameStateManager();
        var snap = mgr.TakeSnapshot();
        mgr.Apply(new DealOperation(2, 5));
        Assert.AreEqual(52, snap.Deck.Length);
        Assert.AreEqual(52, snap.Table.Count(ZoneId.Deck));
    }

    // ---- Save / load ----

    [Test]
    public void SaveLoad_JsonRoundTrip_RestoresEveryCardAndFace()
    {
        var mgr = new GameStateManager();
        mgr.Apply(new RiffleOperation(20, new[] { 2, 1 }));
        mgr.Apply(new DealOperation(3, 4));
        mgr.Apply(new MoveCardOperation(mgr.CurrentHands[1][2], ZoneId.Pile(2), flip: true));
        mgr.Apply(new MovePacketOperation(CardMove.TopToTop(ZoneId.Deck, 3, ZoneId.Discard())));

        string json = JsonUtility.ToJson(mgr.CreateSaveData());
        var loaded = new GameStateManager();
        loaded.Load(JsonUtility.FromJson<TableSaveData>(json));

        AssertSameTable(mgr.Table, loaded.Table);
        Assert.AreEqual(mgr.RoundNumber, loaded.RoundNumber);
        Assert.IsFalse(loaded.CanUndo, "history is not persisted");
    }

    [Test]
    public void Load_RejectsCorruptData_AndKeepsCurrentTable()
    {
        var mgr  = new GameStateManager();
        var data = mgr.CreateSaveData();
        data.zones[0].cards[0] = data.zones[0].cards[1]; // duplicate a card

        mgr.Apply(new CutOperation(5));
        Assert.Throws<System.ArgumentException>(() => mgr.Load(data));
        Assert.AreEqual(5, mgr.CurrentDeck[0]);

        var badVersion = mgr.CreateSaveData();
        badVersion.version = 99;
        Assert.Throws<System.ArgumentException>(() => mgr.Load(badVersion));
    }

    private static void AssertSameTable(CardTable expected, CardTable actual)
    {
        CollectionAssert.AreEqual(expected.NonEmptyZones(), actual.NonEmptyZones());
        foreach (var zone in expected.NonEmptyZones())
            CollectionAssert.AreEqual(Cards(expected, zone), Cards(actual, zone), zone.ToString());
        for (int c = 0; c < 52; c++)
            Assert.AreEqual(expected.IsFaceUp(c), actual.IsFaceUp(c), $"face of card {c}");
    }
}
