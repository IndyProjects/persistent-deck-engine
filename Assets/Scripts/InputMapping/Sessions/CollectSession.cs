using System.Collections.Generic;

/// Accumulates pile taps in player-specified order for CollectCards.
public class CollectSession : GestureSession
{
    public List<int>   PileOrder  { get; } = new List<int>();
    public int         TotalPiles { get; }

    private readonly HashSet<int> _seen = new HashSet<int>();

    public CollectSession(int totalPiles) : base(GestureType.CollectPile)
        => TotalPiles = totalPiles;

    public override void AddEvent(GestureEvent e)
    {
        if (IsComplete || IsCancelled) return;
        Events.Add(e);

        if (e.Type == GestureType.CollectPile && e.Payload is CollectPilePayload tap)
        {
            if (tap.PileIndex < 0 || tap.PileIndex >= TotalPiles)
            {
                Cancel(); // out of range
                return;
            }
            if (_seen.Contains(tap.PileIndex))
            {
                Cancel(); // duplicate pile index
                return;
            }

            _seen.Add(tap.PileIndex);
            PileOrder.Add(tap.PileIndex);

            if (PileOrder.Count == TotalPiles)
                Complete();
        }
    }
}
