using UnityEngine;

public struct RawPointerEvent
{
    public int         DeviceId;
    public Vector2     Position;
    public Vector2     DeltaPosition;
    public float       Pressure;
    public GesturePhase Phase;
    public double      Timestamp;
    public InputSource Source;
}
