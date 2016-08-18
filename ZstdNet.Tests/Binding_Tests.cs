using System;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace ZstdNet.Tests
{
	[TestFixture]
	public class Binding_Tests
	{
		[Test]
		public void CompressAndDecompress_workCorrectly_withoutDictionary()
		{
			var data = GenerateSample();

			byte[] compressed;
			using(var compressor = new Compressor())
				compressed = compressor.Wrap(data);
			byte[] decompressed;
			using(var decompressor = new Decompressor())
				decompressed = decompressor.Unwrap(compressed);

			CollectionAssert.AreEqual(data, decompressed);
		}

		[Test]
		public void CompressAndDecompress_workCorrectly_withDictionary()
		{
			var data = GenerateSample();

			var dict = BuildDictionary();
			byte[] compressed;
			using(var compressor = new Compressor(dict))
				compressed = compressor.Wrap(data);
			byte[] decompressed;
			using(var decompressor = new Decompressor(dict))
				decompressed = decompressor.Unwrap(compressed);

			CollectionAssert.AreEqual(data, decompressed);
		}

		[Test]
		public void Decompress_withoutDictionary_worksCorrectly_onDataWithDictionary()
		{
			var data = GenerateSample();
			byte[] compressed;
			using(var compressor = new Compressor())
				compressed = compressor.Wrap(data);

			var dict = BuildDictionary();
			byte[] decompressed;
			using(var decompressor = new Decompressor(dict))
				decompressed = decompressor.Unwrap(compressed);

			CollectionAssert.AreEqual(data, decompressed);
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
			using(var compressor = new Compressor(BuildDictionary()))
				compressedWithDict = compressor.Wrap(data);

			Assert.Greater(compressedWithoutDict.Length, compressedWithDict.Length);
		}

		[Test]
		public void Decompress_throwsZstdException_onInvalidData()
		{
			var data = GenerateSample(); // This isn't data in compressed format

			using(var decompressor = new Decompressor())
				Assert.Throws<ZstdException>(() => decompressor.Unwrap(data));
		}

		[Test]
		public void Compress_worksWithArraySegment()
		{
			var data = GenerateSample();
			var segment = new ArraySegment<byte>(data, 2, data.Length - 5);

			byte[] compressed;
			using(var compressor = new Compressor())
				compressed = compressor.Wrap(segment);

			byte[] decompressed;
			using(var decompressor = new Decompressor())
				decompressed = decompressor.Unwrap(compressed);
			CollectionAssert.AreEqual(segment, decompressed);
		}

		[Test]
		public void Decompress_worksWithArraySegment()
		{
			var data = GenerateSample();
			byte[] compressed;
			using(var compressor = new Compressor())
				compressed = compressor.Wrap(data);
			compressed = new byte[] {1, 2}.Concat(compressed).Concat(new byte[] {4, 5, 6})
				.ToArray();
			var segment = new ArraySegment<byte>(compressed, 2, compressed.Length - 5);

			byte[] decompressed;
			using(var decompressor = new Decompressor())
				decompressed = decompressor.Unwrap(segment);

			CollectionAssert.AreEqual(data, decompressed);
		}

		[Test]
		public void CompressAndDecompress_workCorrectly_onEmptyBuffer()
		{
			var data = new byte[0];

			byte[] compressed;
			using(var compressor = new Compressor())
				compressed = compressor.Wrap(data);
			byte[] decompressed;
			using(var decompressor = new Decompressor())
				decompressed = decompressor.Unwrap(compressed);

			CollectionAssert.AreEqual(data, decompressed);
		}

		[Test]
		public void CompressAndDecompress_workCorrectly_onOneByteBuffer()
		{
			var data = new byte[] { 42 };

			byte[] compressed;
			using(var compressor = new Compressor())
				compressed = compressor.Wrap(data);
			byte[] decompressed;
			using(var decompressor = new Decompressor())
				decompressed = decompressor.Unwrap(compressed);

			CollectionAssert.AreEqual(data, decompressed);
		}

		[Test]
		public void CompressAndDecompress_workCorrectly_onArraysOfDifferentSizes()
		{
			using (var compressor = new Compressor())
			using (var decompressor = new Decompressor())
			{
				for (var i = 2; i < 100000; i += 3000)
				{
					var data = GenerateBuffer(i);

					var decompressed = decompressor.Unwrap(compressor.Wrap(data));

					CollectionAssert.AreEqual(data, decompressed);
				}
			}
		}

		[Test]
		public void CompressAndDecompress_workCorrectly_ifDifferentInstancesRunInDifferentThreads()
		{
			Enumerable.Range(0, 100)
				.AsParallel().WithDegreeOfParallelism(50)
				.ForAll(_ =>
				{
					using (var compressor = new Compressor())
					using (var decompressor = new Decompressor())
					{
						for (var i = 2; i < 100000; i += 30000)
						{
							var data = GenerateBuffer(i);

							var decompressed = decompressor.Unwrap(compressor.Wrap(data));

							CollectionAssert.AreEqual(data, decompressed);
						}
					}
				});
		}

		private static byte[] BuildDictionary()
		{
			return DictBuilder.TrainFromBuffer(Enumerable.Range(0, 5).Select(_ => GenerateSample()).ToArray(), 1024);
		}

		private static byte[] GenerateSample()
		{
			return Enumerable.Range(0, 3)
				.SelectMany(_ => Encoding.ASCII.GetBytes(string.Format("['a': 'constant_field', 'b': '{0}', 'c': {1}, 'd': '{2} constant field']",
					Random.Next(), Random.Next(), Random.Next(1) == 1 ? "almost" : "sometimes")))
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
