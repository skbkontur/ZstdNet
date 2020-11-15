using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace ZstdNet.Tests
{
	public enum DataFill
	{
		Random,
		Sequential
	}

	internal static class DataGenerator
	{
		private static readonly Random Random = new Random(1234);

		public const int LargeBufferSize = 1024 * 1024;
		public const int SmallBufferSize = 1024;

		public static MemoryStream GetSmallStream(DataFill dataFill) => GetStream(SmallBufferSize, dataFill);
		public static MemoryStream GetLargeStream(DataFill dataFill) => GetStream(LargeBufferSize, dataFill);
		public static MemoryStream GetStream(int length, DataFill dataFill) => new MemoryStream(GetBuffer(length, dataFill));

		public static byte[] GetSmallBuffer(DataFill dataFill) => GetBuffer(SmallBufferSize, dataFill);
		public static byte[] GetLargeBuffer(DataFill dataFill) => GetBuffer(LargeBufferSize, dataFill);

		public static byte[] GetBuffer(int length, DataFill dataFill)
		{
			var buffer = new byte[length];
			if(dataFill == DataFill.Random)
				Random.NextBytes(buffer);
			else
			{
				for(int i = 0; i < buffer.Length; i++)
					buffer[i] = (byte)(i % 256);
			}

			return buffer;
		}
	}

	[TestFixture]
	public class SteamingTests
	{
		[Test]
		public void StreamingCompressionZeroAndOneByte()
		{
			var data = new byte[] {0, 0, 0, 1, 2, 3, 4, 0, 0, 0};

			var tempStream = new MemoryStream();
			using(var compressionStream = new CompressionStream(tempStream))
			{
				compressionStream.Write(data, 0, 0);
				compressionStream.Write(ReadOnlySpan<byte>.Empty);
				compressionStream.WriteAsync(data, 0, 0).GetAwaiter().GetResult();
				compressionStream.WriteAsync(ReadOnlyMemory<byte>.Empty).GetAwaiter().GetResult();

				compressionStream.Write(data, 3, 1);
				compressionStream.Write(new ReadOnlySpan<byte>(data, 4, 1));
				compressionStream.Flush();
				compressionStream.WriteAsync(data, 5, 1).GetAwaiter().GetResult();
				compressionStream.WriteAsync(new ReadOnlyMemory<byte>(data, 6, 1)).GetAwaiter().GetResult();
				compressionStream.FlushAsync().GetAwaiter().GetResult();
			}

			tempStream.Seek(0, SeekOrigin.Begin);

			var result = new byte[data.Length];
			using(var decompressionStream = new DecompressionStream(tempStream))
			{
				Assert.AreEqual(0, decompressionStream.Read(result, 0, 0));
				Assert.AreEqual(0, decompressionStream.Read(Span<byte>.Empty));
				Assert.AreEqual(0, decompressionStream.ReadAsync(result, 0, 0).GetAwaiter().GetResult());
				Assert.AreEqual(0, decompressionStream.ReadAsync(Memory<byte>.Empty).GetAwaiter().GetResult());

				Assert.AreEqual(1, decompressionStream.Read(result, 3, 1));
				Assert.AreEqual(1, decompressionStream.Read(new Span<byte>(result, 4, 1)));
				Assert.AreEqual(1, decompressionStream.ReadAsync(result, 5, 1).GetAwaiter().GetResult());
				Assert.AreEqual(1, decompressionStream.ReadAsync(new Memory<byte>(result, 6, 1)).GetAwaiter().GetResult());
			}

			Assert.AreEqual(data, result);
		}


		[TestCase(new byte[0], 0, 0)]
		[TestCase(new byte[] {1, 2, 3}, 1, 2)]
		[TestCase(new byte[] {1, 2, 3}, 0, 2)]
		[TestCase(new byte[] {1, 2, 3}, 1, 1)]
		[TestCase(new byte[] {1, 2, 3}, 0, 3)]
		public void StreamingCompressionSimpleWrite(byte[] data, int offset, int count)
		{
			var tempStream = new MemoryStream();
			using(var compressionStream = new CompressionStream(tempStream))
				compressionStream.Write(data, offset, count);

			tempStream.Seek(0, SeekOrigin.Begin);

			var resultStream = new MemoryStream();
			using(var decompressionStream = new DecompressionStream(tempStream))
				decompressionStream.CopyTo(resultStream);

			var dataToCompress = new byte[count];
			Array.Copy(data, offset, dataToCompress, 0, count);

			Assert.AreEqual(dataToCompress, resultStream.ToArray());
		}

		[TestCase(1)]
		[TestCase(2)]
		[TestCase(3)]
		[TestCase(5)]
		[TestCase(9)]
		[TestCase(10)]
		public void StreamingDecompressionSimpleRead(int readCount)
		{
			var data = new byte[] {0, 1, 2, 3, 4, 5, 6, 7, 8, 9};

			var tempStream = new MemoryStream();
			using(var compressionStream = new CompressionStream(tempStream))
				compressionStream.Write(data, 0, data.Length);

			tempStream.Seek(0, SeekOrigin.Begin);

			var buffer = new byte[data.Length];
			using(var decompressionStream = new DecompressionStream(tempStream))
			{
				int bytesRead;
				int totalBytesRead = 0;
				while((bytesRead = decompressionStream.Read(buffer, totalBytesRead, Math.Min(readCount, buffer.Length - totalBytesRead))) > 0)
				{
					Assert.LessOrEqual(bytesRead, readCount);
					totalBytesRead += bytesRead;
				}

				Assert.AreEqual(data.Length, totalBytesRead);
			}

			Assert.AreEqual(data, buffer);
		}

		[Test]
		public void StreamingCompressionFlushDataFromInternalBuffers()
		{
			var testBuffer = new byte[1];

			var tempStream = new MemoryStream();
			using(var compressionStream = new CompressionStream(tempStream))
			{
				compressionStream.Write(testBuffer, 0, testBuffer.Length);
				compressionStream.Flush();

				Assert.Greater(tempStream.Length, 0);
				tempStream.Seek(0, SeekOrigin.Begin);

				//NOTE: without ZSTD_endStream call on compression
				var resultStream = new MemoryStream();
				using(var decompressionStream = new DecompressionStream(tempStream))
					decompressionStream.CopyTo(resultStream);

				Assert.AreEqual(testBuffer, resultStream.ToArray());
			}
		}

		[Test]
		public void CompressionImprovesWithDictionary()
		{
			var dict = TrainDict();
			var compressionOptions = new CompressionOptions(dict);

			var dataStream = DataGenerator.GetSmallStream(DataFill.Sequential);

			var normalResultStream = new MemoryStream();
			using(var compressionStream = new CompressionStream(normalResultStream))
				dataStream.CopyTo(compressionStream);

			dataStream.Seek(0, SeekOrigin.Begin);

			var dictResultStream = new MemoryStream();
			using(var compressionStream = new CompressionStream(dictResultStream, compressionOptions))
				dataStream.CopyTo(compressionStream);

			Assert.Greater(normalResultStream.Length, dictResultStream.Length);

			dictResultStream.Seek(0, SeekOrigin.Begin);

			var resultStream = new MemoryStream();
			using(var decompressionStream = new DecompressionStream(dictResultStream, new DecompressionOptions(dict)))
				decompressionStream.CopyTo(resultStream);

			Assert.AreEqual(dataStream.ToArray(), resultStream.ToArray());
		}

		[Test]
		public void CompressionShrinksData()
		{
			var dataStream = DataGenerator.GetLargeStream(DataFill.Sequential);

			var resultStream = new MemoryStream();
			using(var compressionStream = new CompressionStream(resultStream))
				dataStream.CopyTo(compressionStream);

			Assert.Greater(dataStream.Length, resultStream.Length);
		}

		[Test]
		public void RoundTrip_BatchToStreaming()
		{
			var data = DataGenerator.GetLargeBuffer(DataFill.Sequential);

			byte[] compressed;
			using(var compressor = new Compressor())
				compressed = compressor.Wrap(data);

			var resultStream = new MemoryStream();
			using(var decompressionStream = new DecompressionStream(new MemoryStream(compressed)))
				decompressionStream.CopyTo(resultStream);

			Assert.AreEqual(data, resultStream.ToArray());
		}

		[Test]
		public void RoundTrip_StreamingToBatch()
		{
			var dataStream = DataGenerator.GetLargeStream(DataFill.Sequential);

			var tempStream = new MemoryStream();
			using(var compressionStream = new CompressionStream(tempStream))
				dataStream.CopyTo(compressionStream);

			var resultBuffer = new byte[dataStream.Length];
			using(var decompressor = new Decompressor())
				Assert.AreEqual(dataStream.Length, decompressor.Unwrap(tempStream.ToArray(), resultBuffer, 0, false));

			Assert.AreEqual(dataStream.ToArray(), resultBuffer);
		}

		[Test, Combinatorial, Parallelizable(ParallelScope.Children)]
		public void RoundTrip_StreamingToStreaming(
			[Values(false, true)] bool useDict, [Values(false, true)] bool advanced,
			[Values(1, 2, 7, 101, 1024, 65535, DataGenerator.LargeBufferSize, DataGenerator.LargeBufferSize + 1)] int zstdBufferSize,
			[Values(1, 2, 7, 101, 1024, 65535, DataGenerator.LargeBufferSize, DataGenerator.LargeBufferSize + 1)] int copyBufferSize)
		{
			var dict = useDict ? TrainDict() : null;
			var testStream = DataGenerator.GetLargeStream(DataFill.Sequential);

			const int offset = 1;
			var buffer = new byte[copyBufferSize + offset + 1];

			var tempStream = new MemoryStream();
			using(var compressionOptions = new CompressionOptions(dict, advanced ? new Dictionary<ZSTD_cParameter, int> {{ZSTD_cParameter.ZSTD_c_windowLog, 11}, {ZSTD_cParameter.ZSTD_c_checksumFlag, 1}, {ZSTD_cParameter.ZSTD_c_nbWorkers, 4}} : null))
			using(var compressionStream = new CompressionStream(tempStream, compressionOptions, zstdBufferSize))
			{
				int bytesRead;
				while((bytesRead = testStream.Read(buffer, offset, copyBufferSize)) > 0)
					compressionStream.Write(buffer, offset, bytesRead);
			}

			tempStream.Seek(0, SeekOrigin.Begin);

			var resultStream = new MemoryStream();
			using(var decompressionOptions = new DecompressionOptions(dict, advanced ? new Dictionary<ZSTD_dParameter, int> {{ZSTD_dParameter.ZSTD_d_windowLogMax, 11}} : null))
			using(var decompressionStream = new DecompressionStream(tempStream, decompressionOptions, zstdBufferSize))
			{
				int bytesRead;
				while((bytesRead = decompressionStream.Read(buffer, offset, copyBufferSize)) > 0)
					resultStream.Write(buffer, offset, bytesRead);
			}

			Assert.AreEqual(testStream.ToArray(), resultStream.ToArray());
		}

		[Test, Combinatorial, Parallelizable(ParallelScope.Children)]
		public async Task RoundTrip_StreamingToStreamingAsync(
			[Values(false, true)] bool useDict, [Values(false, true)] bool advanced,
			[Values(1, 2, 7, 101, 1024, 65535, DataGenerator.LargeBufferSize, DataGenerator.LargeBufferSize + 1)] int zstdBufferSize,
			[Values(1, 2, 7, 101, 1024, 65535, DataGenerator.LargeBufferSize, DataGenerator.LargeBufferSize + 1)] int copyBufferSize)
		{
			var dict = useDict ? TrainDict() : null;
			var testStream = DataGenerator.GetLargeStream(DataFill.Sequential);

			const int offset = 1;
			var buffer = new byte[copyBufferSize + offset + 1];

			var tempStream = new MemoryStream();
			using(var compressionOptions = new CompressionOptions(dict, advanced ? new Dictionary<ZSTD_cParameter, int> {{ZSTD_cParameter.ZSTD_c_windowLog, 11}, {ZSTD_cParameter.ZSTD_c_checksumFlag, 1}, {ZSTD_cParameter.ZSTD_c_nbWorkers, 4}} : null))
			await using(var compressionStream = new CompressionStream(tempStream, compressionOptions, zstdBufferSize))
			{
				int bytesRead;
				while((bytesRead = await testStream.ReadAsync(buffer, offset, copyBufferSize)) > 0)
					await compressionStream.WriteAsync(buffer, offset, bytesRead);
			}

			tempStream.Seek(0, SeekOrigin.Begin);

			var resultStream = new MemoryStream();
			using(var decompressionOptions = new DecompressionOptions(dict, advanced ? new Dictionary<ZSTD_dParameter, int> {{ZSTD_dParameter.ZSTD_d_windowLogMax, 11}} : null))
			await using(var decompressionStream = new DecompressionStream(tempStream, decompressionOptions, zstdBufferSize))
			{
				int bytesRead;
				while((bytesRead = await decompressionStream.ReadAsync(buffer, offset, copyBufferSize)) > 0)
					await resultStream.WriteAsync(buffer, offset, bytesRead);
			}

			Assert.AreEqual(testStream.ToArray(), resultStream.ToArray());
		}

		[Test, Explicit("stress")]
		public void RoundTrip_StreamingToStreaming_Stress([Values(true, false)] bool useDict, [Values(true, false)] bool async)
		{
			long i = 0;
			var dict = useDict ? TrainDict() : null;
			var compressionOptions = new CompressionOptions(dict);
			var decompressionOptions = new DecompressionOptions(dict);
			Enumerable.Range(0, 10000)
				.AsParallel()
				.WithDegreeOfParallelism(Environment.ProcessorCount * 4)
				.ForAll(n =>
				{
					var testStream = DataGenerator.GetSmallStream(DataFill.Sequential);
					var cBuffer = new byte[1 + (int)(n % (testStream.Length * 11))];
					var dBuffer = new byte[1 + (int)(n % (testStream.Length * 13))];

					var tempStream = new MemoryStream();
					using(var compressionStream = new CompressionStream(tempStream, compressionOptions, 1 + (int)(n % (testStream.Length * 17))))
					{
						int bytesRead;
						int offset = n % cBuffer.Length;
						while((bytesRead = testStream.Read(cBuffer, offset, cBuffer.Length - offset)) > 0)
						{
							if(async)
								compressionStream.WriteAsync(cBuffer, offset, bytesRead).GetAwaiter().GetResult();
							else
								compressionStream.Write(cBuffer, offset, bytesRead);
							if(Interlocked.Increment(ref i) % 100 == 0)
								GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
						}
					}

					tempStream.Seek(0, SeekOrigin.Begin);

					var resultStream = new MemoryStream();
					using(var decompressionStream = new DecompressionStream(tempStream, decompressionOptions, 1 + (int)(n % (testStream.Length * 19))))
					{
						int bytesRead;
						int offset = n % dBuffer.Length;
						while((bytesRead = async ? decompressionStream.ReadAsync(dBuffer, offset, dBuffer.Length - offset).GetAwaiter().GetResult() : decompressionStream.Read(dBuffer, offset, dBuffer.Length - offset)) > 0)
						{
							resultStream.Write(dBuffer, offset, bytesRead);
							if(Interlocked.Increment(ref i) % 100 == 0)
								GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
						}
					}

					Assert.AreEqual(testStream.ToArray(), resultStream.ToArray());
				});
			GC.KeepAlive(compressionOptions);
			GC.KeepAlive(decompressionOptions);
		}

		private static byte[] TrainDict()
		{
			var trainingData = new byte[100][];
			for(int i = 0; i < trainingData.Length; i++)
				trainingData[i] = DataGenerator.GetSmallBuffer(DataFill.Sequential);
			return DictBuilder.TrainFromBuffer(trainingData);
		}
	}
}
