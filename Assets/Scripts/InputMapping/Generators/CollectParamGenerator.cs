using System.Collections.Generic;

public class CollectParamGenerator : IParameterGenerator<CollectParams>
{
    public ParameterResult<CollectParams> Generate(List<GestureEvent> events)
        => ParameterResult<CollectParams>.Rejected("Use GenerateFromSession for collect.");

    public ParameterResult<CollectParams> GenerateFromSession(CollectSession session, int[][] piles)
    {
        if (session == null)
            return ParameterResult<CollectParams>.Rejected("Null CollectSession.");
        if (session.IsCancelled)
            return ParameterResult<CollectParams>.Rejected("CollectSession was cancelled.");
        if (piles == null)
            return ParameterResult<CollectParams>.Rejected("Piles not provided.");

        var order = session.PileOrder;
        if (order.Count != piles.Length)
            return ParameterResult<CollectParams>.Rejected(
                $"Order length {order.Count} != pile count {piles.Length}.");

        return ParameterResult<CollectParams>.Valid(new CollectParams
        {
            Piles = piles,
            Order = order.ToArray()
        });
    }
}
