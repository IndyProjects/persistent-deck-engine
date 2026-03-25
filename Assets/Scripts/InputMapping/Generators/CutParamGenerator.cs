using System.Collections.Generic;
using UnityEngine;

public class CutParamGenerator : IParameterGenerator<CutParams>
{
    public ParameterResult<CutParams> Generate(List<GestureEvent> events)
    {
        if (events == null || events.Count == 0)
            return ParameterResult<CutParams>.Rejected("No events in Cut session.");

        var e = events[0];
        if (e.Payload is not CutPayload payload)
            return ParameterResult<CutParams>.Rejected("Cut event missing CutPayload.");

        if (float.IsNaN(payload.NormalizedPosition) || float.IsInfinity(payload.NormalizedPosition))
            return ParameterResult<CutParams>.Rejected("CutPayload.NormalizedPosition is NaN or Infinity.");

        int raw      = Mathf.RoundToInt(payload.NormalizedPosition * 51f);
        int clamped  = Mathf.Clamp(raw, 1, 51);
        var p        = new CutParams { Position = clamped };

        return (raw == clamped)
            ? ParameterResult<CutParams>.Valid(p)
            : ParameterResult<CutParams>.Clamped(p, $"position {raw} clamped to {clamped}");
    }
}
