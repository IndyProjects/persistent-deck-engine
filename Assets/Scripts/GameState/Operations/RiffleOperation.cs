using System;

/// Physical riffle: the top <see cref="SplitIndex"/> cards go to the left hand, the rest to the right,
/// then cards fall from the BOTTOM of each hand onto the deck, alternating left/right per
/// <see cref="DropPattern"/> (one card per drop once the pattern runs out).
/// For a full deck the result matches DeckEngine.RiffleShuffle exactly.
public sealed class RiffleOperation : GameOperation
{
    public int   SplitIndex  { get; }
    public int[] DropPattern { get; }

    public RiffleOperation(int splitIndex, int[] dropPattern)
    {
        SplitIndex  = splitIndex;
        DropPattern = dropPattern;
    }

    public override void Execute(TableTransaction tx)
    {
        var table = tx.Table;
        int n = table.Count(ZoneId.Deck);
        if (SplitIndex < 1 || SplitIndex > n - 1)
            throw new ArgumentOutOfRangeException(nameof(SplitIndex), $"Split must be between 1 and {n - 1}.");
        if (DropPattern == null || DropPattern.Length == 0)
            throw new ArgumentException("DropPattern must not be empty.", nameof(DropPattern));
        foreach (int drop in DropPattern)
            if (drop < 0) throw new ArgumentException("DropPattern entries must be >= 0.", nameof(DropPattern));
        RequireEmptyHands(table);

        tx.Move(CardMove.TopToTop(ZoneId.Deck, SplitIndex,     ZoneId.LeftHand));
        tx.Move(CardMove.TopToTop(ZoneId.Deck, n - SplitIndex, ZoneId.RightHand));

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
    }
}
