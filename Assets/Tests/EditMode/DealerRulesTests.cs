using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// The dealing round: pick up → shuffle in hand → deal packets clockwise from the dealer's left →
/// dealt. Shuffling locks once dealing starts, and the deal must be finished.
public class DealerRulesTests
{
    private DealerRules      _rules;
    private GameStateManager _sm;

    [SetUp]
    public void NewRound()
    {
        _rules = new DealerRules(players: 4, cardsPerPlayer: 5, dealer: 0);
        _sm    = new GameStateManager(_rules);
    }

    private RoundPhase Phase => _rules.PhaseOf(_sm.Table);
    private int[] Cards(ZoneId z) => new List<int>(_sm.Table.Cards(z)).ToArray();

    private void Ok(GameOperation op)     => Assert.IsTrue(_sm.Apply(op).Success, op.Name);
    private void Refused(GameOperation op) => Assert.IsFalse(_sm.Apply(op).Success, op.Name);

    private static int[] Ones(int n)
    {
        var a = new int[n];
        for (int i = 0; i < n; i++) a[i] = 1;
        return a;
    }

    [Test]
    public void StartsGathered_AndOnlyPickUpIsAllowed()
    {
        Assert.AreEqual(RoundPhase.Gathered, Phase);
        Refused(new CutOperation(26));
        Refused(new RiffleOperation(26, new[] { 1 }));
        Refused(_rules.NextPacket(1));
        Refused(CollectOperation.All());
        Refused(new PickUpDeckOperation(ZoneId.LeftHand));

        Ok(new PickUpDeckOperation());
        Assert.AreEqual(RoundPhase.Shuffling, Phase);
        Assert.AreEqual(52, _sm.Table.Count(ZoneId.RightHand));
    }

    [Test]
    public void ShufflingInHand_ThenPutDown_IsAllowed()
    {
        Ok(new PickUpDeckOperation());
        Ok(new RiffleOperation(26, Ones(52), ZoneId.RightHand));
        Ok(new OverhandOperation(new[] { 10, 20, 22 }, ZoneId.RightHand));
        Ok(new CutOperation(13, ZoneId.RightHand));
        Assert.AreEqual(RoundPhase.Shuffling, Phase, "still holding the whole deck");
        Refused(new CutOperation(13), "shuffling must use the deck in hand");

        Ok(new PutDownDeckOperation());
        Assert.AreEqual(RoundPhase.Gathered, Phase);
        Assert.IsTrue(_sm.Table.Validate());
    }

    private void Refused(GameOperation op, string because) => Assert.IsFalse(_sm.Apply(op).Success, because);

    [Test]
    public void Dealing_GoesClockwiseFromDealersLeft_DealerLast()
    {
        Ok(new PickUpDeckOperation());
        Assert.AreEqual(1, _rules.NextSeat);

        var order = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            order.Add(_rules.NextSeat);
            Ok(_rules.NextPacket(1));
        }
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 0 }, order);
        Assert.AreEqual(1, _rules.NextSeat, "back round to the first seat");
    }

    [Test]
    public void StartingToDeal_LocksShufflingUntilTheDealIsComplete()
    {
        Ok(new PickUpDeckOperation());
        Ok(_rules.NextPacket(2));
        Assert.AreEqual(RoundPhase.Dealing, Phase);

        Refused(new CutOperation(10, ZoneId.RightHand));
        Refused(new RiffleOperation(20, new[] { 1 }, ZoneId.RightHand));
        Refused(new OverhandOperation(new[] { 50 }, ZoneId.RightHand));
        Refused(new PutDownDeckOperation());
        Refused(CollectOperation.All());
        Refused(new FlipCardOperation(_sm.Table.Cards(ZoneId.Hand(1))[0]));
        Refused(new DealOperation(4, 5));
    }

    [Test]
    public void PacketsOfAnySize_TwoTwoOne_CompleteTheDeal()
    {
        Ok(new PickUpDeckOperation());
        var deckBefore = Cards(ZoneId.RightHand);

        foreach (int size in new[] { 2, 2, 1 })
            for (int seat = 0; seat < 4; seat++)
                Ok(_rules.NextPacket(size));

        Assert.AreEqual(RoundPhase.Dealt, Phase);
        for (int seat = 0; seat < 4; seat++)
            Assert.AreEqual(5, _sm.Table.Count(ZoneId.Hand(seat)));
        Assert.AreEqual(32, _sm.Table.Count(ZoneId.RightHand), "the stub stays in the dealer's hand");

        // Seat 1 (first to receive) got deck cards 0,1 then 8,9 then 16 — packets stay in order
        CollectionAssert.AreEqual(
            new[] { deckBefore[0], deckBefore[1], deckBefore[8], deckBefore[9], deckBefore[16] },
            Cards(ZoneId.Hand(1)));

        Refused(_rules.NextPacket(1), "deal is finished");
        Ok(new PutDownDeckOperation());
        Assert.AreEqual(RoundPhase.Dealt, Phase);
    }

    [Test]
    public void MixedRhythm_PerSeat_IsAllowed_ButNotMoreThanASeatNeeds()
    {
        Ok(new PickUpDeckOperation());
        Ok(_rules.NextPacket(3));          // seat 1 → 3
        Ok(_rules.NextPacket(1));          // seat 2 → 1
        Ok(_rules.NextPacket(5));          // seat 3 → 5 (full)
        Ok(_rules.NextPacket(4));          // seat 0 → 4
        Assert.AreEqual(1, _rules.NextSeat);
        Refused(_rules.NextPacket(3), "seat 1 only needs 2");
        Ok(_rules.NextPacket(2));          // seat 1 full
        Assert.AreEqual(2, _rules.NextSeat, "seat 3 is full, so it is skipped later");
        Ok(_rules.NextPacket(4));          // seat 2 full
        Assert.AreEqual(0, _rules.NextSeat, "full seat 3 skipped");
        Ok(_rules.NextPacket(1));          // seat 0 full
        Assert.AreEqual(RoundPhase.Dealt, Phase);
    }

    [Test]
    public void DealingOutOfTurn_IsRefused()
    {
        Ok(new PickUpDeckOperation());
        Refused(new DealPacketOperation(2, 1, ZoneId.RightHand), "seat 1 is first");
        Refused(new DealPacketOperation(1, 1, ZoneId.Deck), "must deal from the dealer's hand");
    }

    [Test]
    public void AfterTheDeal_PlayAndCollect_StartANewRound()
    {
        DealFullRound(1);
        Ok(new MoveCardOperation(_sm.Table.Cards(ZoneId.Hand(0))[0], ZoneId.Pile(0), flip: true));
        Assert.AreEqual(RoundPhase.Playing, Phase);
        Refused(new PickUpDeckOperation(), "the deck isn't gathered");

        Ok(CollectOperation.All());
        Assert.AreEqual(RoundPhase.Gathered, Phase);
        Assert.AreEqual(1, _sm.RoundNumber);
        Assert.AreEqual(_rules.FirstSeat, _rules.NextSeat);
        Assert.IsTrue(_sm.Table.Validate());
    }

    [Test]
    public void Undo_RestoresWhoIsNextToReceive()
    {
        Ok(new PickUpDeckOperation());
        Ok(_rules.NextPacket(2));
        Ok(_rules.NextPacket(2));
        Assert.AreEqual(3, _rules.NextSeat);

        _sm.Undo();
        Assert.AreEqual(2, _rules.NextSeat);
        _sm.Undo();
        Assert.AreEqual(1, _rules.NextSeat);
        Assert.AreEqual(RoundPhase.Shuffling, Phase, "undoing the first packet unlocks shuffling");
        Ok(new CutOperation(5, ZoneId.RightHand));
    }

    [Test]
    public void SaveLoad_KeepsTheDealInProgress()
    {
        Ok(new PickUpDeckOperation());
        Ok(_rules.NextPacket(2));
        Ok(_rules.NextPacket(1));

        string json = JsonUtility.ToJson(_sm.CreateSaveData());
        var rules = new DealerRules(4, 5, 0);
        var loaded = new GameStateManager(rules);
        loaded.Load(JsonUtility.FromJson<TableSaveData>(json));

        Assert.AreEqual(RoundPhase.Dealing, rules.PhaseOf(loaded.Table));
        Assert.AreEqual(3, rules.NextSeat);
        Assert.IsFalse(loaded.Apply(new CutOperation(5, ZoneId.RightHand)).Success, "still locked after loading");
    }

    [Test]
    public void Reset_StartsAFreshRound()
    {
        Ok(new PickUpDeckOperation());
        Ok(_rules.NextPacket(1));
        _sm.Reset();
        Assert.AreEqual(RoundPhase.Gathered, Phase);
        Assert.AreEqual(_rules.FirstSeat, _rules.NextSeat);
    }

    [Test]
    public void OtherDealer_DealsToTheirLeftFirst()
    {
        var rules = new DealerRules(4, 5, dealer: 2);
        Assert.AreEqual(3, rules.FirstSeat);
        var sm = new GameStateManager(rules);
        sm.Apply(new PickUpDeckOperation());
        var order = new List<int>();
        for (int i = 0; i < 4; i++)
        {
            order.Add(rules.NextSeat);
            Assert.IsTrue(sm.Apply(rules.NextPacket(5)).Success);
        }
        CollectionAssert.AreEqual(new[] { 3, 0, 1, 2 }, order);
    }

    [Test]
    public void InvalidConfiguration_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => new DealerRules(5, 11));
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new DealerRules(4, 5, dealer: 4));
    }

    // ---- Shuffling a held deck matches the reference engine ----

    private static int[] Scrambled(int seed)
    {
        var deck = DeckEngine.CreateOrderedDeck();
        var rng = new System.Random(seed);
        for (int i = deck.Length - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }
        return deck;
    }

    private static CardTable HeldIn(ZoneId hand, int[] order)
    {
        var t = new CardTable(order);
        t.Apply(CardMove.TopToTop(ZoneId.Deck, 52, hand));
        return t;
    }

    [TestCase(true), TestCase(false)]
    public void HeldRiffle_MatchesDeckEngine(bool rightHand)
    {
        var hand = rightHand ? ZoneId.RightHand : ZoneId.LeftHand;
        var start = Scrambled(4);
        var pattern = new[] { 2, 1, 3, 1, 2 };
        var t = HeldIn(hand, start);
        new RiffleOperation(20, pattern, hand).Execute(new TableTransaction(t));

        CollectionAssert.AreEqual(DeckEngine.RiffleShuffle(start, 20, pattern), new List<int>(t.Cards(hand)).ToArray());
        Assert.AreEqual(0, t.Count(ZoneId.Deck));
        Assert.IsTrue(t.Validate());
    }

    [TestCase(true), TestCase(false)]
    public void HeldOverhand_MatchesDeckEngine(bool rightHand)
    {
        var hand = rightHand ? ZoneId.RightHand : ZoneId.LeftHand;
        var start = Scrambled(9);
        var chunks = new[] { 7, 5, 9, 11, 20 };
        var t = HeldIn(hand, start);
        new OverhandOperation(chunks, hand).Execute(new TableTransaction(t));

        CollectionAssert.AreEqual(DeckEngine.OverhandShuffle(start, chunks), new List<int>(t.Cards(hand)).ToArray());
        Assert.AreEqual(0, t.Count(rightHand ? ZoneId.LeftHand : ZoneId.RightHand), "other hand empty again");
    }

    [Test]
    public void HeldCut_MatchesDeckEngine()
    {
        var start = Scrambled(2);
        var t = HeldIn(ZoneId.RightHand, start);
        new CutOperation(17, ZoneId.RightHand).Execute(new TableTransaction(t));
        CollectionAssert.AreEqual(DeckEngine.CutDeck(start, 17), new List<int>(t.Cards(ZoneId.RightHand)).ToArray());
    }

    [Test]
    public void HeldRiffle_NeedsAClearDeckSpot()
    {
        var t = HeldIn(ZoneId.RightHand, DeckEngine.CreateOrderedDeck());
        t.Apply(CardMove.TopToTop(ZoneId.RightHand, 2, ZoneId.Deck));
        Assert.Throws<System.InvalidOperationException>(() =>
            new RiffleOperation(20, new[] { 1 }, ZoneId.RightHand).Execute(new TableTransaction(t)));
    }

    private void DealFullRound(int packet)
    {
        Ok(new PickUpDeckOperation());
        while (Phase != RoundPhase.Dealt) Ok(_rules.NextPacket(packet));
    }
}
