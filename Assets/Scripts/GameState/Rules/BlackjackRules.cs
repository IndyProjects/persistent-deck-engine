using System;
using System.Collections.Generic;
using UnityEngine;

/// Blackjack, the shedding game: be first to get rid of all your cards.
///
/// The round starts with the standard dealing round (7 cards each, 5 if there are 5+ players); the stub
/// is put down as the draw pile and its top card turned up to start the discard pile. Play goes
/// clockwise from the dealer's left. A play is one card, or a run of consecutive cards of one suit (up or
/// down; Ace low or high), whose first card matches the pile by suit or number. Only the last card of a
/// play takes effect:
///   2            next player picks up 2 (2s stack)
///   8            next player misses a turn
///   J♠ / J♣      next player picks up 7 (black jacks stack)
///   Ace          the player names the suit to follow
/// A pending pickup must be stacked with a single matching card or picked up (which ends the turn).
/// A player who can't or won't play draws one card. Call LAST CARD before going down to one card (or
/// before going out with a run); anyone may catch an uncalled last card, and the caught player picks up 2.
/// You can't go out on a special card. First player out wins; the deck is then collected for a new round.
public sealed class BlackjackRules : IStatefulRuleSet, IDealingRuleSet
{
    public const int Two = 0, Eight = 6, Jack = 9, Ace = 12; // DeckEngine ranks (0 = 2 … 12 = Ace)
    public const int TwoPenalty = 2, BlackJackPenalty = 7;

    public enum Phase { Dealing, ReadyToStart, Playing, Finished }
    public enum Penalty { None, Twos, BlackJacks }

    public int         Players { get; }
    public DealerRules Dealing { get; }

    // ---- Game state (saved; restored by undo) ----
    public int     Turn        { get; private set; }
    public int     Pending     { get; private set; }
    public Penalty PendingKind { get; private set; }
    /// Suit the next play must follow (the top card's, or the one named with an Ace).
    public int     ActiveSuit  { get; private set; } = -1;
    public int     Winner      { get; private set; } = -1;
    private bool[] _announced;
    private int[]  _wins;

    public string Name => "Blackjack";

    public static int CardsFor(int players) => players >= 5 ? 5 : 7;

    public BlackjackRules(int players = 4, int dealer = 0)
    {
        Players = players;
        Dealing = new DealerRules(players, CardsFor(players), dealer);
        _wins = new int[players];
        ResetGame();
    }

    public bool HasAnnounced(int seat) => _announced[seat];
    public int  Wins(int seat)         => _wins[seat];
    public int  Next(int seat)         => (seat + 1) % Players;

    // ---- Card helpers ----

    public static bool IsBlackJack(int card) => DeckEngine.GetRank(card) == Jack && DeckEngine.GetSuit(card) >= 2;

    public static bool IsSpecial(int card)
    {
        int rank = DeckEngine.GetRank(card);
        return rank == Two || rank == Eight || rank == Ace || IsBlackJack(card);
    }

    /// True if <paramref name="b"/> directly follows <paramref name="a"/> in a run going in
    /// <paramref name="direction"/> (+1 up, −1 down). Same suit; Ace may sit below 2 or above King.
    public static bool Follows(int a, int b, int direction)
    {
        if (DeckEngine.GetSuit(a) != DeckEngine.GetSuit(b)) return false;
        int ra = DeckEngine.GetRank(a), rb = DeckEngine.GetRank(b);
        if (rb - ra == direction) return true;
        // Ace low: A,2 going up / 2,A going down
        return (ra == Ace && rb == Two && direction == 1) || (ra == Two && rb == Ace && direction == -1);
    }

    /// True if the cards, in this order, form one card or a consecutive same-suit run.
    public static bool IsRun(IReadOnlyList<int> cards)
    {
        if (cards == null || cards.Count == 0) return false;
        if (cards.Count == 1) return true;
        int suit = DeckEngine.GetSuit(cards[0]);
        foreach (int card in cards) if (DeckEngine.GetSuit(card) != suit) return false;

        // The Ace is low or high for the whole run (no K-A-2 wrap-around)
        foreach (bool aceLow in new[] { false, true })
        foreach (int direction in new[] { 1, -1 })
        {
            bool ok = true;
            for (int i = 1; i < cards.Count && ok; i++)
                ok = RunValue(cards[i], aceLow) - RunValue(cards[i - 1], aceLow) == direction;
            if (ok) return true;
        }
        return false;
    }

    private static int RunValue(int card, bool aceLow)
    {
        int rank = DeckEngine.GetRank(card);
        return aceLow && rank == Ace ? -1 : rank;
    }

    public static int Top(CardTable table) => table.Count(ZoneId.Discard()) > 0 ? table.Cards(ZoneId.Discard())[0] : -1;

    // ---- Phases ----

    public Phase PhaseOf(CardTable table)
    {
        if (Winner >= 0) return Phase.Finished;
        if (table.Count(ZoneId.Discard()) > 0) return Phase.Playing;
        bool dealt = Dealing.PhaseOf(table) == RoundPhase.Dealt;
        if (dealt && table.Count(Dealing.DealerHand) == 0 && table.Count(ZoneId.Deck) > 0) return Phase.ReadyToStart;
        return Phase.Dealing;
    }

    // ---- Legality ----

    public OperationResult Check(GameOperation op, CardTable table)
    {
        switch (PhaseOf(table))
        {
            case Phase.Dealing:
                switch (op)
                {
                    case PickUpDeckOperation _: case PutDownDeckOperation _: case DealPacketOperation _:
                    case CutOperation _: case CutSectionOperation _: case RiffleOperation _: case OverhandOperation _:
                    case CollectOperation _:
                        return Dealing.Check(op, table);
                    default:
                        return Reject(Dealing.PhaseOf(table) == RoundPhase.Dealt
                            ? "Put the rest of the deck down as the draw pile."
                            : "Finish the deal first.");
                }

            case Phase.ReadyToStart:
                return op is StartDiscardOperation || op is CollectOperation
                    ? Ok() : Reject("Turn up the first card to start.");

            case Phase.Playing:
                switch (op)
                {
                    case PlayCardsOperation play:          return CheckPlay(play, table, requireAnnouncement: true);
                    case DrawCardsOperation draw:          return CheckDraw(draw);
                    case AnnounceLastCardOperation call:   return CheckAnnounce(call, table);
                    case CatchLastCardOperation caught:    return CheckCatch(caught, table);
                    case CollectOperation _:               return Reject("The game isn't over yet (Reset abandons it).");
                    default:                               return Reject($"{Who(Turn)} to play: play cards or pick up.");
                }

            default: // Finished
                return op is CollectOperation
                    ? Ok() : Reject($"{Who(Winner)} won — collect the cards for a new game.");
        }
    }

    /// Whether a play is legal right now. <paramref name="requireAnnouncement"/> false lets an AI ask
    /// "could I go out if I called LAST CARD first?".
    public OperationResult CheckPlay(PlayCardsOperation play, CardTable table, bool requireAnnouncement)
    {
        if (play.Seat != Turn) return Reject($"It's {Whose(Turn)} turn.");
        var cards = play.Cards;
        if (cards == null || cards.Length == 0) return Reject("Choose cards to play.");
        var hand = ZoneId.Hand(play.Seat);
        var seen = new HashSet<int>();
        foreach (int card in cards)
        {
            if (card < 0 || card >= DeckEngine.DeckSize || table.LocationOf(card) != hand) return Reject("You can only play cards from your hand.");
            if (!seen.Add(card)) return Reject("Each card can only be played once.");
        }
        if (!IsRun(cards)) return Reject("Several cards must be a run: consecutive, all one suit.");

        int first = cards[0], last = cards[cards.Length - 1];
        if (Pending > 0)
        {
            if (cards.Length > 1) return Reject($"Stack a single {PenaltyCard} or pick up {Pending}.");
            if (!Stacks(first)) return Reject($"Stack a {PenaltyCard} or pick up {Pending}.");
        }
        else if (!Matches(first, table))
            return Reject($"Play {SuitName(SuitToFollow(table))} or a {RankName(Top(table))} (or pick up).");

        bool lastIsAce = DeckEngine.GetRank(last) == Ace;
        if (lastIsAce && (play.ChosenSuit < 0 || play.ChosenSuit > 3)) return Reject("Name the suit for your Ace.");
        if (!lastIsAce && play.ChosenSuit >= 0) return Reject("Only an Ace names a suit.");

        if (cards.Length == table.Count(hand))
        {
            if (IsSpecial(last)) return Reject("You can't go out on a special card.");
            if (requireAnnouncement && !_announced[play.Seat]) return Reject("Call LAST CARD before going out.");
        }
        return Ok();
    }

    /// True if <paramref name="card"/> may start a play on the current pile (ignoring pending pickups).
    public bool Matches(int card, CardTable table)
    {
        int top = Top(table);
        return top < 0 || DeckEngine.GetSuit(card) == SuitToFollow(table) || DeckEngine.GetRank(card) == DeckEngine.GetRank(top);
    }

    /// The suit to follow: the one named with an Ace, otherwise the top card's.
    public int SuitToFollow(CardTable table)
    {
        if (ActiveSuit >= 0) return ActiveSuit;
        int top = Top(table);
        return top >= 0 ? DeckEngine.GetSuit(top) : -1;
    }

    /// True if <paramref name="card"/> passes on the pending pickup.
    public bool Stacks(int card)
        => (PendingKind == Penalty.Twos && DeckEngine.GetRank(card) == Two) ||
           (PendingKind == Penalty.BlackJacks && IsBlackJack(card));

    /// Cards the current player must pick up if they don't play (the pending pickup, else one).
    public int DrawCount => Pending > 0 ? Pending : 1;

    private OperationResult CheckDraw(DrawCardsOperation draw)
    {
        if (draw.Seat != Turn) return Reject($"It's {Whose(Turn)} turn.");
        if (draw.Count != DrawCount)
            return Reject(Pending > 0 ? $"Pick up all {Pending}." : "Pick up one card.");
        return Ok();
    }

    private OperationResult CheckAnnounce(AnnounceLastCardOperation call, CardTable table)
    {
        if (call.Seat < 0 || call.Seat >= Players) return Reject("No such seat.");
        if (_announced[call.Seat]) return Reject("Already called.");
        if (call.Seat != Turn && table.Count(ZoneId.Hand(call.Seat)) != 1)
            return Reject("Call LAST CARD on your turn, or once you're down to one card.");
        return Ok();
    }

    private OperationResult CheckCatch(CatchLastCardOperation caught, CardTable table)
        => CanBeCaught(caught.Target, table) && caught.Catcher != caught.Target && caught.Catcher >= 0 && caught.Catcher < Players
            ? Ok() : Reject("There's no uncalled last card to catch.");

    /// True if a seat holds one card without having called LAST CARD, and its turn has passed.
    public bool CanBeCaught(int seat, CardTable table)
        => seat >= 0 && seat < Players && !_announced[seat] && seat != Turn && table.Count(ZoneId.Hand(seat)) == 1;

    // ---- Effects ----

    public void OnApplied(GameOperation op, CardTable table)
    {
        switch (op)
        {
            case StartDiscardOperation _:
                ResetGame();
                ActiveSuit = DeckEngine.GetSuit(Top(table)); // the starting card's effect doesn't apply
                break;

            case PlayCardsOperation play:
                AfterPlay(play, table);
                break;

            case DrawCardsOperation draw:
                Pending = 0;
                PendingKind = Penalty.None;
                _announced[draw.Seat] = false;
                Turn = Next(draw.Seat);
                break;

            case AnnounceLastCardOperation call:
                _announced[call.Seat] = true;
                break;

            case CatchLastCardOperation caught:
                _announced[caught.Target] = false;
                break;

            case CollectOperation _:
                ResetGame();
                Dealing.OnApplied(op, table);
                break;

            default:
                Dealing.OnApplied(op, table);
                break;
        }
    }

    private void AfterPlay(PlayCardsOperation play, CardTable table)
    {
        int last = play.Cards[play.Cards.Length - 1];
        if (table.Count(ZoneId.Hand(play.Seat)) == 0)
        {
            Winner = play.Seat;
            _wins[play.Seat]++;
            return;
        }

        int rank = DeckEngine.GetRank(last);
        ActiveSuit = rank == Ace ? play.ChosenSuit : DeckEngine.GetSuit(last);

        bool skip = false;
        if (rank == Two)            { Pending += TwoPenalty;       PendingKind = Penalty.Twos; }
        else if (IsBlackJack(last)) { Pending += BlackJackPenalty; PendingKind = Penalty.BlackJacks; }
        else if (rank == Eight)     skip = true;

        int next = Next(play.Seat);
        Turn = skip ? Next(next) : next;
    }

    private void ResetGame()
    {
        Turn = Dealing.FirstSeat;
        Pending = 0;
        PendingKind = Penalty.None;
        ActiveSuit = -1;
        Winner = -1;
        _announced = new bool[Players];
    }

    // ---- Status ----

    public string Describe(CardTable table)
    {
        switch (PhaseOf(table))
        {
            case Phase.Dealing:      return Dealing.Describe(table);
            case Phase.ReadyToStart: return "Turning up the first card…";
            case Phase.Finished:     return $"{Who(Winner)} won! Collect to play again";
            default:
                string who = Turn == 0 ? "Your turn" : $"Seat {Turn} to play";
                return Pending > 0
                    ? $"{who} · stack a {PenaltyCard} or pick up {Pending}"
                    : $"{who} · {SuitName(SuitToFollow(table))} or a {RankName(Top(table))}";
        }
    }

    private string PenaltyCard => PendingKind == Penalty.Twos ? "2" : "black jack";

    public static string Who(int seat)   => seat == 0 ? "You" : $"Seat {seat}";
    public static string Whose(int seat) => seat == 0 ? "your" : $"seat {seat}'s";

    private static readonly string[] Suits = { "Hearts", "Diamonds", "Clubs", "Spades" };
    private static readonly string[] Ranks = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "Jack", "Queen", "King", "Ace" };

    public static string SuitName(int suit) => suit >= 0 && suit < 4 ? Suits[suit] : "any suit";
    public static string RankName(int card) => card >= 0 ? Ranks[DeckEngine.GetRank(card)] : "any card";

    // ---- Persistence ----

    [Serializable]
    private class State
    {
        public string dealing;
        public int turn, pending, pendingKind, activeSuit, winner;
        public bool[] announced;
        public int[] wins;
    }

    public string SaveState() => JsonUtility.ToJson(new State
    {
        dealing = Dealing.SaveState(), turn = Turn, pending = Pending, pendingKind = (int)PendingKind,
        activeSuit = ActiveSuit, winner = Winner, announced = (bool[])_announced.Clone(), wins = (int[])_wins.Clone()
    });

    public void LoadState(string state)
    {
        State s = null;
        if (!string.IsNullOrEmpty(state))
            try { s = JsonUtility.FromJson<State>(state); } catch (ArgumentException) { }

        if (s == null || s.announced == null || s.announced.Length != Players)
        {
            Dealing.LoadState(null);
            ResetGame();
            if (s == null) _wins = new int[Players];
            return;
        }
        Dealing.LoadState(s.dealing);
        Turn        = Mathf.Clamp(s.turn, 0, Players - 1);
        Pending     = Mathf.Max(0, s.pending);
        PendingKind = Enum.IsDefined(typeof(Penalty), s.pendingKind) ? (Penalty)s.pendingKind : Penalty.None;
        ActiveSuit  = s.activeSuit;
        Winner      = s.winner;
        _announced  = (bool[])s.announced.Clone();
        _wins       = s.wins != null && s.wins.Length == Players ? (int[])s.wins.Clone() : new int[Players];
    }

    private static OperationResult Ok() => OperationResult.Ok();
    private static OperationResult Reject(string reason) => OperationResult.Reject(reason);
}
