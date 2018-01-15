using System;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace ZstdNet.Benchmarks
{
    [MemoryDiagnoser]
    public class CompressionOverheadBenchmarks
    {
        const int TestSize = 1024;

        byte[] UncompressedData = new byte[TestSize];
        byte[] CompressedData;

        byte[] Buffer = new byte[Compressor.GetCompressBound(TestSize)];

        Compressor Compressor = new Compressor(new CompressionOptions(1));
        Decompressor Decompressor = new Decompressor();

        public CompressionOverheadBenchmarks()
        {
            var r = new Random(0);
            r.NextBytes(UncompressedData);

            CompressedData = Compressor.Wrap(UncompressedData);
        }

        [Benchmark]
        public void Compress1KBRandom()
        {
            Compressor.Wrap(UncompressedData, Buffer, 0);
        }

        [Benchmark]
        public void Decompress1KBRandom()
        {
            Decompressor.Unwrap(CompressedData, Buffer, 0);
        }
    }
}
