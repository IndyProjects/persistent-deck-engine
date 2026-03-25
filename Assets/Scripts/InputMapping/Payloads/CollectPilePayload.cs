/// Payload for one pile tap during card collection. PileIndex is the index into the current piles array.
public struct CollectPilePayload : IGesturePayload
{
    public int PileIndex;
}
