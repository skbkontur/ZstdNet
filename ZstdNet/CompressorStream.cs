using System;
using System.IO;

using static ZstdNet.ExternMethods;

namespace ZstdNet
{
    public class CompressorStream : Stream, IDisposable
    {
        private readonly IntPtr CStream;
        private readonly Stream InnerStream;
        private readonly CompressionOptions Options;
        private readonly ArraySegmentPtr OutputBuffer;

        private ZSTD_Buffer OutputBufferState;

        public CompressorStream(Stream stream) : this(stream, CompressionOptions.DefaultCompressionOptions)
        {
            
        }

        public CompressorStream(Stream stream, CompressionOptions options)
        {
            InnerStream = stream;
            Options = options;
            CStream = ZSTD_createCStream();
            if (options.Cdict == IntPtr.Zero)
                ZSTD_initCStream(CStream, options.CompressionLevel).EnsureZstdSuccess();
            else
                ZSTD_initCStream_usingCDict(CStream, options.Cdict).EnsureZstdSuccess();

            OutputBuffer = CreateOutputBuffer();
            InitializedOutputBufferState();
        }

        private static ArraySegmentPtr CreateOutputBuffer()
        {
            var outputBufferSize = (int)ZSTD_CStreamOutSize();
            var outputArray = new byte[outputBufferSize];
            return new ArraySegmentPtr(outputArray, 0, outputArray.Length);
        }

        private void InitializedOutputBufferState()
        {
            OutputBufferState = new ZSTD_Buffer(OutputBuffer);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            using (var inBuffer = new ArraySegmentPtr(buffer, offset, count))
            {
                var inputBufferState = new ZSTD_Buffer(inBuffer);

                while (!inputBufferState.IsFullyConsumed)
                {
                    if (OutputBufferState.IsFullyConsumed)
                        FlushOutputBuffer();

                    ZSTD_compressStream(CStream, ref OutputBufferState, ref inputBufferState).EnsureZstdSuccess();
                }
            }
        }

        private void FlushOutputBuffer()
        {
            InnerStream.Write(OutputBuffer.Array, 0, OutputBufferState.IntPos);
            OutputBufferState.pos = UIntPtr.Zero;
        }

        ~CompressorStream()
        {
            Close();
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
            FlushOutputBuffer();
            InnerStream.Flush();
        }
       
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            try
            {
                var endResult = ZSTD_endStream(CStream, ref OutputBufferState).EnsureZstdSuccess();
                if (UIntPtr.Zero != endResult)
                {
                    FlushOutputBuffer();

                    ZSTD_endStream(CStream, ref OutputBufferState).EnsureZstdSuccess();
                }

                FlushOutputBuffer();
            }
            finally
            {
                ZSTD_freeCStream(CStream);
                OutputBuffer.Dispose();
            }
        }
    }
}
