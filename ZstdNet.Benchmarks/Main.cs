using BenchmarkDotNet.Running;

namespace ZstdNet.Benchmarks
{
	class Program
	{
		static void Main()
		{
			BenchmarkRunner.Run<CompressionOverheadBenchmarks>();
		}
	}
}
