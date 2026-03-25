using System;
using UnityEngine;

/// Recognizes CollectPile gestures sequential pile taps/key presses.
public class CollectRecognizer : IGestureRecognizer
{
    public GestureType Handles => GestureType.CollectPile;
    public event Action<GestureEvent> GestureRecognized;

    private int _nextGamepadPile = 0;

    public void OnPointerEvent(RawPointerEvent e)
    {
        // Short tap = collect next pile.
        if (e.Phase == GesturePhase.Ended)
        {
            EmitCollect(0, e.Source, e.Timestamp);
        }
    }

    public void OnAxisEvent(RawAxisEvent e)
    {
        if (e.ActionName == "CollectPile")
        {
            int idx = Mathf.RoundToInt(e.Value);
            EmitCollect(idx, e.Source, e.Timestamp);
        }
    }

    public void OnButtonEvent(RawButtonEvent e)
    {
        switch (e.ActionName)
        {
            case "CollectNextPile":
                EmitCollect(_nextGamepadPile++, e.Source, e.Timestamp);
                break;
            case "CollectStart":
                _nextGamepadPile = 0;
                break;
        }
    }

    private void EmitCollect(int pileIndex, InputSource src, double t)
    {
        GestureRecognized?.Invoke(new GestureEvent
        {
            Type      = GestureType.CollectPile,
            Source    = src,
            Phase     = GesturePhase.Ended,
            Timestamp = t,
            Payload   = new CollectPilePayload { PileIndex = pileIndex }
        });
    }
}
