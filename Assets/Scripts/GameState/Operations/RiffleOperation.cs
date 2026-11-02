using System;
using System.Collections.Generic;

/// Physical riffle: the top <see cref="SplitIndex"/> cards form the left packet and the rest the right,
/// then cards fall from the BOTTOM of each packet, alternating left/right per <see cref="DropPattern"/>
/// (one card per drop once the pattern runs out). For a full deck the result matches
/// DeckEngine.RiffleShuffle exactly.
///
/// From the table (<see cref="Source"/> = Deck) both packets are lifted into the hands and fall back
/// onto the deck's spot. From a held packet, the deck is split between the two hands, riffled onto the
/// table and picked back up into the same hand — so the table's deck spot must be clear.
public sealed class RiffleOperation : GameOperation
{
    public int    SplitIndex  { get; }
    public int[]  DropPattern { get; }
    public ZoneId Source      { get; }

    public RiffleOperation(int splitIndex, int[] dropPattern) : this(splitIndex, dropPattern, ZoneId.Deck) { }

    public RiffleOperation(int splitIndex, int[] dropPattern, ZoneId source)
    {
        SplitIndex  = splitIndex;
        DropPattern = dropPattern;
        Source      = source;
    }

    /// Builds a drop pattern from individual drops in the order they fell (true = left hand).
    /// Consecutive drops from one hand merge into one count, and a 0 is inserted when the right hand
    /// drops first, so the pattern always starts with the left hand as Execute expects.
    public static int[] PatternFromDrops(IEnumerable<bool> dropsFromLeft)
    {
        var pattern = new List<int>();
        bool side = true; // left
        int count = 0;
        foreach (bool left in dropsFromLeft)
        {
            if (left != side)
            {
                pattern.Add(count);
                side  = left;
                count = 0;
            }
            count++;
        }
        pattern.Add(count);
        return pattern.ToArray();
    }

    public override void Execute(TableTransaction tx)
    {
        var table = tx.Table;
        int n = table.Count(Source);
        if (SplitIndex < 1 || SplitIndex > n - 1)
            throw new ArgumentOutOfRangeException(nameof(SplitIndex), $"Split must be between 1 and {n - 1}.");
        if (DropPattern == null || DropPattern.Length == 0)
            throw new ArgumentException("DropPattern must not be empty.", nameof(DropPattern));
        foreach (int drop in DropPattern)
            if (drop < 0) throw new ArgumentException("DropPattern entries must be >= 0.", nameof(DropPattern));
        RequireShuffleSource(table, Source);

        bool held = Source != ZoneId.Deck;
        if (held && table.Count(ZoneId.Deck) > 0)
            throw new InvalidOperationException("Clear the deck's spot on the table before riffling onto it.");

        // Split: left hand gets the top packet, right hand the rest
        if (!held)
        {
            tx.Move(CardMove.TopToTop(ZoneId.Deck, SplitIndex,     ZoneId.LeftHand));
            tx.Move(CardMove.TopToTop(ZoneId.Deck, n - SplitIndex, ZoneId.RightHand));
        }
        else if (Source == ZoneId.RightHand)
            tx.Move(CardMove.TopToTop(ZoneId.RightHand, SplitIndex, ZoneId.LeftHand));
        else
            tx.Move(new CardMove(ZoneId.LeftHand, SplitIndex, n - SplitIndex, ZoneId.RightHand, 0));

        // Interleave onto the table
        bool fromLeft = true;
        for (int i = 0; table.Count(ZoneId.LeftHand) > 0 || table.Count(ZoneId.RightHand) > 0; i++)
        {
            var hand   = fromLeft ? ZoneId.LeftHand : ZoneId.RightHand;
            int take   = i < DropPattern.Length ? DropPattern[i] : 1;
            int actual = Math.Min(take, table.Count(hand));
            if (actual > 0)
                tx.Move(CardMove.BottomToTop(hand, actual, ZoneId.Deck));
            fromLeft = !fromLeft;
        }

        // Square up and lift back into the hand it came from
        if (held)
            tx.Move(CardMove.TopToTop(ZoneId.Deck, n, Source));
    }
}
