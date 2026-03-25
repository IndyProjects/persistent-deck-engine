/// Payload for one drop group during a riffle shuffle.
public struct RiffleDropPayload : IGesturePayload
{
    public ShuffleSide Side;
    public int         CardCount;
}
