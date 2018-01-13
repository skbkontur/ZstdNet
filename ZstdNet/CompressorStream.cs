using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZstdNet
{
    public class CompressorStream : Stream, IDisposable
    {
        private IntPtr ZSTD_CStream;
        public readonly CompressionOptions Options;
        private readonly ArraySegmentPtr OutputBuffer;

        private readonly Stream InnerStream;

        public CompressorStream(Stream stream) : this(stream, new CompressionOptions(CompressionOptions.DefaultCompressionLevel))
        {
            
        }

        public CompressorStream(Stream stream, CompressionOptions compressionOptions, int bufferSize = 32 * 1024)
        {
            InnerStream = stream;

            ZSTD_CStream = ExternMethods.ZSTD_createCStream();
            
            OutputBuffer = new ArraySegmentPtr(new ArraySegment<byte>(new byte[Compressor.GetCompressBound(bufferSize)]));

            ExternMethods.ZSTD_initCStream(ZSTD_CStream, compressionOptions.CompressionLevel).EnsureZstdSuccess();
        }

        ~CompressorStream()
        {
            Dispose(true);
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override void Flush() => InnerStream.Flush();

        public override void Write(byte[] buffer, int offset, int count)
        {
            using (var ptr = new ArraySegmentPtr(buffer, offset, count))
            {
                var inBuffer = new ExternMethods.ZSTD_Buffer(ptr);
                
                while ((int)inBuffer.pos < (int)inBuffer.size)
                {
                    var outBuffer = new ExternMethods.ZSTD_Buffer(OutputBuffer);
                    ExternMethods.ZSTD_compressStream(ZSTD_CStream, ref outBuffer, ref inBuffer).EnsureZstdSuccess();
                    InnerStream.Write(OutputBuffer.Buffer, 0, (int)outBuffer.pos);
                }
            }
        }

        public override long Position
        {
            get => InnerStream.Position;
            set => throw new NotSupportedException();
        }
        public override long Length => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            var outBuffer = new ExternMethods.ZSTD_Buffer(OutputBuffer);
            ExternMethods.ZSTD_endStream(ZSTD_CStream, ref outBuffer);
            ExternMethods.ZSTD_freeCStream(ZSTD_CStream);
            OutputBuffer.Dispose();
        }
    }
}
