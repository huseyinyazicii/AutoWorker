using System.Collections.Concurrent;

namespace AutoWorker;

public class AutoWorker<TInput, TOutput>(Func<TInput, Task<TOutput>> processItemAsync)
{
    private readonly ConcurrentQueue<(TInput Item, TaskCompletionSource<WorkerResult<TOutput>>? Tcs)> _queue = [];
    private readonly Func<TInput, Task<TOutput>> _processItemAsync = processItemAsync ?? throw new ArgumentNullException(nameof(processItemAsync));
    private long _isProcessing = 0;

    public void Enqueue(TInput item, Action? checkExpiredKeys = null)
    {
        _queue.Enqueue((item, null));
        TryStartProcessing(checkExpiredKeys);
    }

    public Task<WorkerResult<TOutput>> EnqueueAsync(TInput item, Action? checkExpiredKeys = null)
    {
        var tcs = new TaskCompletionSource<WorkerResult<TOutput>>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Enqueue((item, tcs));
        TryStartProcessing(checkExpiredKeys);
        return tcs.Task;
    }

    private async Task ProcessQueueAsync(Action? checkExpiredKeys)
    {
        while (_queue.TryDequeue(out var entry))
        {
            try
            {
                var result = await _processItemAsync(entry.Item);
                entry.Tcs?.TrySetResult(WorkerResult<TOutput>.Success(result));
            }
            catch (Exception ex)
            {
                entry.Tcs?.TrySetResult(WorkerResult<TOutput>.Error(ex));
            }
        }

        Interlocked.Exchange(ref _isProcessing, 0);

        if (!_queue.IsEmpty)
            TryStartProcessing(checkExpiredKeys);
        else if (checkExpiredKeys is not null)
            checkExpiredKeys();
    }

    private void TryStartProcessing(Action? checkExpiredKeys)
    {
        if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) == 0)
            _ = Task.Run(() => ProcessQueueAsync(checkExpiredKeys));
    }

    internal bool HasNoWork() => _queue.IsEmpty && Interlocked.Read(ref _isProcessing) == 0;
}
