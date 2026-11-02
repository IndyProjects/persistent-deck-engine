using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// Blackjack (the shedding game): matching, runs, special cards, stacking, last card, going out, the
/// draw pile, persistence, the full round flow, and AI games played to the end.
public class BlackjackTests
{
    // ---- Card shorthand: C("7H") = 7 of Hearts, C("10S"), C("AS"), C("JC") ----

    private static int C(string name)
    {
        string rank = name.Substring(0, name.Length - 1);
        int suit = "HDCS".IndexOf(name[name.Length - 1]);
        int r = rank switch { "J" => 9, "Q" => 10, "K" => 11, "A" => 12, _ => int.Parse(rank) - 2 };
        return suit * 13 + r;
    }

    private static int[] Cs(params string[] names)
    {
        var cards = new int[names.Length];
        for (int i = 0; i < names.Length; i++) cards[i] = C(names[i]);
        return cards;
    }

    /// A game in progress: the given hands (seat → cards; null = three ordinary cards nobody else
    /// holds), <paramref name="top"/> on the discard pile, everything else in the draw pile.
    /// Dealer 3, so seat 0 (you) plays first.
    private static (GameStateManager sm, BlackjackRules rules) Game(string top, params string[][] hands)
    {
        var rules = new BlackjackRules(4, dealer: 3);
        var sm = new GameStateManager(rules);
        var used = new HashSet<int> { C(top) };
        foreach (var hand in hands)
            if (hand != null) foreach (int c in Cs(hand)) used.Add(c);

        var zones = new List<TableSaveData.ZoneData>
        {
            new TableSaveData.ZoneData { kind = ZoneKind.Discard, index = 0, cards = new[] { C(top) } }
        };
        for (int seat = 0; seat < hands.Length; seat++)
        {
            var cards = hands[seat] != null ? Cs(hands[seat]) : Filler(used);
            zones.Add(new TableSaveData.ZoneData { kind = ZoneKind.Hand, index = seat, cards = cards });
        }
        var rest = new List<int>();
        for (int c = 0; c < 52; c++) if (!used.Contains(c)) rest.Add(c);
        zones.Add(new TableSaveData.ZoneData { kind = ZoneKind.Deck, index = 0, cards = rest.ToArray() });

        sm.Load(new TableSaveData { zones = zones.ToArray(), faceUpCards = new[] { C(top) } });
        Assert.AreEqual(BlackjackRules.Phase.Playing, rules.PhaseOf(sm.Table));
        Assert.AreEqual(0, rules.Turn);
        return (sm, rules);
    }

    /// Three ordinary (non-special) cards not used elsewhere, marking them used.
    private static int[] Filler(HashSet<int> used)
    {
        var cards = new List<int>();
        for (int c = 51; c >= 0 && cards.Count < 3; c--)
            if (!used.Contains(c) && !BlackjackRules.IsSpecial(c)) { cards.Add(c); used.Add(c); }
        return cards.ToArray();
    }


    private static bool Play(GameStateManager sm, int seat, string[] cards, int suit = -1)
        => sm.Apply(new PlayCardsOperation(seat, Cs(cards), suit)).Success;

    // ---- Matching and runs ----

    [Test]
    public void Cards_MatchBySuitOrNumber()
    {
        var (sm, _) = Game("7H", new[] { "9S", "2D", "7S", "KH" }, null, null, null);
        Assert.IsFalse(Play(sm, 0, new[] { "9S" }), "neither suit nor number");
        Assert.IsTrue(Play(sm, 0, new[] { "7S" }), "same number");
    }

    [Test]
    public void Runs_AreConsecutiveOneSuit_UpOrDown_FirstCardMatching()
    {
        var (sm, rules) = Game("5H", new[] { "6H", "7H", "9H", "4H", "3H", "QC" }, null, null, null);
        Assert.IsFalse(Play(sm, 0, new[] { "6H", "7H", "9H" }), "gap in the run");
        Assert.IsFalse(Play(sm, 0, new[] { "6H", "QC" }), "mixed suits");
        Assert.IsTrue(Play(sm, 0, new[] { "4H", "3H" }), "descending run");
        Assert.AreEqual(1, rules.Turn);
    }

    [Test]
    public void Runs_CanUseAceLowOrHigh()
    {
        Assert.IsTrue(BlackjackRules.IsRun(Cs("AH", "2H", "3H")));
        Assert.IsTrue(BlackjackRules.IsRun(Cs("QS", "KS", "AS")));
        Assert.IsFalse(BlackjackRules.IsRun(Cs("KS", "AS", "2S")), "no wrapping round");
    }

    [Test]
    public void SelectionInAnyOrder_IsOrderedIntoALegalRun()
    {
        var (sm, rules) = Game("9D", new[] { "JD", "9C", "10D", "QD", "4S" }, null, null, null);
        var ordered = BlackjackPlays.Order(rules, sm.Table, 0, Cs("QD", "10D", "JD"), 0);
        CollectionAssert.AreEqual(Cs("10D", "JD", "QD"), ordered);
        Assert.IsNull(BlackjackPlays.Order(rules, sm.Table, 0, Cs("10D", "4S"), 0));
    }

    // ---- Special cards ----

    [Test]
    public void Eight_SkipsTheNextPlayer_AndOnlyTheLastCardOfARunCounts()
    {
        var (sm, rules) = Game("6S", new[] { "7S", "8S", "KD" }, null, null, null);
        Assert.IsTrue(Play(sm, 0, new[] { "7S", "8S" }));
        Assert.AreEqual(2, rules.Turn, "seat 1 missed a turn");
        Assert.AreEqual(0, rules.Pending);
    }

    [Test]
    public void Twos_Stack_AndTheLoserPicksUpEverything()
    {
        var (sm, rules) = Game("5H", new[] { "2H", "KD", "QS" }, new[] { "2S", "KC" }, new[] { "9D", "10D" }, null);
        Assert.IsTrue(Play(sm, 0, new[] { "2H" }));
        Assert.AreEqual(2, rules.Pending);

        Assert.IsFalse(Play(sm, 1, new[] { "KC" }), "must stack or pick up");
        Assert.IsTrue(Play(sm, 1, new[] { "2S" }), "a 2 stacks");
        Assert.AreEqual(4, rules.Pending);

        Assert.IsFalse(sm.Apply(new DrawCardsOperation(2, 1)).Success, "must take the whole penalty");
        Assert.IsTrue(sm.Apply(new DrawCardsOperation(2, 4)).Success);
        Assert.AreEqual(6, sm.Table.Count(ZoneId.Hand(2)));
        Assert.AreEqual(0, rules.Pending);
        Assert.AreEqual(3, rules.Turn, "picking up ends the turn");
    }

    [Test]
    public void BlackJacks_PickUpSeven_StackOnlyWithBlackJacks()
    {
        var (sm, rules) = Game("4S", new[] { "JS", "KD", "QD" }, new[] { "JH", "JC", "5D" }, null, null);
        Assert.IsTrue(Play(sm, 0, new[] { "JS" }));
        Assert.AreEqual(7, rules.Pending);
        Assert.IsFalse(Play(sm, 1, new[] { "JH" }), "a red jack doesn't stack");
        Assert.IsTrue(Play(sm, 1, new[] { "JC" }));
        Assert.AreEqual(14, rules.Pending);
    }

    [Test]
    public void Ace_NamesTheSuitToFollow()
    {
        var (sm, rules) = Game("9H", new[] { "AH", "KD", "QD" }, new[] { "5H", "6C", "AD" }, null, null);
        Assert.IsFalse(Play(sm, 0, new[] { "AH" }), "an Ace must name a suit");
        Assert.IsTrue(Play(sm, 0, new[] { "AH" }, suit: 2)); // Clubs
        Assert.AreEqual(2, rules.SuitToFollow(sm.Table));
        Assert.IsFalse(Play(sm, 1, new[] { "5H" }), "hearts no longer follow");
        Assert.IsTrue(Play(sm, 1, new[] { "6C" }));
    }

    // ---- Last card and going out ----

    [Test]
    public void GoingOut_NeedsLastCardCalled_AndANormalFinalCard()
    {
        var (sm, rules) = Game("4D", new[] { "5D", "6D" }, null, null, null);
        Assert.IsFalse(Play(sm, 0, new[] { "5D", "6D" }), "LAST CARD not called");
        Assert.IsTrue(sm.Apply(new AnnounceLastCardOperation(0)).Success);
        Assert.IsTrue(Play(sm, 0, new[] { "5D", "6D" }));

        Assert.AreEqual(BlackjackRules.Phase.Finished, rules.PhaseOf(sm.Table));
        Assert.AreEqual(0, rules.Winner);
        Assert.AreEqual(1, rules.Wins(0));
        Assert.IsFalse(sm.Apply(new DrawCardsOperation(1, 1)).Success, "the game is over");
        Assert.IsTrue(sm.Apply(CollectOperation.All()).Success);
        Assert.AreEqual(52, sm.Table.Count(ZoneId.Deck));
        Assert.AreEqual(1, rules.Wins(0), "wins carry into the next game");
    }

    [Test]
    public void YouCantGoOutOnASpecialCard()
    {
        var (sm, _) = Game("4D", new[] { "8D" }, null, null, null);
        sm.Apply(new AnnounceLastCardOperation(0));
        Assert.IsFalse(Play(sm, 0, new[] { "8D" }));
    }

    [Test]
    public void ForgettingLastCard_CanBeCaught_ForTwoCards()
    {
        var (sm, rules) = Game("4D", new[] { "5D", "KS" }, null, null, null);
        Assert.IsTrue(Play(sm, 0, new[] { "5D" }));
        Assert.IsTrue(rules.CanBeCaught(0, sm.Table));
        Assert.IsTrue(sm.Apply(new CatchLastCardOperation(1, 0)).Success);
        Assert.AreEqual(3, sm.Table.Count(ZoneId.Hand(0)));
        Assert.IsFalse(sm.Apply(new CatchLastCardOperation(1, 0)).Success, "only once");
    }

    [Test]
    public void CallingLastCard_PreventsTheCatch()
    {
        var (sm, rules) = Game("4D", new[] { "5D", "KS" }, null, null, null);
        sm.Apply(new AnnounceLastCardOperation(0));
        Play(sm, 0, new[] { "5D" });
        Assert.IsFalse(rules.CanBeCaught(0, sm.Table));
        Assert.IsFalse(sm.Apply(new CatchLastCardOperation(1, 0)).Success);
    }

    // ---- Draw pile ----

    [Test]
    public void EmptyDrawPile_TurnsTheDiscardsOver()
    {
        var (sm, rules) = Game("4D", new[] { "9S", "KC" }, null, null, null);
        // Move the whole draw pile onto the discards (under the top card) to empty it
        int n = sm.Table.Count(ZoneId.Deck);
        sm.Rules = new FreePlayRules();
        sm.Apply(new MovePacketOperation(new CardMove(ZoneId.Deck, 0, n, ZoneId.Discard(), 1, flip: true)));
        sm.Rules = rules;
        Assert.AreEqual(0, sm.Table.Count(ZoneId.Deck));

        int top = BlackjackRules.Top(sm.Table);
        Assert.IsTrue(sm.Apply(new DrawCardsOperation(0, 1)).Success);
        Assert.AreEqual(top, BlackjackRules.Top(sm.Table), "the top card stays");
        Assert.AreEqual(n - 1, sm.Table.Count(ZoneId.Deck));
        foreach (int card in sm.Table.Cards(ZoneId.Deck)) Assert.IsFalse(sm.Table.IsFaceUp(card));
        Assert.IsTrue(sm.Table.Validate());
    }

    // ---- Persistence and flow ----

    [Test]
    public void GameState_SurvivesUndoAndSaveLoad()
    {
        var (sm, rules) = Game("5H", new[] { "2H", "KD", "QS" }, new[] { "2S", "KC" }, null, null);
        Play(sm, 0, new[] { "2H" });
        Play(sm, 1, new[] { "2S" });
        Assert.AreEqual(4, rules.Pending);

        sm.Undo();
        Assert.AreEqual(2, rules.Pending);
        Assert.AreEqual(1, rules.Turn);

        string json = JsonUtility.ToJson(sm.CreateSaveData());
        var loadedRules = new BlackjackRules(4, dealer: 3);
        new GameStateManager(loadedRules).Load(JsonUtility.FromJson<TableSaveData>(json));
        Assert.AreEqual(2, loadedRules.Pending);
        Assert.AreEqual(BlackjackRules.Penalty.Twos, loadedRules.PendingKind);
        Assert.AreEqual(1, loadedRules.Turn);
    }

    [Test]
    public void FullRound_DealSevenEach_PutDown_TurnUp_Play()
    {
        var rules = new BlackjackRules(4);
        var sm = new GameStateManager(rules);
        Assert.AreEqual(BlackjackRules.Phase.Dealing, rules.PhaseOf(sm.Table));
        Assert.IsFalse(sm.Apply(new PlayCardsOperation(1, new[] { 0 })).Success, "no playing during the deal");

        sm.Apply(new PickUpDeckOperation());
        sm.Apply(new RiffleOperation(26, new[] { 1 }, ZoneId.RightHand));
        rules.Dealing.PacketSize = 7;
        for (int i = 0; i < 4; i++) Assert.IsTrue(sm.Apply(rules.Dealing.NextPacket(sm.Table)).Success);
        Assert.AreEqual(BlackjackRules.Phase.Dealing, rules.PhaseOf(sm.Table), "stub still in hand");
        Assert.IsTrue(sm.Apply(new PutDownDeckOperation()).Success);
        Assert.AreEqual(BlackjackRules.Phase.ReadyToStart, rules.PhaseOf(sm.Table));

        Assert.IsTrue(sm.Apply(new StartDiscardOperation()).Success);
        Assert.AreEqual(BlackjackRules.Phase.Playing, rules.PhaseOf(sm.Table));
        Assert.AreEqual(1, rules.Turn, "play starts left of the dealer");
        Assert.AreEqual(23, sm.Table.Count(ZoneId.Deck));
        Assert.AreEqual(5, BlackjackRules.CardsFor(5));
    }

    [TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5)]
    public void AIGames_AlwaysProgress_AndReachAWinner(int seed)
    {
        var rules = new BlackjackRules(4);
        var sm = new GameStateManager(rules);
        var rng = new System.Random(seed);

        sm.Apply(new PickUpDeckOperation());
        for (int r = 0; r < 4; r++)
            sm.Apply(new RiffleOperation(20 + rng.Next(12), new[] { 1 + rng.Next(3), 1 + rng.Next(3) }, ZoneId.RightHand));
        rules.Dealing.PacketSize = 7;
        while (rules.Dealing.PhaseOf(sm.Table) != RoundPhase.Dealt) sm.Apply(rules.Dealing.NextPacket(sm.Table));
        sm.Apply(new PutDownDeckOperation());
        sm.Apply(new StartDiscardOperation());

        for (int move = 0; move < 3000 && rules.Winner < 0; move++)
        {
            int seat = rules.Turn;
            var op = BlackjackAI.ChooseMove(rules, sm.Table, seat, rng, out bool call);
            if (call) Assert.IsTrue(sm.Apply(new AnnounceLastCardOperation(seat)).Success);
            var result = sm.Apply(op);
            Assert.IsTrue(result.Success, $"move {move}: {op.Name} — {result.Reason}");
            Assert.IsTrue(sm.Table.Validate());
        }
        Assert.That(rules.Winner, Is.InRange(0, 3), "someone went out");
    }
}
