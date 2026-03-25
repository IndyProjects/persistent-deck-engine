public struct GestureEvent
{
    public GestureType     Type;
    public InputSource     Source;
    public GesturePhase    Phase;
    public double          Timestamp;
    public IGesturePayload Payload;
}
