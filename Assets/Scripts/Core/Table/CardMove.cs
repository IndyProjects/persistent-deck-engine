/// The single primitive that changes where cards are. Everything — dealing, playing, cutting,
/// shuffling, collecting, flipping — is a sequence of CardMoves.
///
/// Semantics (index 0 = top of a stack / first card of a hand):
///   1. Remove <see cref="Count"/> contiguous cards starting at <see cref="FromIndex"/> in <see cref="From"/>.
///   2. If <see cref="Flip"/>, turn the packet over: its order reverses and every card's face toggles.
///   3. Insert the packet at <see cref="ToIndex"/> in <see cref="To"/>, measured AFTER the removal
///      (this matters when From == To, e.g. reordering a hand or flipping a card in place).
///
/// <see cref="Bottom"/> may be used for FromIndex (take the bottom Count cards) or ToIndex (place at bottom).
/// CardTable.Apply resolves it to concrete indices, so recorded moves never contain it.
public readonly struct CardMove
{
    public const int Bottom = -1;

    public ZoneId From      { get; }
    public int    FromIndex { get; }
    public int    Count     { get; }
    public ZoneId To        { get; }
    public int    ToIndex   { get; }
    public bool   Flip      { get; }

    public CardMove(ZoneId from, int fromIndex, int count, ZoneId to, int toIndex, bool flip = false)
    {
        From      = from;
        FromIndex = fromIndex;
        Count     = count;
        To        = to;
        ToIndex   = toIndex;
        Flip      = flip;
    }

    /// Top <paramref name="count"/> cards of <paramref name="from"/> onto the top of <paramref name="to"/>.
    public static CardMove TopToTop(ZoneId from, int count, ZoneId to, bool flip = false)
        => new CardMove(from, 0, count, to, 0, flip);

    /// Top <paramref name="count"/> cards of <paramref name="from"/> to the bottom of <paramref name="to"/>.
    public static CardMove TopToBottom(ZoneId from, int count, ZoneId to, bool flip = false)
        => new CardMove(from, 0, count, to, Bottom, flip);

    /// Bottom <paramref name="count"/> cards of <paramref name="from"/> onto the top of <paramref name="to"/> (a riffle drop).
    public static CardMove BottomToTop(ZoneId from, int count, ZoneId to)
        => new CardMove(from, Bottom, count, to, 0);

    /// Turns over the single card at <paramref name="index"/> without moving it.
    public static CardMove FlipInPlace(ZoneId zone, int index)
        => new CardMove(zone, index, 1, zone, index, flip: true);

    /// The move that exactly undoes this one (valid only for a resolved move, i.e. no Bottom sentinels).
    public CardMove Inverse() => new CardMove(To, ToIndex, Count, From, FromIndex, Flip);

    public override string ToString()
        => $"{Count}×{From}[{FromIndex}] → {To}[{ToIndex}]{(Flip ? " flipped" : "")}";
}
