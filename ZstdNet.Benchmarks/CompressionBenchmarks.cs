using System;
using BenchmarkDotNet.Attributes;

namespace ZstdNet.Benchmarks
{
	[MemoryDiagnoser]
	public class CompressionOverheadBenchmarks
	{
		private const int TestSize = 1024;

		private readonly byte[] UncompressedData = new byte[TestSize];
		private readonly byte[] CompressedData;

		private readonly byte[] Buffer = new byte[Compressor.GetCompressBound(TestSize)];

		private readonly Compressor Compressor = new Compressor(new CompressionOptions(1));
		private readonly Decompressor Decompressor = new Decompressor();

		public CompressionOverheadBenchmarks()
		{
			var r = new Random(0);
			r.NextBytes(UncompressedData);

			CompressedData = Compressor.Wrap(UncompressedData);
		}

		[Benchmark] public void Compress1KBRandom() => Compressor.Wrap(UncompressedData, Buffer, 0);
		[Benchmark] public void Decompress1KBRandom() => Decompressor.Unwrap(CompressedData, Buffer, 0);
	}
}
