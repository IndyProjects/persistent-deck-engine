using System;
using UnityEngine;

/// Recognizes RiffleStart (split) and RiffleDrop (interleave) gestures.
/// Keyboard: S key emits RiffleStart; RiffleDrop axis emits a drop.
/// Gamepad: RiffleStart axis, RiffleDropLeft/Right axes.
/// Mouse/Touch: long press (no drag) = RiffleStart at that x; subsequent short taps = RiffleDrop alternating.
/// While a riffle is active this recognizer captures the pointer so taps can't trigger cuts/overhands.
public class RiffleRecognizer : IGestureRecognizer
{
    public GestureType Handles => GestureType.RiffleStart;
    public bool CapturesPointer => _riffleActive;
    public event Action<GestureEvent> GestureRecognized;

    private bool        _riffleActive;
    private ShuffleSide _nextSide = ShuffleSide.Left;
    private Vector2     _pressStart;
    private double      _pressStartTime;
    private bool        _pressMoved;

    /// Called by InputCoordinator when the riffle session completes or is abandoned.
    public void EndRiffle()
    {
        _riffleActive = false;
        _nextSide     = ShuffleSide.Left;
    }

    public void OnPointerEvent(RawPointerEvent e)
    {
        switch (e.Phase)
        {
            case GesturePhase.Began:
                _pressStart     = e.Position;
                _pressStartTime = e.Timestamp;
                _pressMoved     = false;
                break;

            case GesturePhase.Updated:
                if (Vector2.Distance(e.Position, _pressStart) >= InputConstants.DragMinPixels)
                    _pressMoved = true;
                break;

            case GesturePhase.Ended:
            {
                if (_pressMoved || Vector2.Distance(e.Position, _pressStart) >= InputConstants.DragMinPixels)
                    return; // drags are cuts/overhands, never riffles

                bool longPress = e.Timestamp - _pressStartTime >= InputConstants.LongPressSeconds;
                if (longPress)
                    EmitStart(Mathf.Clamp01(e.Position.x / Screen.width), e.Source, e.Timestamp);
                else if (_riffleActive)
                    EmitDrop(e.Source, e.Timestamp);
                break;
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
        else if (e.ActionName == "Cancel")
            EndRiffle();
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
