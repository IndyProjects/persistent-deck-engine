using System;
using System.Collections.Generic;

/// Authoritative record of where every one of the 52 cards is: which zone, at which position,
/// and whether it is face up. Pure logic, no Unity dependencies.
///
/// Invariant: each card 0–51 is in exactly one zone, exactly once. Every mutation goes through
/// <see cref="Apply"/>, which validates the move fully before touching any state.
public class CardTable
{
    private readonly Dictionary<ZoneId, List<int>> _zones = new Dictionary<ZoneId, List<int>>();
    private readonly ZoneId[] _location = new ZoneId[DeckEngine.DeckSize];
    private readonly bool[]   _faceUp   = new bool[DeckEngine.DeckSize];

    private static readonly IReadOnlyList<int> Empty = Array.Empty<int>();

    /// A table with a fresh ordered deck, all face down.
    public CardTable() : this(DeckEngine.CreateOrderedDeck()) { }

    /// A table with the given 52-card order in the deck, all face down.
    public CardTable(int[] deckOrder)
    {
        if (!DeckEngine.ValidateDeck(deckOrder))
            throw new ArgumentException("deckOrder must contain each card 0–51 exactly once.", nameof(deckOrder));
        SetZoneUnchecked(ZoneId.Deck, deckOrder);
    }

    /// Empty table for internal construction; callers must fill all 52 cards.
    private CardTable(bool _) { }

    /// Builds a table from arbitrary zone contents (used by loading). Throws unless all 52 cards appear exactly once.
    public static CardTable FromZones(IEnumerable<KeyValuePair<ZoneId, int[]>> zones, IEnumerable<int> faceUpCards)
    {
        var table = new CardTable(false);

        var all = new List<int>(DeckEngine.DeckSize);
        foreach (var kv in zones)
        {
            if (kv.Value == null || kv.Value.Length == 0) continue;
            if (table._zones.ContainsKey(kv.Key))
                throw new ArgumentException($"Zone {kv.Key} listed twice.");
            all.AddRange(kv.Value);
            table.SetZoneUnchecked(kv.Key, kv.Value);
        }
        if (!DeckEngine.ValidateDeck(all.ToArray()))
            throw new ArgumentException("Zones must contain each card 0–51 exactly once.");

        if (faceUpCards != null)
            foreach (int card in faceUpCards)
            {
                if (card < 0 || card >= DeckEngine.DeckSize)
                    throw new ArgumentOutOfRangeException(nameof(faceUpCards), $"Card {card} out of range.");
                table._faceUp[card] = true;
            }
        return table;
    }

    public CardTable Clone()
    {
        var copy = new CardTable(false);
        foreach (var kv in _zones)
            copy.SetZoneUnchecked(kv.Key, kv.Value);
        Array.Copy(_faceUp, copy._faceUp, _faceUp.Length);
        return copy;
    }

    // ---- Queries ----

    /// Cards in a zone, index 0 = top. Unknown or empty zones return an empty list.
    public IReadOnlyList<int> Cards(ZoneId zone)
        => _zones.TryGetValue(zone, out var list) ? list : Empty;

    public int Count(ZoneId zone) => _zones.TryGetValue(zone, out var list) ? list.Count : 0;

    /// All zones currently holding at least one card, in canonical order.
    public List<ZoneId> NonEmptyZones()
    {
        var result = new List<ZoneId>(_zones.Keys);
        result.Sort();
        return result;
    }

    public ZoneId LocationOf(int card) => _location[CheckCard(card)];
    public int    IndexOf(int card)    => _zones[_location[CheckCard(card)]].IndexOf(card);
    public bool   IsFaceUp(int card)   => _faceUp[CheckCard(card)];

    /// Highest occupied hand seat + 1 (0 when no hand holds cards).
    public int HandCount()
    {
        int max = -1;
        foreach (var zone in _zones.Keys)
            if (zone.Kind == ZoneKind.Hand && zone.Index > max) max = zone.Index;
        return max + 1;
    }

    /// True if every card is in exactly one zone, and the location index agrees with the zones.
    public bool Validate()
    {
        var all = new List<int>(DeckEngine.DeckSize);
        foreach (var kv in _zones)
        {
            if (kv.Value.Count == 0) return false; // empty zones must be pruned
            foreach (int card in kv.Value)
            {
                if (card < 0 || card >= DeckEngine.DeckSize || _location[card] != kv.Key) return false;
                all.Add(card);
            }
        }
        return DeckEngine.ValidateDeck(all.ToArray());
    }

    // ---- Mutation ----

    /// Validates and applies a move. Returns the resolved move (Bottom sentinels replaced by concrete
    /// indices), which is what should be recorded: its Inverse() exactly undoes it.
    /// Throws ArgumentException without changing state if the move is impossible.
    public CardMove Apply(CardMove move)
    {
        var resolved = Resolve(move);

        var source = _zones.TryGetValue(resolved.From, out var s) ? s : null;
        var packet = source.GetRange(resolved.FromIndex, resolved.Count);
        source.RemoveRange(resolved.FromIndex, resolved.Count);
        if (source.Count == 0) _zones.Remove(resolved.From);

        if (resolved.Flip)
        {
            packet.Reverse();
            foreach (int card in packet) _faceUp[card] = !_faceUp[card];
        }

        if (!_zones.TryGetValue(resolved.To, out var dest))
        {
            dest = new List<int>();
            _zones[resolved.To] = dest;
        }
        dest.InsertRange(resolved.ToIndex, packet);
        foreach (int card in packet) _location[card] = resolved.To;

        return resolved;
    }

    /// Checks a move against the current state and returns it with concrete indices.
    public CardMove Resolve(CardMove move)
    {
        int sourceCount = Count(move.From);
        if (move.Count < 1)
            throw new ArgumentException($"Move count must be at least 1 (got {move.Count}).");
        if (move.Count > sourceCount)
            throw new ArgumentException($"Cannot take {move.Count} card(s) from {move.From}: it holds {sourceCount}.");

        int fromIndex = move.FromIndex == CardMove.Bottom ? sourceCount - move.Count : move.FromIndex;
        if (fromIndex < 0 || fromIndex + move.Count > sourceCount)
            throw new ArgumentException($"Cards {fromIndex}..{fromIndex + move.Count - 1} are outside {move.From} (holds {sourceCount}).");

        // Destination size measured after the removal
        int destCount = Count(move.To) - (move.To == move.From ? move.Count : 0);
        int toIndex   = move.ToIndex == CardMove.Bottom ? destCount : move.ToIndex;
        if (toIndex < 0 || toIndex > destCount)
            throw new ArgumentException($"Insert position {toIndex} is outside {move.To} (holds {destCount}).");

        return new CardMove(move.From, fromIndex, move.Count, move.To, toIndex, move.Flip);
    }

    private void SetZoneUnchecked(ZoneId zone, IEnumerable<int> cards)
    {
        var list = new List<int>(cards);
        _zones[zone] = list;
        foreach (int card in list) _location[card] = zone;
    }

    private static int CheckCard(int card)
    {
        if (card < 0 || card >= DeckEngine.DeckSize)
            throw new ArgumentOutOfRangeException(nameof(card), $"Card {card} is not in 0–51.");
        return card;
    }
}
