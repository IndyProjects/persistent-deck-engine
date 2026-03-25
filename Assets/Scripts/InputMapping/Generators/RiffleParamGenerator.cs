using System.Collections.Generic;
using UnityEngine;

public class RiffleParamGenerator : IParameterGenerator<RiffleParams>
{
    public ParameterResult<RiffleParams> Generate(List<GestureEvent> events)
    {
        if (events == null || events.Count == 0)
            return ParameterResult<RiffleParams>.Rejected("No events in Riffle session.");

        // First event must be RiffleStart
        if (events[0].Payload is not RiffleStartPayload startPayload)
            return ParameterResult<RiffleParams>.Rejected("First event is not RiffleStart.");

        float norm     = startPayload.NormalizedSplitPosition;
        if (float.IsNaN(norm) || float.IsInfinity(norm))
            return ParameterResult<RiffleParams>.Rejected("RiffleStartPayload.NormalizedSplitPosition is invalid.");

        int raw      = Mathf.RoundToInt(norm * 51f);
        int split    = Mathf.Clamp(raw, 1, 51);
        bool clamped = (raw != split);

        // Collect drop counts
        var pattern = new List<int>();
        for (int i = 1; i < events.Count; i++)
        {
            if (events[i].Payload is not RiffleDropPayload drop)
                continue;
            if (drop.CardCount < 1)
                return ParameterResult<RiffleParams>.Rejected($"Drop at index {i} has CardCount < 1.");
            pattern.Add(drop.CardCount);
        }

        if (pattern.Count == 0)
            return ParameterResult<RiffleParams>.Rejected("No RiffleDrop events in session.");

        var p = new RiffleParams { SplitIndex = split, DropPattern = pattern.ToArray() };

        return clamped
            ? ParameterResult<RiffleParams>.Clamped(p, $"splitIndex {raw} clamped to {split}")
            : ParameterResult<RiffleParams>.Valid(p);
    }
}
