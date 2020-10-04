using BenchmarkDotNet.Running;

namespace ZstdNet.Benchmarks
{
	class Program
	{
		static void Main(string[] args)
		{
			BenchmarkSwitcher
				.FromTypes(new[] {typeof(CompressionOverheadBenchmarks)})
				.Run(args);
		}
	}
}
