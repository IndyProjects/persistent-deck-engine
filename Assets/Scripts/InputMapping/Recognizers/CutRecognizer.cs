using System;
using UnityEngine;

/// Recognizes a Cut gesture from mouse/touch drag, keyboard [ / ] + Enter, or gamepad CutCommit axis.
public class CutRecognizer : IGestureRecognizer
{
    public GestureType Handles => GestureType.Cut;
    public event Action<GestureEvent> GestureRecognized;

    private Vector2 _dragStart;
    private bool    _dragging;

    public void OnPointerEvent(RawPointerEvent e)
    {
        if (e.Phase == GesturePhase.Began)
        {
            _dragStart = e.Position;
            _dragging  = false;
        }
        else if (e.Phase == GesturePhase.Updated)
        {
            if (!_dragging && Vector2.Distance(e.Position, _dragStart) >= InputConstants.DragMinPixels)
                _dragging = true;
        }
        else if (e.Phase == GesturePhase.Ended && _dragging)
        {
            float norm = Mathf.Clamp01(e.Position.x / Screen.width);
            Emit(norm, e.Source, e.Timestamp);
            _dragging = false;
        }
    }

    public void OnAxisEvent(RawAxisEvent e)
    {
        if (e.ActionName == "CutCommit")
        {
            float norm = Mathf.Clamp01(e.Value);
            if (!float.IsNaN(norm) && !float.IsInfinity(norm))
                Emit(norm, e.Source, e.Timestamp);
        }
    }

    public void OnButtonEvent(RawButtonEvent e) { }

    private void Emit(float norm, InputSource src, double t)
    {
        GestureRecognized?.Invoke(new GestureEvent
        {
            Type      = GestureType.Cut,
            Source    = src,
            Phase     = GesturePhase.Ended,
            Timestamp = t,
            Payload   = new CutPayload { NormalizedPosition = norm }
        });
    }
}
