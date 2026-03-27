using System;

public interface IInputReader
{
    event Action<RawPointerEvent> PointerEvent;
    event Action<RawAxisEvent>    AxisEvent;
    event Action<RawButtonEvent>  ButtonEvent;

    void Enable();
    void Disable();
    void Poll();
}
