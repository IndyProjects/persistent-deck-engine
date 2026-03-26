/// Returned by GameStateManager.Apply and GameStateManager.CanApply.
public struct OperationResult
{
    public bool   Success { get; private set; }
    public string Reason  { get; private set; }

    public static OperationResult Ok()                   => new OperationResult { Success = true };
    public static OperationResult Reject(string reason)  => new OperationResult { Success = false, Reason = reason };
}
