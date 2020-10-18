using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using NUnit.Framework;

namespace ZstdNet.Tests
{
	[TestFixture]
	public class Binding_Tests
	{
		public enum CompressionLevel
		{
			Default = 0,
			Min,
			Max
		}

		[Test]
		public void CompressAndDecompress_workCorrectly([Values(false, true)] bool useDictionary, [Values(CompressionLevel.Min, CompressionLevel.Default, CompressionLevel.Max)] CompressionLevel level)
		{
			var data = GenerateSample();

			var dict = useDictionary ? BuildDictionary() : null;
			var compressionLevel = level switch
			{
				CompressionLevel.Min => CompressionOptions.MinCompressionLevel,
				CompressionLevel.Max => CompressionOptions.MaxCompressionLevel,
				_ => CompressionOptions.DefaultCompressionLevel
			};

			byte[] compressed;
			using(var options = new CompressionOptions(dict, compressionLevel))
			using(var compressor = new Compressor(options))
				compressed = compressor.Wrap(data);

			byte[] decompressed;
			using(var options = new DecompressionOptions(dict))
			using(var decompressor = new Decompressor(options))
				decompressed = decompressor.Unwrap(compressed);

			CollectionAssert.AreEqual(data, decompressed);
		}

		[Test]
		public void CompressAndDecompress_worksCorrectly_advanced([Values(false, true)] bool useDictionary)
		{
			var data = GenerateSample();
			var dict = useDictionary ? BuildDictionary() : null;

			byte[] compressed1, compressed2;

			using(var options = new CompressionOptions(dict, new Dictionary<ZSTD_cParameter, int> {{ZSTD_cParameter.ZSTD_c_checksumFlag, 0}}))
			using(var compressor = new Compressor(options))
				compressed1 = compressor.Wrap(data);

			using(var options = new CompressionOptions(dict, new Dictionary<ZSTD_cParameter, int> {{ZSTD_cParameter.ZSTD_c_checksumFlag, 1}}))
			using(var compressor = new Compressor(options))
				compressed2 = compressor.Wrap(data);

			Assert.AreEqual(compressed1.Length + 4, compressed2.Length);

			using(var options = new DecompressionOptions(dict, new Dictionary<ZSTD_dParameter, int>()))
			using(var decompressor = new Decompressor(options))
			{
				CollectionAssert.AreEqual(data, decompressor.Unwrap(compressed1));
				CollectionAssert.AreEqual(data, decompressor.Unwrap(compressed2));
			}
		}

		[Test]
		public void DecompressWithDictionary_worksCorrectly_onDataCompressedWithoutIt()
		{
			var data = GenerateSample();
			byte[] compressed;
			using(var compressor = new Compressor())
				compressed = compressor.Wrap(data);

			var dict = BuildDictionary();

			byte[] decompressed;
			using(var options = new DecompressionOptions(dict))
			using(var decompressor = new Decompressor(options))
				decompressed = decompressor.Unwrap(compressed);

			CollectionAssert.AreEqual(data, decompressed);
		}

		[Test]
		public void DecompressWithoutDictionary_throwsZstdException_onDataCompressedWithIt()
		{
			var data = GenerateSample();
			var dict = BuildDictionary();

			byte[] compressed;
			using(var options = new CompressionOptions(dict))
			using(var compressor = new Compressor(options))
				compressed = compressor.Wrap(data);

			using(var decompressor = new Decompressor())
				Assert.Throws<ZstdException>(() => decompressor.Unwrap(compressed));
		}

		[Test]
		public void DecompressWithAnotherDictionary_throwsZstdException()
		{
			var data = GenerateSample();
			var oldDict = BuildDictionary();

			byte[] compressed;
			using(var options = new CompressionOptions(oldDict))
			using(var compressor = new Compressor(options))
				compressed = compressor.Wrap(data);

			var newDict = Encoding.ASCII.GetBytes("zstd supports raw-content dictionaries");

			using(var options = new DecompressionOptions(newDict))
			using(var decompressor = new Decompressor(options))
				Assert.Throws<ZstdException>(() => decompressor.Unwrap(compressed));
		}

		[Test]
		public void Compress_reducesDataSize()
		{
			var data = GenerateSample();

			byte[] compressed;
			using(var compressor = new Compressor())
				compressed = compressor.Wrap(data);

			Assert.Greater(data.Length, compressed.Length);
		}

		[Test]
		public void Compress_worksBetter_withDictionary()
		{
			var data = GenerateSample();

			byte[] compressedWithoutDict, compressedWithDict;
			using (var compressor = new Compressor())
				compressedWithoutDict = compressor.Wrap(data);

			using(var options = new CompressionOptions(BuildDictionary()))
			using(var compressor = new Compressor(options))
				compressedWithDict = compressor.Wrap(data);

			Assert.Greater(compressedWithoutDict.Length, compressedWithDict.Length);
		}

		[Test]
		public void Decompress_throwsZstdException_onInvalidData([Values(false, true)] bool useDictionary)
		{
			var data = GenerateSample(); // This isn't data in compressed format
			var dict = useDictionary ? BuildDictionary() : null;

			using(var options = new DecompressionOptions(dict))
			using(var decompressor = new Decompressor(options))
				Assert.Throws<ZstdException>(() => decompressor.Unwrap(data));
		}

		[Test]
		public void Decompress_throwsZstdException_onMalformedDecompressedSize([Values(false, true)] bool useDictionary)
		{
			var data = GenerateSample();
			var dict = useDictionary ? BuildDictionary() : null;

			byte[] compressed;
			using(var options = new CompressionOptions(dict))
			using(var compressor = new Compressor(options))
				compressed = compressor.Wrap(data);

			var frameHeader = compressed[4]; // Ensure that we malform decompressed size in the right place
			if(useDictionary)
			{
				Assert.AreEqual(frameHeader, 0x63);
				compressed[9]--;
			}
			else
			{
				Assert.AreEqual(frameHeader, 0x60);
				compressed[5]--;
			}

			// Thus, ZSTD_getDecompressedSize will return size that is one byte lesser than actual
			using(var options = new DecompressionOptions(dict))
			using(var decompressor = new Decompressor(options))
				Assert.Throws<ZstdException>(() => decompressor.Unwrap(compressed));
		}

		[Test]
		public void Decompress_throwsArgumentOutOfRangeException_onTooBigData([Values(false, true)] bool useDictionary)
		{
			var data = GenerateSample();
			var dict = useDictionary ? BuildDictionary() : null;

			byte[] compressed;
			using(var options = new CompressionOptions(dict))
			using(var compressor = new Compressor(options))
				compressed = compressor.Wrap(data);

			using(var options = new DecompressionOptions(dict))
			using(var decompressor = new Decompressor(options))
				Assert.Throws<ArgumentOutOfRangeException>(() => decompressor.Unwrap(compressed, 20));
		}

		[Test]
		public void Compress_canRead_fromArraySegment([Values(false, true)] bool useDictionary)
		{
			var data = GenerateSample();
			var segment = new ArraySegment<byte>(data, 2, data.Length - 5);
			var dict = useDictionary ? BuildDictionary() : null;

			byte[] compressed;
			using(var options = new CompressionOptions(dict))
			using(var compressor = new Compressor(options))
				compressed = compressor.Wrap(segment);

			byte[] decompressed;
			using(var options = new DecompressionOptions(dict))
			using(var decompressor = new Decompressor(options))
				decompressed = decompressor.Unwrap(compressed);

			CollectionAssert.AreEqual(segment, decompressed);
		}

		[Test]
		public void CompressAndDecompress_workCorrectly_spans([Values(false, true)] bool useDictionary)
		{
			var buffer = GenerateSample();

			var data = new ReadOnlySpan<byte>(buffer, 1, buffer.Length - 1);
			var dict = useDictionary ? BuildDictionary() : null;

			Span<byte> compressed = stackalloc byte[Compressor.GetCompressBound(data.Length)];
			using(var options = new CompressionOptions(dict))
			using(var compressor = new Compressor(options))
			{
				var size = compressor.Wrap(data, compressed);
				compressed = compressed.Slice(0, size);
			}

			Span<byte> decompressed = stackalloc byte[data.Length + 1];
			using(var options = new DecompressionOptions(dict))
			using(var decompressor = new Decompressor(options))
			{
				var size = decompressor.Unwrap(compressed, decompressed);
				Assert.AreEqual(data.Length, size);
				decompressed = decompressed.Slice(0, size);
			}

			CollectionAssert.AreEqual(data.ToArray(), decompressed.ToArray());
		}

		[Test]
		public void Decompress_canRead_fromArraySegment([Values(false, true)] bool useDictionary)
		{
			var data = GenerateSample();
			var dict = useDictionary ? BuildDictionary() : null;

			byte[] compressed;
			using(var options = new CompressionOptions(dict))
			using(var compressor = new Compressor(options))
				compressed = compressor.Wrap(data);

			compressed = new byte[] {1, 2}.Concat(compressed).Concat(new byte[] {4, 5, 6}).ToArray();
			var segment = new ArraySegment<byte>(compressed, 2, compressed.Length - 5);

			byte[] decompressed;
			using(var options = new DecompressionOptions(dict))
			using(var decompressor = new Decompressor(options))
				decompressed = decompressor.Unwrap(segment);

			CollectionAssert.AreEqual(data, decompressed);
		}

		[Test]
		public void Compress_canWrite_toGivenBuffer([Values(false, true)] bool useDictionary)
		{
			var data = GenerateSample();
			var dict = useDictionary ? BuildDictionary() : null;
			var compressed = new byte[1000];
			const int offset = 54;

			int compressedSize;
			using(var options = new CompressionOptions(dict))
			using(var compressor = new Compressor(options))
				compressedSize = compressor.Wrap(data, compressed, offset);

			byte[] decompressed;
			using(var options = new DecompressionOptions(dict))
			using(var decompressor = new Decompressor(options))
				decompressed = decompressor.Unwrap(compressed.Skip(offset).Take(compressedSize).ToArray());

			CollectionAssert.AreEqual(data, decompressed);
		}

		[Test]
		public void Decompress_canWrite_toGivenBuffer([Values(false, true)] bool useDictionary)
		{
			var data = GenerateSample();
			var dict = useDictionary ? BuildDictionary() : null;

			byte[] compressed;
			using(var options = new CompressionOptions(dict))
			using(var compressor = new Compressor(options))
				compressed = compressor.Wrap(data);

			var decompressed = new byte[1000];
			const int offset = 54;

			int decompressedSize;
			using(var options = new DecompressionOptions(dict))
			using(var decompressor = new Decompressor(options))
				decompressedSize = decompressor.Unwrap(compressed, decompressed, offset);

			CollectionAssert.AreEqual(data, decompressed.Skip(offset).Take(decompressedSize));
		}

		[Test]
		public void Compress_throwsInsufficientMemoryException_whenDestinationBufferIsTooSmall([Values(false, true)] bool useDictionary)
		{
			var data = GenerateSample();
			var dict = useDictionary ? BuildDictionary() : null;
			var compressed = new byte[20];
			const int offset = 4;

			using(var options = new CompressionOptions(dict))
			using(var compressor = new Compressor(options))
				Assert.Throws<InsufficientMemoryException>(() => compressor.Wrap(data, compressed, offset));
		}

		[Test]
		public void Decompress_throwsInsufficientMemoryException_whenDestinationBufferIsTooSmall([Values(false, true)] bool useDictionary)
		{
			var data = GenerateSample();
			var dict = useDictionary ? BuildDictionary() : null;

			byte[] compressed;
			using(var options = new CompressionOptions(dict))
			using(var compressor = new Compressor(options))
				compressed = compressor.Wrap(data);

			var decompressed = new byte[20];
			const int offset = 4;

			using(var options = new DecompressionOptions(dict))
			using(var decompressor = new Decompressor(options))
				Assert.Throws<InsufficientMemoryException>(() => decompressor.Unwrap(compressed, decompressed, offset));
		}

		[Test]
		public void CompressAndDecompress_workCorrectly_onEmptyBuffer([Values(false, true)] bool useDictionary)
		{
			var data = new byte[0];
			var dict = useDictionary ? BuildDictionary() : null;

			byte[] compressed;
			using(var options = new CompressionOptions(dict))
			using(var compressor = new Compressor(options))
				compressed = compressor.Wrap(data);

			byte[] decompressed;
			using(var options = new DecompressionOptions(dict))
			using(var decompressor = new Decompressor(options))
				decompressed = decompressor.Unwrap(compressed);

			CollectionAssert.AreEqual(data, decompressed);
		}

		[Test]
		public void CompressAndDecompress_workCorrectly_onOneByteBuffer([Values(false, true)] bool useDictionary)
		{
			var data = new byte[] { 42 };
			var dict = useDictionary ? BuildDictionary() : null;

			byte[] compressed;
			using(var options = new CompressionOptions(dict))
			using(var compressor = new Compressor(options))
				compressed = compressor.Wrap(data);

			byte[] decompressed;
			using(var options = new DecompressionOptions(dict))
			using(var decompressor = new Decompressor(options))
				decompressed = decompressor.Unwrap(compressed);

			CollectionAssert.AreEqual(data, decompressed);
		}

		[Test]
		public void CompressAndDecompress_workCorrectly_onArraysOfDifferentSizes([Values(false, true)] bool useDictionary)
		{
			var dict = useDictionary ? BuildDictionary() : null;
			using(var compressionOptions = new CompressionOptions(dict))
			using(var decompressionOptions = new DecompressionOptions(dict))
			using(var compressor = new Compressor(compressionOptions))
			using(var decompressor = new Decompressor(decompressionOptions))
			{
				for(var i = 2; i < 100000; i += 3000)
				{
					var data = GenerateBuffer(i);

					var decompressed = decompressor.Unwrap(compressor.Wrap(data));

					CollectionAssert.AreEqual(data, decompressed);
				}
			}
		}

		[Test]
		public void CompressAndDecompress_workCorrectly_ifDifferentInstancesRunInDifferentThreads([Values(false, true)] bool useDictionary)
		{
			var dict = useDictionary ? BuildDictionary() : null;
			using(var compressionOptions = new CompressionOptions(dict))
			using(var decompressionOptions = new DecompressionOptions(dict))
				Enumerable.Range(0, 100)
					.AsParallel().WithDegreeOfParallelism(50)
					.ForAll(_ =>
					{
						using(var compressor = new Compressor(compressionOptions))
						using(var decompressor = new Decompressor(decompressionOptions))
						{
							for(var i = 2; i < 100000; i += 30000)
							{
								var data = GenerateBuffer(i);

								var decompressed = decompressor.Unwrap(compressor.Wrap(data));

								CollectionAssert.AreEqual(data, decompressed);
							}
						}
					});
		}

		[Test, Explicit("stress")]
		public void CompressAndDecompress_workCorrectly_stress([Values(false, true)] bool useDictionary)
		{
			long i = 0L;
			var data = GenerateBuffer(65536);
			var dict = useDictionary ? BuildDictionary() : null;
			using(var compressionOptions = new CompressionOptions(dict))
			using(var decompressionOptions = new DecompressionOptions(dict))
				Enumerable.Range(0, 10000)
					.AsParallel().WithDegreeOfParallelism(100)
					.ForAll(_ =>
					{
						using(var compressor = new Compressor(compressionOptions))
						using(var decompressor = new Decompressor(decompressionOptions))
						{
							var decompressed = decompressor.Unwrap(compressor.Wrap(data));
							if(Interlocked.Increment(ref i) % 100 == 0)
								GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
							CollectionAssert.AreEqual(data, decompressed);
						}
					});
		}

		private static byte[] BuildDictionary()
		{
			return DictBuilder.TrainFromBuffer(Enumerable.Range(0, 8).Select(_ => GenerateSample()).ToArray(), 1024);
		}

		private static byte[] GenerateSample()
		{
			return Enumerable.Range(0, 10)
				.SelectMany(_ => Encoding.ASCII.GetBytes($"['a': 'constant_field', 'b': '{Random.Next()}', 'c': {Random.Next()}, 'd': '{(Random.Next(1) == 1 ? "almost" : "sometimes")} constant field']"))
				.ToArray();
		}

		private static byte[] GenerateBuffer(int size)
		{
			return Enumerable.Range(0, size)
				.Select(i => unchecked((byte)i))
				.ToArray();
		}

		private static readonly Random Random = new Random(1234);
	}
}
