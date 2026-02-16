using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
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
		private readonly Compressor CompressorAdvanced = new Compressor(new CompressionOptions(null, new Dictionary<ZSTD_cParameter, int>()));
		private readonly Decompressor Decompressor = new Decompressor();

		// Native dependencies are not added to the deps.json file via ProjectReference
		// https://github.com/dotnet/sdk/issues/10575
		static CompressionOverheadBenchmarks()
		{
			var ext = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "dll" : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "dylib" : "so";
			NativeLibrary.SetDllImportResolver(typeof(DictBuilder).Assembly, (name, a, b) => name == "libzstd"
				? NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, "runtimes", RuntimeInformation.RuntimeIdentifier, "native", $"libzstd.{ext}"))
				: IntPtr.Zero);
		}

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
		[Benchmark] public void CompressAdvanced() => CompressorAdvanced.Wrap(Data, Buffer, 0);
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
			await using var compressionStream = new CompressionStream(Stream.Null, CompressionOptions.Default, zstdBufferSize);
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
			await using var decompressionStream = new DecompressionStream(new MemoryStream(CompressedStreamData), zstdBufferSize);
			await decompressionStream.CopyToAsync(Stream.Null, copyBufferSize);
		}
	}
}
