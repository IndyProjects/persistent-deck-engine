using System;

public interface IGestureRecognizer
{
    GestureType Handles { get; }

    /// While true, pointer events are routed only to this recognizer (e.g. mid-riffle taps).
    bool CapturesPointer { get; }

    void OnPointerEvent(RawPointerEvent e);
    void OnAxisEvent(RawAxisEvent e);
    void OnButtonEvent(RawButtonEvent e);

    event Action<GestureEvent> GestureRecognized;
}
