public struct ParameterResult<T>
{
    public T               Params;
    public ParameterStatus Status;
    public string          Reason;

    public static ParameterResult<T> Valid(T p)
        => new ParameterResult<T> { Params = p, Status = ParameterStatus.Valid };

    public static ParameterResult<T> Clamped(T p, string reason)
        => new ParameterResult<T> { Params = p, Status = ParameterStatus.Clamped, Reason = reason };

    public static ParameterResult<T> Rejected(string reason)
        => new ParameterResult<T> { Status = ParameterStatus.Rejected, Reason = reason };
}
