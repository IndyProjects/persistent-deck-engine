using System;

public interface IGestureRecognizer
{
    GestureType Handles { get; }

    void OnPointerEvent(RawPointerEvent e);
    void OnAxisEvent(RawAxisEvent e);
    void OnButtonEvent(RawButtonEvent e);

    event Action<GestureEvent> GestureRecognized;
}
