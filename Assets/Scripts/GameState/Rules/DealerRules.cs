using System;

public enum RoundPhase
{
    Gathered,  // Whole deck squared on the table: the dealer may pick it up
    Shuffling, // Dealer holds the whole deck: shuffle, put it down, or start dealing
    Dealing,   // Dealing has begun: only dealing packets is allowed until every seat is full
    Dealt,     // Every seat has its cards: play, put the stub down, collect
    Playing    // Cards have moved beyond a clean deal: play and collect
}

/// A dealing round as at a real table. The dealer picks the deck up, shuffles it in hand, then deals
/// packets of any size — one at a time, two at a time, any rhythm — clockwise starting left of the
/// dealer, until every player holds <see cref="CardsPerPlayer"/>. Once the first packet is dealt the
/// deck can't be shuffled again and nothing else may happen until the deal is complete.
///
/// The phase is read from the table itself; only whose turn it is to receive a packet is remembered
/// (and saved for undo and persistence).
public sealed class DealerRules : IStatefulRuleSet, IDealingRuleSet
{
    public int    Players        { get; }
    public int    CardsPerPlayer { get; }
    public int    Dealer         { get; }
    /// The dealer holds the deck in their right hand while shuffling and dealing.
    public ZoneId DealerHand     => ZoneId.RightHand;

    DealerRules IDealingRuleSet.Dealing => this;

    /// Seat that receives the next packet.
    public int NextSeat { get; private set; }

    /// Dealing starts with the player to the dealer's left.
    public int FirstSeat => (Dealer + 1) % Players;

    public string Name => $"Deal {CardsPerPlayer} to {Players}";

    public DealerRules(int players = 4, int cardsPerPlayer = 5, int dealer = 0)
    {
        if (players < 1) throw new ArgumentOutOfRangeException(nameof(players));
        if (cardsPerPlayer < 1) throw new ArgumentOutOfRangeException(nameof(cardsPerPlayer));
        if (players * cardsPerPlayer > DeckEngine.DeckSize)
            throw new ArgumentException($"{players} × {cardsPerPlayer} cards exceeds the deck.");
        if (dealer < 0 || dealer >= players) throw new ArgumentOutOfRangeException(nameof(dealer));

        Players        = players;
        CardsPerPlayer = cardsPerPlayer;
        Dealer         = dealer;
        NextSeat       = FirstSeat;
    }

    /// The packet the dealer would deal next if they chose <paramref name="count"/> cards.
    public DealPacketOperation NextPacket(int count) => new DealPacketOperation(NextSeat, count, DealerHand);

    private int _packetSize = 1;

    /// The dealer's chosen rhythm (cards per packet), shared by every control that deals. 1…CardsPerPlayer.
    public int PacketSize
    {
        get => _packetSize;
        set => _packetSize = Math.Max(1, Math.Min(CardsPerPlayer, value));
    }

    /// The next packet at the dealer's chosen size, trimmed to what the next seat still needs.
    public DealPacketOperation NextPacket(CardTable table)
        => NextPacket(Math.Max(1, Math.Min(PacketSize, Remaining(table, NextSeat))));

    /// Cards a seat still needs to complete the deal.
    public int Remaining(CardTable table, int seat) => Math.Max(0, CardsPerPlayer - table.Count(ZoneId.Hand(seat)));

    public RoundPhase PhaseOf(CardTable table)
    {
        if (table.Count(ZoneId.Deck) == DeckEngine.DeckSize) return RoundPhase.Gathered;
        if (table.Count(DealerHand)  == DeckEngine.DeckSize) return RoundPhase.Shuffling;
        if (!IsCleanDeal(table)) return RoundPhase.Playing;

        bool allFull = true;
        for (int seat = 0; seat < Players; seat++)
            if (Remaining(table, seat) > 0) allFull = false;
        if (allFull) return RoundPhase.Dealt;

        // Part-dealt with the deck still in hand is a deal in progress; anything else is free-form play
        return table.Count(DealerHand) > 0 && table.Count(ZoneId.Deck) == 0 ? RoundPhase.Dealing : RoundPhase.Playing;
    }

    public OperationResult Check(GameOperation op, CardTable table)
    {
        var phase = PhaseOf(table);
        switch (op)
        {
            case PickUpDeckOperation pickUp:
                if (phase != RoundPhase.Gathered) return Reject("Pick the deck up when it is squared on the table.");
                return pickUp.Hand == DealerHand ? Ok() : Reject("The dealer picks the deck up in their right hand.");

            case CutOperation cut:         return CheckShuffle(phase, cut.Source);
            case CutSectionOperation sec:  return CheckShuffle(phase, sec.Source);
            case RiffleOperation riffle:   return CheckShuffle(phase, riffle.Source);
            case OverhandOperation over:   return CheckShuffle(phase, over.Source);

            case DealPacketOperation deal:
                if (phase == RoundPhase.Gathered) return Reject("Pick up the deck before dealing.");
                if (phase != RoundPhase.Shuffling && phase != RoundPhase.Dealing) return Reject("The deal is finished.");
                if (deal.From != DealerHand) return Reject("Deal from the deck in the dealer's hand.");
                if (deal.Seat != NextSeat) return Reject($"Seat {NextSeat} is next to be dealt to.");
                int remaining = Remaining(table, deal.Seat);
                if (deal.Count > remaining)
                    return Reject($"Seat {deal.Seat} only needs {remaining} more card{(remaining == 1 ? "" : "s")}.");
                return Ok();

            case PutDownDeckOperation _:
                if (phase == RoundPhase.Dealing) return Reject("Finish dealing before putting the deck down.");
                return Ok();

            case CollectOperation _:
                if (phase == RoundPhase.Gathered) return Reject("Nothing to collect.");
                if (phase == RoundPhase.Shuffling) return Reject("Put the deck down instead.");
                if (phase == RoundPhase.Dealing) return Reject("Finish dealing first.");
                return Ok();

            case MoveCardOperation _:
            case FlipCardOperation _:
            case MovePacketOperation _:
                return phase == RoundPhase.Dealt || phase == RoundPhase.Playing
                    ? Ok()
                    : Reject(phase == RoundPhase.Dealing ? "Finish dealing first." : "Deal before playing cards.");

            case DealOperation _:
                return Reject("Deal packet by packet: choose how many cards go to each player at a time.");

            default:
                return Reject($"{op.Name} isn't part of this game.");
        }
    }

    private OperationResult CheckShuffle(RoundPhase phase, ZoneId source)
    {
        switch (phase)
        {
            case RoundPhase.Gathered: return Reject("Pick up the deck to shuffle it.");
            case RoundPhase.Shuffling:
                return source == DealerHand ? Ok() : Reject("Shuffle the deck in your hand.");
            case RoundPhase.Dealing:  return Reject("Dealing has started: no more shuffling. Finish the deal.");
            default:                  return Reject("Collect the cards before shuffling.");
        }
    }

    public void OnApplied(GameOperation op, CardTable table)
    {
        switch (op)
        {
            case DealPacketOperation _:
                // Next seat clockwise that still needs cards (deal is complete when none do)
                for (int step = 1; step <= Players; step++)
                {
                    int seat = (NextSeat + step) % Players;
                    if (Remaining(table, seat) > 0)
                    {
                        NextSeat = seat;
                        return;
                    }
                }
                NextSeat = FirstSeat;
                break;

            case PickUpDeckOperation _:
            case CollectOperation _:
                NextSeat = FirstSeat;
                break;
        }
    }

    public string Describe(CardTable table)
    {
        switch (PhaseOf(table))
        {
            case RoundPhase.Gathered:  return "Pick up the deck";
            case RoundPhase.Shuffling: return $"Shuffling · deal starts at seat {NextSeat}";
            case RoundPhase.Dealing:   return $"Dealing · seat {NextSeat} needs {Remaining(table, NextSeat)}";
            case RoundPhase.Dealt:     return "Dealt · play or collect";
            default:                   return "Playing · collect when done";
        }
    }

    public string SaveState() => NextSeat.ToString();

    public void LoadState(string state)
        => NextSeat = int.TryParse(state, out int seat) && seat >= 0 && seat < Players ? seat : FirstSeat;

    /// The deal so far: hands only for seats in play, each within its quota; held cards only in the
    /// dealer's hand; nothing on the table except possibly the deck.
    private bool IsCleanDeal(CardTable table)
    {
        foreach (var zone in table.NonEmptyZones())
            switch (zone.Kind)
            {
                case ZoneKind.Deck: break;
                case ZoneKind.Hand:
                    if (zone.Index >= Players || table.Count(zone) > CardsPerPlayer) return false;
                    break;
                case ZoneKind.Held:
                    if (zone != DealerHand) return false;
                    break;
                default:
                    return false;
            }
        return true;
    }

    private static OperationResult Ok() => OperationResult.Ok();
    private static OperationResult Reject(string reason) => OperationResult.Reject(reason);
}
