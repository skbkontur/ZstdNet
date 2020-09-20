using System;
using System.IO;
using static ZstdNet.ExternMethods;

namespace ZstdNet
{
	public class DecompressionStream : Stream
	{
		private readonly Stream innerStream;

		private ArraySegmentPtr inputBuffer;
		private IntPtr dStream;

		private ZSTD_Buffer inputBufferState;

		public DecompressionStream(Stream stream)
			: this(stream, null)
		{}

		public DecompressionStream(Stream stream, int bufferSize)
			: this(stream, null, bufferSize)
		{}

		public DecompressionStream(Stream stream, DecompressionOptions options, int bufferSize = 0)
		{
			if(bufferSize < 0)
				throw new ArgumentOutOfRangeException(nameof(bufferSize));

			innerStream = stream;

			dStream = ZSTD_createDStream();
			if(options == null || options.Ddict == IntPtr.Zero)
				ZSTD_initDStream(dStream);
			else
				ZSTD_initDStream_usingDDict(dStream, options.Ddict);

			inputBuffer = CreateInputBuffer(bufferSize);
			inputBufferState = new ZSTD_Buffer(inputBuffer);
			inputBufferState.pos = inputBufferState.size;
		}

		private static ArraySegmentPtr CreateInputBuffer(int bufferSize)
		{
			var buffer = new byte[bufferSize > 0 ? bufferSize : (int)ZSTD_DStreamInSize().EnsureZstdSuccess()];
			return new ArraySegmentPtr(buffer, 0, buffer.Length);
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if(offset < 0)
				throw new ArgumentOutOfRangeException(nameof(offset));
			if(count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if(offset + count > buffer.Length)
				throw new ArgumentException("The sum of offset and count is greater than the buffer length");

			if(count == 0)
				return 0;

			using(var outputBufferPtr = new ArraySegmentPtr(buffer, offset, count))
			{
				var outputBufferState = new ZSTD_Buffer(outputBufferPtr);
				while(!outputBufferState.IsFullyConsumed && (!inputBufferState.IsFullyConsumed || FillInputBuffer() > 0))
					ZSTD_decompressStream(dStream, ref outputBufferState, ref inputBufferState).EnsureZstdSuccess();

				return (int)outputBufferState.pos;
			}
		}

		private int FillInputBuffer()
		{
			int bytesRead = innerStream.Read(inputBuffer.Array, 0, inputBuffer.Length);

			inputBufferState.pos = UIntPtr.Zero;
			inputBufferState.size = (UIntPtr)bytesRead;

			return bytesRead;
		}

		~DecompressionStream() => Dispose(false);

		public override bool CanRead => true;
		public override bool CanSeek => false;
		public override bool CanWrite => false;

		public override long Length => throw new NotSupportedException();
		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		public override void Flush() {}

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		protected override void Dispose(bool disposing)
		{
			if(dStream == IntPtr.Zero)
				return;

			ZSTD_freeDStream(dStream);
			inputBuffer.Dispose();

			dStream = IntPtr.Zero;
		}
	}
}
