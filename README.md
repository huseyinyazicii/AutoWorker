# AutoWorker & PartitionedAutoWorker  

This library provides two utility classes, `AutoWorker<TInput, TOutput>` and `PartitionedAutoWorker<TInput, TOutput>`, designed to make **queue-based background processing** easier.  

The main goal:  
- **AutoWorker** → Process items in a queue sequentially on a single thread.  
- **PartitionedAutoWorker** → Process items sequentially **per partition key**, while allowing different keys to be processed **in parallel**.  

---

## Features 🚀  

- **AutoWorker**
  - Processes items **on a single thread** in FIFO (First-In First-Out) order.
  - Automatically starts when items are enqueued (no manual trigger needed).
  - Supports both `Enqueue` (fire-and-forget) and `EnqueueAsync` (awaitable).
  - Stops automatically when the queue is empty.

- **PartitionedAutoWorker**
  - Distributes work into partitions based on a **partition key**.
  - Each partition has its own `AutoWorker`, ensuring sequential processing within the same partition.
  - Different partitions are processed **in parallel**.
  - Expired/unused partitions are automatically removed.

---

## Usage  

### AutoWorker  

```csharp
// 1. Create a worker
var worker = new AutoWorker<string, int>(async item =>
{
    await Task.Delay(100); // simulate some work
    return item.Length;    // return string length
});

// 2. Fire-and-forget usage
worker.Enqueue("hello");

// 3. Awaitable usage
var result = await worker.EnqueueAsync("world");
Console.WriteLine($"Result: {result.Result}, Success: {result.IsSuccess}");
````

### PartitionedAutoWorker  

```csharp
// 1. Create a partitioned worker
var partitionedWorker = new PartitionedAutoWorker<string, int>(
    async (partitionKey, item) =>
    {
        await Task.Delay(100);
        Console.WriteLine($"Partition: {partitionKey}, Item: {item}");
        return item.Length;
    });

// 2. Same partition key → processed sequentially
partitionedWorker.Enqueue("user:1", "hello");
partitionedWorker.Enqueue("user:1", "world");

// 3. Different partition keys → processed in parallel
partitionedWorker.Enqueue("user:2", "parallel");

// 4. Awaitable usage
var result = await partitionedWorker.EnqueueAsync("user:3", "async test");
Console.WriteLine($"Partition: user:3, Result: {result.Result}");
````

---

### WorkerResult  

Each processed item returns a WorkerResult<T>:

```csharp
public struct WorkerResult<T>
{
    public T? Result { get; }
    public Exception? Exception { get; }
    public bool IsSuccess => Exception == null;
}
````

✅ Success(result) → successful operation

❌ Error(exception) → failed operation

Example:

```csharp
var result = await worker.EnqueueAsync("test");
if (result.IsSuccess)
    Console.WriteLine($"Result: {result.Result}");
else
    Console.WriteLine($"Error: {result.Exception}");
````

---

### Scenarios

- **User-based job queue** → All jobs for the same user are processed sequentially, different users run in parallel.

- **File processing** → Ensure sequential operations per file, while multiple files are handled concurrently.

- **Event processing** → Partition events by key to ensure order, but scale across partitions.


### Notes 📝

- `AutoWorker` always guarantees single-threaded sequential processing.

- `PartitionedAutoWorker` spins up one `AutoWorker` per partition key, giving per-partition sequential + cross-partition parallelism.

- `PartitionItem` is implemented as a `struct` for value-based comparison in the dictionary.

- Expired partitions are cleaned up automatically via `CheckExpiredKeys`.
