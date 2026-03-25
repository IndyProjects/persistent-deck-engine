using System;
using UnityEngine;

/// Recognizes overhand shuffle grab/release pairs.
/// Mouse/Touch: vertical drag start = grab, drag end = release.
/// Keyboard/Gamepad: OverhandChunk button = one fixed-size chunk.
public class OverhandRecognizer : IGestureRecognizer
{
    public GestureType Handles => GestureType.OverhandGrab;
    public event Action<GestureEvent> GestureRecognized;

    private bool  _grabActive;
    private float _grabNorm;
    private Vector2 _dragStart;

    public void OnPointerEvent(RawPointerEvent e)
    {
        if (e.Phase == GesturePhase.Began)
        {
            _dragStart  = e.Position;
            _grabActive = false;
        }
        else if (e.Phase == GesturePhase.Updated)
        {
            if (!_grabActive && Vector2.Distance(e.Position, _dragStart) >= InputConstants.DragMinPixels)
            {
                _grabActive = true;
                _grabNorm   = Mathf.Clamp01(e.Position.y / Screen.height);
                GestureRecognized?.Invoke(new GestureEvent
                {
                    Type      = GestureType.OverhandGrab,
                    Source    = e.Source,
                    Phase     = GesturePhase.Began,
                    Timestamp = e.Timestamp,
                    Payload   = new OverhandGrabPayload { NormalizedDepth = _grabNorm }
                });
            }
        }
        else if (e.Phase == GesturePhase.Ended && _grabActive)
        {
            float releaseNorm = Mathf.Clamp01(e.Position.y / Screen.height);
            GestureRecognized?.Invoke(new GestureEvent
            {
                Type      = GestureType.OverhandRelease,
                Source    = e.Source,
                Phase     = GesturePhase.Ended,
                Timestamp = e.Timestamp,
                Payload   = new OverhandReleasePayload { NormalizedDepth = releaseNorm }
            });
            _grabActive = false;
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
