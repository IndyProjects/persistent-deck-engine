/// Payload declaring intent to deal: player count and cards per player.
public struct DealStartPayload : IGesturePayload
{
    public int PlayerCount;
    public int CardsEachCount;
}
