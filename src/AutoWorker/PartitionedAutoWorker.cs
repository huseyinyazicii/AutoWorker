using System.Collections.Concurrent;

namespace AutoWorker;

public class PartitionedAutoWorker<TInput, TOutput>(Func<string, TInput, Task<TOutput>> processItemByPartitionAsync)
{
    private readonly ConcurrentDictionary<string, PartitionItem> _partitionKeys = [];
    private readonly Func<string, TInput, Task<TOutput>> _processItemByPartitionAsync = processItemByPartitionAsync ?? throw new ArgumentNullException(nameof(processItemByPartitionAsync));

    public void Enqueue(string partitionKey, TInput item)
    {
        var partitionItem = GetOrAddPartitionItem(partitionKey);
        partitionItem.Worker.Enqueue(item, CheckExpiredKeys);
    }

    public Task<WorkerResult<TOutput>> EnqueueAsync(string partitionKey, TInput item)
    {
        var partitionItem = GetOrAddPartitionItem(partitionKey);
        return partitionItem.Worker.EnqueueAsync(item, CheckExpiredKeys);
    }

    private void CheckExpiredKeys()
    {
        foreach (var toBeRemoveItem in _partitionKeys.Where(p => p.Value.UpdateDate < DateTime.UtcNow && p.Value.Worker.HasNoWork()))
        {
            var isRemoved = _partitionKeys.TryRemove(toBeRemoveItem);
        }
    }

    private PartitionItem GetOrAddPartitionItem(string partitionKey) => _partitionKeys
        .AddOrUpdate(
            partitionKey,
            (pKey) => new(new((itemsToProcess) => _processItemByPartitionAsync(pKey, itemsToProcess))),
            (key, old) => new(old.Worker)
        );

    private readonly struct PartitionItem(AutoWorker<TInput, TOutput> worker)
    {
        public readonly DateTime UpdateDate { get; init; } = DateTime.UtcNow;
        public readonly AutoWorker<TInput, TOutput> Worker { get; init; } = worker;
    }
}
