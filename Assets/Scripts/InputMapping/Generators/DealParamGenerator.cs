using System.Collections.Generic;
using UnityEngine;

public class DealParamGenerator : IParameterGenerator<DealParams>
{
    public ParameterResult<DealParams> Generate(List<GestureEvent> events)
    {
        if (events == null || events.Count == 0)
            return ParameterResult<DealParams>.Rejected("No events in Deal session.");

        if (events[0].Payload is not DealStartPayload payload)
            return ParameterResult<DealParams>.Rejected("First event is not DealStart.");

        int players   = payload.PlayerCount;
        int cardsEach = payload.CardsEachCount;

        if (players < 1)
            return ParameterResult<DealParams>.Rejected($"players {players} < 1.");
        if (cardsEach < 1)
            return ParameterResult<DealParams>.Rejected($"cardsEach {cardsEach} < 1.");

        bool clamped = false;
        if (players * cardsEach > DeckEngine.DeckSize)
        {
            cardsEach = DeckEngine.DeckSize / players;
            clamped   = true;
        }

        var p = new DealParams { Players = players, CardsEach = cardsEach };
        return clamped
            ? ParameterResult<DealParams>.Clamped(p, $"cardsEach reduced to {cardsEach} (product exceeded {DeckEngine.DeckSize})")
            : ParameterResult<DealParams>.Valid(p);
    }
}
