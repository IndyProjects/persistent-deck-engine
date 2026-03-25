using System;
using UnityEngine;

/// Recognizes DealStart from keyboard two-digit prompt or gamepad axis events.
public class DealRecognizer : IGestureRecognizer
{
    public GestureType Handles => GestureType.DealStart;
    public event Action<GestureEvent> GestureRecognized;

    private int _pendingPlayers = -1;

    public void OnPointerEvent(RawPointerEvent e) { }

    public void OnAxisEvent(RawAxisEvent e)
    {
        double t = e.Timestamp;
        switch (e.ActionName)
        {
            case "DealPlayers":
                _pendingPlayers = Mathf.RoundToInt(e.Value);
                break;

            case "DealCardsEach":
                if (_pendingPlayers > 0)
                {
                    int cardsEach = Mathf.RoundToInt(e.Value);
                    if (cardsEach >= 1)
                        EmitDealStart(_pendingPlayers, cardsEach, e.Source, t);
                    _pendingPlayers = -1;
                }
                break;
        }
    }

    public void OnButtonEvent(RawButtonEvent e) { }

    private void EmitDealStart(int players, int cardsEach, InputSource src, double t)
    {
        GestureRecognized?.Invoke(new GestureEvent
        {
            Type      = GestureType.DealStart,
            Source    = src,
            Phase     = GesturePhase.Ended,
            Timestamp = t,
            Payload   = new DealStartPayload { PlayerCount = players, CardsEachCount = cardsEach }
        });
    }
}
