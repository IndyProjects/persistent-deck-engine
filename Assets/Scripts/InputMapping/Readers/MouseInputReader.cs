using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class MouseInputReader : IInputReader
{
    public event Action<RawPointerEvent> PointerEvent;
    public event Action<RawAxisEvent>    AxisEvent;
    public event Action<RawButtonEvent>  ButtonEvent;

    private bool    _enabled;
    private bool    _dragging;
    private Vector2 _dragStart;

    public void Enable()  => _enabled = true;
    public void Disable() { _enabled = false; _dragging = false; }

    public void Poll()
    {
        if (!_enabled) return;

        var mouse = Mouse.current;
        if (mouse == null) return;

        double t = Time.realtimeSinceStartupAsDouble;
        Vector2 pos = mouse.position.ReadValue();

        if (mouse.leftButton.wasPressedThisFrame)
        {
            _dragging  = false;
            _dragStart = pos;
            Emit(pos, Vector2.zero, GesturePhase.Began, t);
        }
        else if (mouse.leftButton.isPressed)
        {
            Vector2 delta = mouse.delta.ReadValue();
            if (!_dragging && Vector2.Distance(pos, _dragStart) >= InputConstants.DragMinPixels)
                _dragging = true;
            if (_dragging)
                Emit(pos, delta, GesturePhase.Updated, t);
        }
        else if (mouse.leftButton.wasReleasedThisFrame)
        {
            Emit(pos, Vector2.zero, GesturePhase.Ended, t);
            _dragging = false;
        }

        Vector2 scroll = mouse.scroll.ReadValue();
        if (scroll != Vector2.zero)
        {
            AxisEvent?.Invoke(new RawAxisEvent
            {
                ActionName = "Scroll",
                Value      = scroll.y,
                Source     = InputSource.Mouse,
                Timestamp  = t
            });
        }
    }

    private void Emit(Vector2 pos, Vector2 delta, GesturePhase phase, double t)
    {
        PointerEvent?.Invoke(new RawPointerEvent
        {
            DeviceId      = 0,
            Position      = pos,
            DeltaPosition = delta,
            Pressure      = 1f,
            Phase         = phase,
            Timestamp     = t,
            Source        = InputSource.Mouse
        });
    }
}
