namespace AutoWorker;

public struct WorkerResult<T>
{
    public T? Result { get; private set; }
    public Exception? Exception { get; private set; }

    public readonly bool IsSuccess => Exception == null;

    internal static WorkerResult<T> Error(Exception ex) => new() { Exception = ex };
    internal static WorkerResult<T> Success(T result) => new() { Result = result };
}