using System;
using System.IO;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace ZstdNet.Benchmarks
{
	[MemoryDiagnoser]
	public class CompressionOverheadBenchmarks
	{
		[Params(1, 1024, 128 * 1024 * 1024)]
		public int DataSize { get; set; }

		private byte[] Data;
		private byte[] CompressedData;
		private byte[] CompressedStreamData;

		private byte[] Buffer;

		private readonly Compressor Compressor = new Compressor(CompressionOptions.Default);
		private readonly Decompressor Decompressor = new Decompressor();

		[GlobalSetup]
		public void GlobalSetup()
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
		public async Task CompressStreamAsync(int zstdBufferSize, int copyBufferSize)
		{
#if !NET48
			await
#endif
			using var compressionStream = new CompressionStream(Stream.Null, CompressionOptions.Default, zstdBufferSize);
			await new MemoryStream(Data).CopyToAsync(compressionStream, copyBufferSize);
		}

		[Benchmark]
		[Arguments(7, 13)]
		public void DecompressStream(int zstdBufferSize, int copyBufferSize)
		{
			using var decompressionStream = new DecompressionStream(new MemoryStream(CompressedStreamData), zstdBufferSize);
			decompressionStream.CopyTo(Stream.Null, copyBufferSize);
		}

		[Benchmark]
		[Arguments(7, 13)]
		public async Task DecompressStreamAsync(int zstdBufferSize, int copyBufferSize)
		{
#if !NET48
			await
#endif
			using var decompressionStream = new DecompressionStream(new MemoryStream(CompressedStreamData), zstdBufferSize);
			await decompressionStream.CopyToAsync(Stream.Null, copyBufferSize);
		}
	}
}
