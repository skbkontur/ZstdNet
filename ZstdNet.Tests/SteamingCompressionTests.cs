using System;
using System.IO;
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

        public static MemoryStream GetSmallStream(DataFill dataFill) => new MemoryStream(GetBuffer(SmallBufferSize, dataFill));

        public static MemoryStream GetLargeStream(DataFill dataFill) => new MemoryStream(GetBuffer(LargeBufferSize, dataFill));

        public static MemoryStream GetStream(int length, DataFill dataFill) => new MemoryStream(GetBuffer(length, dataFill));

        public static byte[] GetSmallBuffer(DataFill dataFill) => GetBuffer(SmallBufferSize, dataFill);

        public static byte[] GetLargeBuffer(DataFill dataFill) => GetBuffer(LargeBufferSize, dataFill);

        public static byte[] GetBuffer(int length, DataFill dataFill)
        {
            var buffer = new byte[length];
            if (dataFill == DataFill.Random)
            {
                Random.NextBytes(buffer);
            }
            else
            {
                for (int i = 0; i < buffer.Length; i++)
                    buffer[i] = (byte)(i % 256);
            }
            return buffer;
        }
    }

    [TestFixture]
    public class SteamingTests
    {
        [Test]
        public void CompressionImprovesWithDictionary()
        {
            var trainingData = new byte[100][];
            for (int i = 0; i < trainingData.Length; i++)
                trainingData[i] = DataGenerator.GetSmallBuffer(DataFill.Random);

            var dict = DictBuilder.TrainFromBuffer(trainingData);

            var compressionOptions = new CompressionOptions(dict);

            var testStream = DataGenerator.GetSmallStream(DataFill.Random);
            var normalResultStream = new MemoryStream();

            using (var compressionStream = new CompressorStream(normalResultStream))
                testStream.CopyTo(compressionStream);

            var dictResultStream = new MemoryStream();

            using (var compressionStream = new CompressorStream(dictResultStream, compressionOptions))
                testStream.CopyTo(compressionStream);

            Assert.Greater(normalResultStream.Length, dictResultStream.Length);
        }

        [Test]
        public void CompressionShrinksData()
        {
            var inStream = DataGenerator.GetLargeStream(DataFill.Sequential);
            var outStream = new MemoryStream();

            using (var compressionStream = new CompressorStream(outStream))            
                inStream.CopyTo(compressionStream);            

            Assert.Greater(inStream.Length, outStream.Length);
        }

        [Test]
        public void RoundTrip_BatchToStreaming()
        {
            var testBuffer = DataGenerator.GetLargeBuffer(DataFill.Sequential);

            byte[] compressedBuffer;

            using (var compressor = new Compressor())
                compressedBuffer = compressor.Wrap(testBuffer);

            var resultStream = new MemoryStream();

            using (var decompressionStream = new DecompressorStream(new MemoryStream(compressedBuffer)))
                decompressionStream.CopyTo(resultStream);

            Validate(testBuffer, resultStream.ToArray());
        }

        [Test]
        public void RoundTrip_StreamingToBatch()
        {
            var testStream = DataGenerator.GetLargeStream(DataFill.Sequential);

            var tempStream = new MemoryStream();

            using (var compressorStream = new CompressorStream(testStream))
                tempStream.CopyTo(compressorStream);

            byte[] resultBuffer;

            using (var decompressor = new Decompressor())
                resultBuffer = decompressor.Unwrap(tempStream.ToArray());

            Validate(tempStream.ToArray(), resultBuffer);
        }

        [Test]
        public void RoundTrip_StreamingStreaming()
        {
            var testStream = DataGenerator.GetLargeStream(DataFill.Sequential);
            var tempStream = new MemoryStream();            

            using (var compressionStream = new CompressorStream(tempStream))
                testStream.CopyTo(compressionStream);            

            var resultStream = new MemoryStream();
            tempStream.Position = 0;
            using (var decompressionsStream = new DecompressorStream(tempStream))
                decompressionsStream.CopyTo(resultStream);

            Validate(testStream.ToArray(), resultStream.ToArray());
        }

        private static void Validate(byte[] expected, byte[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length, "Decompressed Stream length is different than input stream");

            for (int i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], actual[i], $"Decompressed byte index {i} is different than input stream");
        }
    }
}
