using System;

public enum ZoneKind
{
    Deck,    // The main stack. Shuffles and deals operate on it.
    Hand,    // A player's hand. Index = seat.
    Pile,    // Any stack or spread on the table (tricks, community cards, cut halves). Index = free choice.
    Discard, // Discard pile(s). Index = free choice, usually 0.
    Held     // A packet held while shuffling. Index 0 = left hand, 1 = right hand.
}

/// Names a place a card can be. Zones exist implicitly: any ZoneId is valid and is simply empty
/// until cards are moved into it, so moves never have to create or destroy zones.
public readonly struct ZoneId : IEquatable<ZoneId>, IComparable<ZoneId>
{
    public ZoneKind Kind  { get; }
    public int      Index { get; }

    public ZoneId(ZoneKind kind, int index = 0)
    {
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(index), "Zone index must be >= 0.");
        Kind  = kind;
        Index = index;
    }

    public static readonly ZoneId Deck      = new ZoneId(ZoneKind.Deck);
    public static readonly ZoneId LeftHand  = new ZoneId(ZoneKind.Held, 0);
    public static readonly ZoneId RightHand = new ZoneId(ZoneKind.Held, 1);

    public static ZoneId Hand(int seat)     => new ZoneId(ZoneKind.Hand, seat);
    public static ZoneId Pile(int index)    => new ZoneId(ZoneKind.Pile, index);
    public static ZoneId Discard(int index = 0) => new ZoneId(ZoneKind.Discard, index);

    public bool Equals(ZoneId other) => Kind == other.Kind && Index == other.Index;
    public override bool Equals(object obj) => obj is ZoneId other && Equals(other);
    public override int GetHashCode() => ((int)Kind * 397) ^ Index;
    public static bool operator ==(ZoneId a, ZoneId b) => a.Equals(b);
    public static bool operator !=(ZoneId a, ZoneId b) => !a.Equals(b);

    /// Orders by kind then index, giving zones a stable canonical order (used for collecting and saving).
    public int CompareTo(ZoneId other)
        => Kind != other.Kind ? Kind.CompareTo(other.Kind) : Index.CompareTo(other.Index);

    public override string ToString() => Kind == ZoneKind.Deck ? "Deck" : $"{Kind}/{Index}";
}
