using System;
using System.Collections.Generic;

/// Finding and ordering legal Blackjack plays (shared by the AI and the human's card selection).
public static class BlackjackPlays
{
    /// Every legal play for a seat right now — single cards and runs in both directions — ignoring
    /// whether LAST CARD has been called (the caller decides about calling).
    public static List<PlayCardsOperation> Legal(BlackjackRules rules, CardTable table, int seat)
    {
        var hand = new List<int>(table.Cards(ZoneId.Hand(seat)));
        var plays = new List<PlayCardsOperation>();
        var seen = new HashSet<string>();

        foreach (int start in hand)
        foreach (int direction in new[] { 1, -1 })
        {
            var run = new List<int> { start };
            while (true)
            {
                // FindIndex, not Find: Find returns 0 when nothing matches, and 0 is a real card (2♥)
                int next = hand.FindIndex(c => !run.Contains(c) && BlackjackRules.Follows(run[run.Count - 1], c, direction));
                if (next < 0) break;
                run.Add(hand[next]);
            }

            for (int length = 1; length <= run.Count; length++)
            {
                var cards = run.GetRange(0, length).ToArray();
                if (!seen.Add(string.Join(",", cards))) continue;
                int suit = DeckEngine.GetRank(cards[length - 1]) == BlackjackRules.Ace ? BestSuit(hand, cards) : -1;
                var play = new PlayCardsOperation(seat, cards, suit);
                if (rules.CheckPlay(play, table, requireAnnouncement: false).Success) plays.Add(play);
            }
        }
        return plays;
    }

    /// Puts a set of selected cards into an order that's a legal play if one exists — so the player can
    /// click cards in any order. Falls back to any valid run order (so the rules can explain why it
    /// doesn't match the pile), or null if the cards aren't a run at all.
    public static int[] Order(BlackjackRules rules, CardTable table, int seat, IReadOnlyList<int> selected, int chosenSuit)
    {
        if (selected == null || selected.Count == 0) return null;
        if (selected.Count == 1) return new[] { selected[0] };

        int[] fallback = null;
        foreach (bool aceLow in new[] { false, true })
        {
            var sorted = new List<int>(selected);
            sorted.Sort((a, b) => Value(a, aceLow).CompareTo(Value(b, aceLow)));
            foreach (bool reverse in new[] { false, true })
            {
                var order = new List<int>(sorted);
                if (reverse) order.Reverse();
                var cards = order.ToArray();
                if (!BlackjackRules.IsRun(cards)) continue;
                fallback ??= cards;
                if (rules.CheckPlay(new PlayCardsOperation(seat, cards, chosenSuit), table, requireAnnouncement: false).Success)
                    return cards;
            }
        }
        return fallback;
    }

    /// The suit the player holds most of (excluding cards about to be played) — the natural Ace call.
    public static int BestSuit(IReadOnlyList<int> hand, IReadOnlyList<int> exclude)
    {
        var counts = new int[4];
        foreach (int card in hand)
            if (exclude == null || Array.IndexOf(ToArray(exclude), card) < 0) counts[DeckEngine.GetSuit(card)]++;
        int best = 0;
        for (int s = 1; s < 4; s++) if (counts[s] > counts[best]) best = s;
        return best;
    }

    private static int Value(int card, bool aceLow)
    {
        int rank = DeckEngine.GetRank(card);
        return aceLow && rank == BlackjackRules.Ace ? -1 : rank;
    }

    private static int[] ToArray(IReadOnlyList<int> list)
    {
        var a = new int[list.Count];
        for (int i = 0; i < a.Length; i++) a[i] = list[i];
        return a;
    }
}

/// A simple Blackjack opponent: goes out when it can, plays long runs, saves attack cards (2, 8, black
/// jacks) for when the next player is close to winning, holds Aces back, and stacks rather than picks up.
public static class BlackjackAI
{
    /// The move to make: a play, or picking up. <paramref name="wantsToCall"/> says the play would leave
    /// it on one card or none, so it should call LAST CARD first.
    public static GameOperation ChooseMove(BlackjackRules rules, CardTable table, int seat, Random rng, out bool wantsToCall)
    {
        wantsToCall = false;
        var plays = BlackjackPlays.Legal(rules, table, seat);
        if (plays.Count == 0) return new DrawCardsOperation(seat, rules.DrawCount);

        int handCount = table.Count(ZoneId.Hand(seat));
        int nextCount = table.Count(ZoneId.Hand(rules.Next(seat)));
        PlayCardsOperation best = null;
        double bestScore = double.MinValue;

        foreach (var play in plays)
        {
            double score = Score(play, handCount, nextCount, rules, table) + rng.NextDouble() * 2.0;
            if (score > bestScore)
            {
                bestScore = score;
                best = play;
            }
        }

        int remaining = handCount - best.Cards.Length;
        wantsToCall = remaining <= 1 && !rules.HasAnnounced(seat);
        return best;
    }

    private static double Score(PlayCardsOperation play, int handCount, int nextCount, BlackjackRules rules, CardTable table)
    {
        int last = play.Cards[play.Cards.Length - 1];
        int rank = DeckEngine.GetRank(last);
        int remaining = handCount - play.Cards.Length;

        if (remaining == 0) return 1000;                 // going out
        double score = 10 * play.Cards.Length;            // shed as many as possible
        if (rules.Pending > 0) score += 100;              // stacking beats picking up

        bool attack = rank == BlackjackRules.Two || rank == BlackjackRules.Eight || BlackjackRules.IsBlackJack(last);
        if (attack) score += nextCount <= 3 ? 25 : -6;    // save attacks for when they matter
        if (rank == BlackjackRules.Ace) score -= 15;      // the Ace is a get-out-of-jail card

        if (remaining == 1)
        {
            // Don't strand yourself on a special card you can't go out with
            foreach (int card in table.Cards(ZoneId.Hand(play.Seat)))
                if (Array.IndexOf(play.Cards, card) < 0 && BlackjackRules.IsSpecial(card)) score -= 30;
        }
        return score;
    }
}
