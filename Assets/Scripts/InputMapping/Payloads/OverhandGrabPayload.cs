/// Payload for the grab phase, 0–1 into the deck (0 = top, 1 = bottom).
public struct OverhandGrabPayload : IGesturePayload
{
    public float NormalizedDepth;
}
