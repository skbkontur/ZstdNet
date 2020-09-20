using System;
using System.IO;
using static ZstdNet.ExternMethods;

namespace ZstdNet
{
	public class CompressionStream : Stream
	{
		private readonly Stream innerStream;

		private ArraySegmentPtr outputBuffer;
		private IntPtr cStream;

		private ZSTD_Buffer outputBufferState;

		public CompressionStream(Stream stream)
			: this(stream, CompressionOptions.Default)
		{}

		public CompressionStream(Stream stream, int bufferSize)
			: this(stream, CompressionOptions.Default, bufferSize)
		{}

		public CompressionStream(Stream stream, CompressionOptions options, int bufferSize = 0)
		{
			if(bufferSize < 0)
				throw new ArgumentOutOfRangeException(nameof(bufferSize));

			innerStream = stream;

			cStream = ZSTD_createCStream();
			if(options.Cdict == IntPtr.Zero)
				ZSTD_initCStream(cStream, options.CompressionLevel).EnsureZstdSuccess();
			else
				ZSTD_initCStream_usingCDict(cStream, options.Cdict).EnsureZstdSuccess();

			outputBuffer = CreateOutputBuffer(bufferSize);
			outputBufferState = new ZSTD_Buffer(outputBuffer);
		}

		private static ArraySegmentPtr CreateOutputBuffer(int bufferSize)
		{
			var outputArray = new byte[bufferSize > 0 ? bufferSize : (int)ZSTD_CStreamOutSize().EnsureZstdSuccess()];
			return new ArraySegmentPtr(outputArray, 0, outputArray.Length);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			if(offset < 0)
				throw new ArgumentOutOfRangeException(nameof(offset));
			if(count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if(offset + count > buffer.Length)
				throw new ArgumentException("The sum of offset and count is greater than the buffer length");

			if(count == 0)
				return;

			using(var inputBuffer = new ArraySegmentPtr(buffer, offset, count))
			{
				var inputBufferState = new ZSTD_Buffer(inputBuffer);
				while(!inputBufferState.IsFullyConsumed)
				{
					if(outputBufferState.IsFullyConsumed)
						FlushOutputBuffer();

					ZSTD_compressStream(cStream, ref outputBufferState, ref inputBufferState).EnsureZstdSuccess();
				}
			}
		}

		private void FlushOutputBuffer()
		{
			if(outputBufferState.pos == UIntPtr.Zero)
				return;

			innerStream.Write(outputBuffer.Array, 0, (int)outputBufferState.pos);
			outputBufferState.pos = UIntPtr.Zero;
		}

		~CompressionStream() => Dispose(false);

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
			do
			{
				if(outputBufferState.IsFullyConsumed)
					FlushOutputBuffer();
			} while(ZSTD_flushStream(cStream, ref outputBufferState).EnsureZstdSuccess() != UIntPtr.Zero);

			FlushOutputBuffer();
			innerStream.Flush();
		}

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		protected override void Dispose(bool disposing)
		{
			if(cStream == IntPtr.Zero)
				return;

			try
			{
				if(!disposing)
					return;

				do
				{
					if(outputBufferState.IsFullyConsumed)
						FlushOutputBuffer();
				} while(ZSTD_endStream(cStream, ref outputBufferState).EnsureZstdSuccess() != UIntPtr.Zero);

				FlushOutputBuffer();
			}
			finally
			{
				ZSTD_freeCStream(cStream);
				outputBuffer.Dispose();

				cStream = IntPtr.Zero;
			}
		}
	}
}
