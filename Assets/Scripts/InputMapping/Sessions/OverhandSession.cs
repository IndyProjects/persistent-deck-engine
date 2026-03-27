using System.Collections.Generic;

/// Accumulates overhand shuffle chunk sizes from grab/release pairs.
public class OverhandSession : GestureSession
{
    public List<int> ChunkSizes     { get; } = new List<int>();
    public int       CardsRemaining { get; private set; } = DeckEngine.DeckSize;

    private float _grabDepth = -1f;

    public OverhandSession() : base(GestureType.OverhandGrab) { }

    public override void AddEvent(GestureEvent e)
    {
        base.AddEvent(e);
        if (IsComplete || IsCancelled) return;

        if (e.Type == GestureType.OverhandGrab && e.Payload is OverhandGrabPayload grab)
        {
            _grabDepth = grab.NormalizedDepth;
            return;
        }

        if (e.Type == GestureType.OverhandRelease && e.Payload is OverhandReleasePayload release)
        {
            if (_grabDepth < 0f)
            {
                Cancel(); // release without preceding grab
                return;
            }

            int chunk = UnityEngine.Mathf.RoundToInt(
                UnityEngine.Mathf.Abs(release.NormalizedDepth - _grabDepth) * DeckEngine.DeckSize);
            chunk = UnityEngine.Mathf.Max(1, chunk);
            chunk = UnityEngine.Mathf.Min(chunk, CardsRemaining);

            ChunkSizes.Add(chunk);
            CardsRemaining -= chunk;
            _grabDepth      = -1f;

            if (CardsRemaining == 0)
                Complete();
        }
    }

    /// Called by keyboard/gamepad readers to add a fixed-size chunk.
    public void AddFixedChunk(int size)
    {
        if (IsComplete || IsCancelled || size <= 0) return;
        size = UnityEngine.Mathf.Min(size, CardsRemaining);
        ChunkSizes.Add(size);
        CardsRemaining -= size;
        if (CardsRemaining == 0)
            Complete();
    }
}
