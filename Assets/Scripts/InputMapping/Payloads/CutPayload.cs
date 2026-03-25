
/// NormalizedPosition is 0–1 along the deck's logical axis. 0 is top of deck, 1 is bottom. for payload
public struct CutPayload : IGesturePayload
{
    public float NormalizedPosition;
}
