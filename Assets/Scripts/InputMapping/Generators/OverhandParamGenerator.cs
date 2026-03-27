using System.Collections.Generic;

public class OverhandParamGenerator
{
    public ParameterResult<OverhandParams> Generate(List<GestureEvent> events)
    {

        return ParameterResult<OverhandParams>.Rejected("Use GenerateFromSession for overhand.");
    }

    public ParameterResult<OverhandParams> GenerateFromSession(OverhandSession session)
    {
        if (session == null)
            return ParameterResult<OverhandParams>.Rejected("Null OverhandSession.");

        if (session.IsCancelled)
            return ParameterResult<OverhandParams>.Rejected("OverhandSession was cancelled.");

        var chunks = session.ChunkSizes;
        if (chunks.Count == 0)
            return ParameterResult<OverhandParams>.Rejected("No chunks in OverhandSession.");

        int sum = 0;
        foreach (int c in chunks)
        {
            if (c <= 0)
                return ParameterResult<OverhandParams>.Rejected($"Chunk size {c} <= 0.");
            sum += c;
        }

        if (sum != DeckEngine.DeckSize)
            return ParameterResult<OverhandParams>.Rejected($"Chunk sum {sum} != {DeckEngine.DeckSize}.");

        return ParameterResult<OverhandParams>.Valid(new OverhandParams { ChunkSizes = chunks.ToArray() });
    }
}
