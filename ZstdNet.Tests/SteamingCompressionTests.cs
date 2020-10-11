using System;
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

		public const int LargeBufferSize = 1 * 1024 * 1024;
		public const int SmallBufferSize = 1 * 1024;

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
			var trainingData = new byte[100][];
			for(int i = 0; i < trainingData.Length; i++)
				trainingData[i] = DataGenerator.GetSmallBuffer(DataFill.Random);

			var dict = DictBuilder.TrainFromBuffer(trainingData);
			var compressionOptions = new CompressionOptions(dict);

			var dataStream = DataGenerator.GetSmallStream(DataFill.Random);

			var normalResultStream = new MemoryStream();
			using(var compressionStream = new CompressionStream(normalResultStream))
				dataStream.CopyTo(compressionStream);

			var dictResultStream = new MemoryStream();
			using(var compressionStream = new CompressionStream(dictResultStream, compressionOptions))
				dataStream.CopyTo(compressionStream);

			Assert.Greater(normalResultStream.Length, dictResultStream.Length);
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
			[Values(1, 2, 7, 101, 1024, 65535, DataGenerator.LargeBufferSize, DataGenerator.LargeBufferSize + 1)] int zstdBufferSize,
			[Values(1, 2, 7, 101, 1024, 65535, DataGenerator.LargeBufferSize, DataGenerator.LargeBufferSize + 1)] int copyBufferSize)
		{
			var testStream = DataGenerator.GetLargeStream(DataFill.Sequential);

			var tempStream = new MemoryStream();
			using(var compressionStream = new CompressionStream(tempStream, zstdBufferSize))
				testStream.CopyTo(compressionStream, copyBufferSize);

			tempStream.Seek(0, SeekOrigin.Begin);

			var resultStream = new MemoryStream();
			using(var decompressionStream = new DecompressionStream(tempStream, zstdBufferSize))
				decompressionStream.CopyTo(resultStream, copyBufferSize);

			Assert.AreEqual(testStream.ToArray(), resultStream.ToArray());
		}

		[Test, Combinatorial, Parallelizable(ParallelScope.Children)]
		public async Task RoundTrip_StreamingToStreamingAsync(
			[Values(1, 2, 7, 101, 1024, 65535, DataGenerator.LargeBufferSize, DataGenerator.LargeBufferSize + 1)] int zstdBufferSize,
			[Values(1, 2, 7, 101, 1024, 65535, DataGenerator.LargeBufferSize, DataGenerator.LargeBufferSize + 1)] int copyBufferSize)
		{
			var testStream = DataGenerator.GetLargeStream(DataFill.Sequential);

			var tempStream = new MemoryStream();
			await using(var compressionStream = new CompressionStream(tempStream, zstdBufferSize))
				await testStream.CopyToAsync(compressionStream, copyBufferSize);

			tempStream.Seek(0, SeekOrigin.Begin);

			var resultStream = new MemoryStream();
			await using(var decompressionStream = new DecompressionStream(tempStream, zstdBufferSize))
				await decompressionStream.CopyToAsync(resultStream, copyBufferSize);

			Assert.AreEqual(testStream.ToArray(), resultStream.ToArray());
		}

		[Test, Explicit("stress")]
		public void RoundTrip_StreamingToStreaming_Stress([Values(true, false)] bool async)
		{
			long i = 0;
			Enumerable.Range(0, 10000)
				.AsParallel()
				.WithDegreeOfParallelism(Environment.ProcessorCount * 4)
				.ForAll(_ =>
				{
					var buffer = new byte[13];
					var testStream = DataGenerator.GetSmallStream(DataFill.Random);

					var tempStream = new MemoryStream();
					using(var compressionStream = new CompressionStream(tempStream, 511))
					{
						int bytesRead;
						int offset = (int)(Interlocked.Read(ref i) % buffer.Length);
						while((bytesRead = testStream.Read(buffer, offset, buffer.Length - offset)) > 0)
						{
							if(async)
								compressionStream.WriteAsync(buffer, offset, bytesRead).GetAwaiter().GetResult();
							else
								compressionStream.Write(buffer, offset, bytesRead);
							if(Interlocked.Increment(ref i) % 100 == 0)
								GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
						}
					}

					tempStream.Seek(0, SeekOrigin.Begin);

					var resultStream = new MemoryStream();
					using(var decompressionStream = new DecompressionStream(tempStream, 511))
					{
						int bytesRead;
						int offset = (int)(Interlocked.Read(ref i) % buffer.Length);
						while((bytesRead = async ? decompressionStream.ReadAsync(buffer, offset, buffer.Length - offset).GetAwaiter().GetResult() : decompressionStream.Read(buffer, offset, buffer.Length - offset)) > 0)
						{
							resultStream.Write(buffer, offset, bytesRead);
							if(Interlocked.Increment(ref i) % 100 == 0)
								GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
						}
					}

					Assert.AreEqual(testStream.ToArray(), resultStream.ToArray());
				});
		}
	}
}
