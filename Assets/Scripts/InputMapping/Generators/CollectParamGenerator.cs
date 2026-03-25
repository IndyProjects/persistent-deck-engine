using System.Collections.Generic;

public class CollectParamGenerator : IParameterGenerator<CollectParams>
{
    // Piles are injected by the coordinator before Generate is called.
    public int[][] CurrentPiles { get; set; }

    public ParameterResult<CollectParams> Generate(List<GestureEvent> events)
    {
        return ParameterResult<CollectParams>.Rejected("Use GenerateFromSession for collect.");
    }

    public ParameterResult<CollectParams> GenerateFromSession(CollectSession session)
    {
        if (session == null)
            return ParameterResult<CollectParams>.Rejected("Null CollectSession.");
        if (session.IsCancelled)
            return ParameterResult<CollectParams>.Rejected("CollectSession was cancelled.");
        if (CurrentPiles == null)
            return ParameterResult<CollectParams>.Rejected("CurrentPiles not set on CollectParamGenerator.");

        var order = session.PileOrder;
        if (order.Count != CurrentPiles.Length)
            return ParameterResult<CollectParams>.Rejected(
                $"Order length {order.Count} != pile count {CurrentPiles.Length}.");

        return ParameterResult<CollectParams>.Valid(new CollectParams
        {
            Piles = CurrentPiles,
            Order = order.ToArray()
        });
    }
}
