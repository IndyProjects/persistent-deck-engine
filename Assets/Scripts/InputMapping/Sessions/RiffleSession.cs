using System.Collections.Generic;

/// Accumulates a riffle shuffle
public class RiffleSession : GestureSession
{
    public int         SplitIndex    { get; private set; }
    public List<int>   DropPattern   { get; } = new List<int>();
    public ShuffleSide CurrentSide   { get; private set; } = ShuffleSide.Left;
    public int         LeftRemaining { get; private set; }
    public int         RightRemaining{ get; private set; }

    public RiffleSession() : base(GestureType.RiffleStart) { }

    public override void AddEvent(GestureEvent e)
    {
        if (IsComplete || IsCancelled) return;
        Events.Add(e);

        if (e.Type == GestureType.RiffleStart && e.Payload is RiffleStartPayload start)
        {
            int split     = UnityEngine.Mathf.RoundToInt(start.NormalizedSplitPosition * 51f);
            SplitIndex    = UnityEngine.Mathf.Clamp(split, 1, 51);
            LeftRemaining = SplitIndex;
            RightRemaining= DeckEngine.DeckSize - SplitIndex;
            CurrentSide   = ShuffleSide.Left;
            return;
        }

        if (e.Type == GestureType.RiffleDrop && e.Payload is RiffleDropPayload drop)
        {
            if (drop.Side != CurrentSide)
            {
                Cancel(); // side out of alternation order
                return;
            }

            int count = UnityEngine.Mathf.Max(1, drop.CardCount);

            if (CurrentSide == ShuffleSide.Left)
            {
                count          = UnityEngine.Mathf.Min(count, LeftRemaining);
                LeftRemaining -= count;
            }
            else
            {
                count           = UnityEngine.Mathf.Min(count, RightRemaining);
                RightRemaining -= count;
            }

            DropPattern.Add(count);
            CurrentSide = (CurrentSide == ShuffleSide.Left) ? ShuffleSide.Right : ShuffleSide.Left;

            if (LeftRemaining == 0 && RightRemaining == 0)
                Complete();
        }
    }
}
