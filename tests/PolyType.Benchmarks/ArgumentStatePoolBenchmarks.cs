using BenchmarkDotNet.Attributes;

namespace PolyType.Benchmarks;

[MemoryDiagnoser]
public class ArgumentStatePoolBenchmarks
{
    private const int OperationsPerThread = 10_000;
    [Params(2, 8)]
    public int ThreadCount { get; set; }

    [Benchmark(Baseline = true, Description = "Single-threaded: global atomic slot")]
    public int GlobalSlotSingleThreaded() => RunSingleThreaded<GlobalSlotPool>();

    [Benchmark(Description = "Single-threaded: thread-static slot")]
    public int ThreadStaticSlotSingleThreaded() => RunSingleThreaded<ThreadStaticSlotPool>();

    [Benchmark(Description = "Parallel: global atomic slot")]
    public int GlobalSlotParallel() => RunParallel<GlobalSlotPool>();

    [Benchmark(Description = "Parallel: thread-static slot")]
    public int ThreadStaticSlotParallel() => RunParallel<ThreadStaticSlotPool>();

    private static int RunSingleThreaded<TPool>()
        where TPool : IStatePool<TPool>
    {
        int sum = 0;
        for (int i = 0; i < OperationsPerThread; i++)
        {
            PooledItem item = TPool.Rent();
            item.Value = i;
            sum += item.Value;
            TPool.Return(item);
        }

        return sum;
    }

    private int RunParallel<TPool>()
        where TPool : IStatePool<TPool>
    {
        int sum = 0;
        Parallel.For(0, ThreadCount, _ =>
        {
            int localSum = 0;
            for (int i = 0; i < OperationsPerThread; i++)
            {
                PooledItem item = TPool.Rent();
                item.Value = i;
                localSum += item.Value;
                TPool.Return(item);
            }

            Interlocked.Add(ref sum, localSum);
        });

        return sum;
    }

    private sealed class PooledItem
    {
        public int Value;
    }

    private interface IStatePool<TSelf>
        where TSelf : IStatePool<TSelf>
    {
        static abstract PooledItem Rent();
        static abstract void Return(PooledItem item);
    }

    private sealed class GlobalSlotPool : IStatePool<GlobalSlotPool>
    {
        private static PooledItem? s_cached;

        public static PooledItem Rent()
        {
            PooledItem item = Interlocked.Exchange(ref s_cached, null) ?? new();
            item.Value = 0;
            return item;
        }

        public static void Return(PooledItem item)
        {
            item.Value = 0;
            Interlocked.CompareExchange(ref s_cached, item, null);
        }
    }

    private sealed class ThreadStaticSlotPool : IStatePool<ThreadStaticSlotPool>
    {
        [ThreadStatic]
        private static PooledItem? s_cached;

        public static PooledItem Rent()
        {
            PooledItem? item = s_cached;
            s_cached = null;
            item ??= new();
            item.Value = 0;
            return item;
        }

        public static void Return(PooledItem item)
        {
            item.Value = 0;
            s_cached = item;
        }
    }

}
