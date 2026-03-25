using System;
using UnityEngine;

/// Recognizes RiffleStart (split) and RiffleDrop (interleave) gestures.
/// Keyboard: S key emits RiffleStart; RiffleDrop axis emits a drop.
/// Gamepad: RiffleStart axis, RiffleDropLeft/Right axes.
/// Mouse/Touch: drag start = RiffleStart; subsequent taps = RiffleDrop alternating.
public class RiffleRecognizer : IGestureRecognizer
{
    public GestureType Handles => GestureType.RiffleStart;
    public event Action<GestureEvent> GestureRecognized;

    private bool       _riffleActive;
    private ShuffleSide _nextSide = ShuffleSide.Left;
    private Vector2    _dragStart;

    public void OnPointerEvent(RawPointerEvent e)
    {
        if (e.Phase == GesturePhase.Began)
        {
            _dragStart = e.Position;
        }
        else if (e.Phase == GesturePhase.Ended)
        {
            float dist = Vector2.Distance(e.Position, _dragStart);
            float norm = Mathf.Clamp01(e.Position.x / Screen.width);

            if (!_riffleActive && dist >= InputConstants.DragMinPixels)
            {
                EmitStart(norm, e.Source, e.Timestamp);
            }
            else if (_riffleActive && dist < InputConstants.DragMinPixels)
            {
                // Short tap = drop
                EmitDrop(e.Source, e.Timestamp);
            }
        }
    }

    public void OnAxisEvent(RawAxisEvent e)
    {
        switch (e.ActionName)
        {
            case "RiffleStart":
            {
                float norm = Mathf.Clamp01(e.Value);
                if (!float.IsNaN(norm)) EmitStart(norm, e.Source, e.Timestamp);
                break;
            }
            case "RiffleDrop":
            {
                int count = Mathf.Max(1, Mathf.RoundToInt(e.Value));
                EmitDropWithCount(_nextSide, count, e.Source, e.Timestamp);
                break;
            }
            case "RiffleDropLeft":
                EmitDropWithCount(ShuffleSide.Left, 1, e.Source, e.Timestamp);
                break;
            case "RiffleDropRight":
                EmitDropWithCount(ShuffleSide.Right, 1, e.Source, e.Timestamp);
                break;
        }
    }

    public void OnButtonEvent(RawButtonEvent e)
    {
        if (e.ActionName == "RiffleStart")
            EmitStart(0.5f, e.Source, e.Timestamp); // default center split
    }

    private void EmitStart(float norm, InputSource src, double t)
    {
        _riffleActive = true;
        _nextSide     = ShuffleSide.Left;
        GestureRecognized?.Invoke(new GestureEvent
        {
            Type      = GestureType.RiffleStart,
            Source    = src,
            Phase     = GesturePhase.Began,
            Timestamp = t,
            Payload   = new RiffleStartPayload { NormalizedSplitPosition = norm }
        });
    }

    private void EmitDrop(InputSource src, double t)
        => EmitDropWithCount(_nextSide, 1, src, t);

    private void EmitDropWithCount(ShuffleSide side, int count, InputSource src, double t)
    {
        if (!_riffleActive) return;
        _nextSide = (side == ShuffleSide.Left) ? ShuffleSide.Right : ShuffleSide.Left;
        GestureRecognized?.Invoke(new GestureEvent
        {
            Type      = GestureType.RiffleDrop,
            Source    = src,
            Phase     = GesturePhase.Ended,
            Timestamp = t,
            Payload   = new RiffleDropPayload { Side = side, CardCount = count }
        });
    }
}
