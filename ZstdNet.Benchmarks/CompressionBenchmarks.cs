using System;
using System.IO;
using BenchmarkDotNet.Attributes;

namespace ZstdNet.Benchmarks
{
	[MemoryDiagnoser]
	public class CompressionOverheadBenchmarks
	{
		[Params(1024)] public static int DataSize;

		private readonly byte[] Data;
		private readonly byte[] CompressedData;
		private readonly byte[] CompressedStreamData;

		private readonly byte[] Buffer;

		private readonly Compressor Compressor = new Compressor(CompressionOptions.Default);
		private readonly Decompressor Decompressor = new Decompressor();

		public CompressionOverheadBenchmarks()
		{
			var r = new Random(0);

			Buffer = new byte[Math.Max(DataSize, Compressor.GetCompressBound(DataSize))];
			Data = new byte[DataSize];
			r.NextBytes(Data);

			CompressedData = Compressor.Wrap(Data);

			using var tempStream = new MemoryStream();
			using var compressionStream = new CompressionStream(tempStream);
			new MemoryStream(Data).CopyTo(compressionStream);
			CompressedStreamData = tempStream.ToArray();
		}

		[Benchmark] public void Compress() => Compressor.Wrap(Data, Buffer, 0);
		[Benchmark] public void Decompress() => Decompressor.Unwrap(CompressedData, Buffer, 0);

		[Benchmark]
		[Arguments(7, 13)]
		public void CompressStream(int zstdBufferSize, int copyBufferSize)
		{
			using var compressionStream = new CompressionStream(Stream.Null, CompressionOptions.Default, zstdBufferSize);
			new MemoryStream(Data).CopyTo(compressionStream, copyBufferSize);
		}

		[Benchmark]
		[Arguments(7, 13)]
		public void DecompressStream(int zstdBufferSize, int copyBufferSize)
		{
			using var decompressionStream = new DecompressionStream(new MemoryStream(CompressedStreamData), zstdBufferSize);
			decompressionStream.CopyTo(Stream.Null, copyBufferSize);
		}
	}
}
