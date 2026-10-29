using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch      = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

/// Reads touch input via the Input System EnhancedTouch API.
/// Requires EnhancedTouchSupport.Enable() before use (called by InputCoordinator.Awake).
public class TouchInputReader : IInputReader
{
    public event Action<RawPointerEvent> PointerEvent;
    public event Action<RawAxisEvent>    AxisEvent;
    public event Action<RawButtonEvent>  ButtonEvent;

    private bool _enabled;

    // Fingers whose touch began on a UI control — ignored until they lift
    private readonly HashSet<int> _uiFingers = new HashSet<int>();

    public void Enable()  => _enabled = true;
    public void Disable() { _enabled = false; _uiFingers.Clear(); }

    /// Call each frame from InputCoordinator.Update().
    public void Poll()
    {
        if (!_enabled) return;
        if (!EnhancedTouchSupport.enabled) return;

        foreach (var touch in Touch.activeTouches)
        {
            GesturePhase phase = touch.phase switch
            {
                TouchPhase.Began     => GesturePhase.Began,
                TouchPhase.Moved     => GesturePhase.Updated,
                TouchPhase.Stationary=> GesturePhase.Updated,
                TouchPhase.Ended     => GesturePhase.Ended,
                TouchPhase.Canceled  => GesturePhase.Cancelled,
                _                    => GesturePhase.Updated
            };

            int finger = touch.finger.index;
            if (phase == GesturePhase.Began && PointerUIFilter.IsOverInteractableUI(touch.screenPosition))
                _uiFingers.Add(finger);
            if (_uiFingers.Contains(finger))
            {
                if (phase == GesturePhase.Ended || phase == GesturePhase.Cancelled)
                    _uiFingers.Remove(finger);
                continue;
            }

            float pressure = touch.pressure;
            if (pressure < InputConstants.PressureMinimum) pressure = 1f; // fallback for devices without pressure

            PointerEvent?.Invoke(new RawPointerEvent
            {
                DeviceId      = finger,
                Position      = touch.screenPosition,
                DeltaPosition = touch.delta,
                Pressure      = pressure,
                Phase         = phase,
                Timestamp     = (double)touch.time,
                Source        = InputSource.Touch
            });
        }
    }
}
