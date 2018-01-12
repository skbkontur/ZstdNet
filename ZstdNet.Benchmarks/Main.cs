using BenchmarkDotNet.Running;

namespace ZstdNet.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<CompressionBenchmarks>();
        }
    }
}
