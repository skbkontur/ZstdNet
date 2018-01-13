using System;
using System.IO;
using NUnit.Framework;

namespace ZstdNet.Tests
{
    [TestFixture]
    public class SteamingCompressionTests
    {
        private static readonly Random Random = new Random(1234);
        private static MemoryStream GetStream()
        {
            var buffer = new byte[1024];
            Random.NextBytes(buffer);
            return new MemoryStream(buffer);
        }

        [Test]
        public void CompressionShrinksData()
        {
            var inStream = GetStream();
            var outStream = new MemoryStream();

            using (var compressionStream = new CompressorStream(outStream))
            {
                inStream.CopyTo(compressionStream);
            }

            Assert.Greater(inStream.Length, outStream.Length);
        }
    }
}
