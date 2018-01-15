using System;
using System.IO;

using static ZstdNet.ExternMethods;

namespace ZstdNet
{
    public class DecompressorStream : Stream, IDisposable
    {
        private readonly IntPtr DStream;
        private readonly Stream InnerStream;
        private readonly DecompressionOptions Options;
        private readonly ArraySegmentPtr InputBuffer;

        private ZSTD_Buffer InputBufferState;

        public DecompressorStream(Stream stream) : this(stream, null)
        {
            
        }

        public DecompressorStream(Stream stream, DecompressionOptions options)
        {
            InnerStream = stream;
            Options = options;
            DStream = ZSTD_createDStream();
            if (options == null || options.Ddict == IntPtr.Zero)
                ZSTD_initDStream(DStream);
            else
                ZSTD_initDStream_usingDDict(DStream, options.Ddict);

            InputBuffer = CreateInputBuffer();
            InitializeInputBufferState();
        }

        private static ArraySegmentPtr CreateInputBuffer()
        {
            var bufferSize = (int)ZSTD_DStreamInSize().EnsureZstdSuccess();
            var buffer = new byte[bufferSize];
            return new ArraySegmentPtr(buffer, 0, buffer.Length);
        }

        private void InitializeInputBufferState()
        {
            InputBufferState = new ZSTD_Buffer(InputBuffer);
            InputBufferState.pos = InputBufferState.size;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            using (var outputBufferPtr = new ArraySegmentPtr(buffer, offset, count))
            {
                var outputBufferState = new ZSTD_Buffer(outputBufferPtr);
                while (!outputBufferState.IsFullyConsumed)
                {
                    if (InputBufferState.IsFullyConsumed && !TryRefreshInputBuffer())
                        break;

                    ZSTD_decompressStream(DStream, ref outputBufferState, ref InputBufferState).EnsureZstdSuccess();
                }

                return outputBufferState.IntPos - offset;//return change in output position as number of read bytes
            }
        }

        private bool TryRefreshInputBuffer()
        {
            int bytesRead = InnerStream.Read(InputBuffer.Array, 0, InputBuffer.Length);

            InputBufferState.pos = UIntPtr.Zero;
            InputBufferState.size = (UIntPtr)bytesRead;

            return bytesRead > 0;
        }

        ~DecompressorStream()
        {
            Close();
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() { }
        
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            ZSTD_freeDStream(DStream);
            InputBuffer.Dispose();
        }
    }
}
