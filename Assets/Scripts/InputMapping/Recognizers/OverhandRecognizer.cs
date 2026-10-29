using System;
using UnityEngine;

/// Recognizes overhand shuffle grab/release pairs.
/// Mouse/Touch: vertical drag start = grab, drag end = release.
/// Keyboard/Gamepad: OverhandChunk button = one fixed-size chunk.
public class OverhandRecognizer : IGestureRecognizer
{
    public GestureType Handles => GestureType.OverhandGrab;
    public bool CapturesPointer => false;
    public event Action<GestureEvent> GestureRecognized;

    private bool    _dragging;
    private Vector2 _dragStart;
    private double  _dragStartTime;

    /// Grab + release are emitted together at drag end, once the drag is known to be vertical
    /// (horizontal drags belong to CutRecognizer).
    public void OnPointerEvent(RawPointerEvent e)
    {
        if (e.Phase == GesturePhase.Began)
        {
            _dragStart     = e.Position;
            _dragStartTime = e.Timestamp;
            _dragging      = false;
        }
        else if (e.Phase == GesturePhase.Updated)
        {
            if (!_dragging && Vector2.Distance(e.Position, _dragStart) >= InputConstants.DragMinPixels)
                _dragging = true;
        }
        else if (e.Phase == GesturePhase.Ended && _dragging)
        {
            _dragging = false;

            Vector2 d = e.Position - _dragStart;
            if (Mathf.Abs(d.y) <= Mathf.Abs(d.x)) return;

            GestureRecognized?.Invoke(new GestureEvent
            {
                Type      = GestureType.OverhandGrab,
                Source    = e.Source,
                Phase     = GesturePhase.Began,
                Timestamp = _dragStartTime,
                Payload   = new OverhandGrabPayload { NormalizedDepth = Mathf.Clamp01(_dragStart.y / Screen.height) }
            });
            GestureRecognized?.Invoke(new GestureEvent
            {
                Type      = GestureType.OverhandRelease,
                Source    = e.Source,
                Phase     = GesturePhase.Ended,
                Timestamp = e.Timestamp,
                Payload   = new OverhandReleasePayload { NormalizedDepth = Mathf.Clamp01(e.Position.y / Screen.height) }
            });
        }
    }

    public void OnAxisEvent(RawAxisEvent e) { }

    public void OnButtonEvent(RawButtonEvent e)
    {
        if (e.ActionName == "OverhandChunk")
        {
            // Emit a synthetic grab + release pair at fixed depth intervals
            GestureRecognized?.Invoke(new GestureEvent
            {
                Type      = GestureType.OverhandGrab,
                Source    = e.Source,
                Phase     = GesturePhase.Began,
                Timestamp = e.Timestamp,
                Payload   = new OverhandGrabPayload { NormalizedDepth = 0f }
            });
            GestureRecognized?.Invoke(new GestureEvent
            {
                Type      = GestureType.OverhandRelease,
                Source    = e.Source,
                Phase     = GesturePhase.Ended,
                Timestamp = e.Timestamp,
                Payload   = new OverhandReleasePayload
                {
                    NormalizedDepth = InputConstants.KeyboardChunkSize / (float)DeckEngine.DeckSize
                }
            });
        }
    }
}
