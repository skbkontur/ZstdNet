using System;
using System.IO;
using static ZstdNet.ExternMethods;

namespace ZstdNet
{
	public class CompressionStream : Stream
	{
		private readonly Stream innerStream;
		private readonly byte[] outputBuffer;

		private IntPtr cStream;
		private UIntPtr pos;

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

			outputBuffer = new byte[bufferSize > 0 ? bufferSize : (int)ZSTD_CStreamOutSize().EnsureZstdSuccess()];
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

			using(var inputHandle = new ArrayHandle(buffer, offset))
			using(var outputHandle = new ArrayHandle(outputBuffer))
			{
				var input = new ZSTD_Buffer(inputHandle, UIntPtr.Zero, (UIntPtr)count);
				var output = new ZSTD_Buffer(outputHandle, pos, (UIntPtr)outputBuffer.Length);

				while(!input.IsFullyConsumed)
				{
					if(output.IsFullyConsumed)
						FlushOutputBuffer(ref output);

					ZSTD_compressStream2(cStream, ref output, ref input, ZSTD_EndDirective.ZSTD_e_continue).EnsureZstdSuccess();
				}

				pos = output.pos;
			}
		}

		private void FlushOutputBuffer(ref ZSTD_Buffer output)
		{
			if(output.pos == UIntPtr.Zero)
				return;

			innerStream.Write(outputBuffer, 0, (int)output.pos);
			output.pos = UIntPtr.Zero;
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
			FlushCompressStream(ZSTD_EndDirective.ZSTD_e_flush);
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

				FlushCompressStream(ZSTD_EndDirective.ZSTD_e_end);
			}
			finally
			{
				ZSTD_freeCStream(cStream);
				cStream = IntPtr.Zero;
			}
		}

		private void FlushCompressStream(ZSTD_EndDirective directive)
		{
			var input = new ZSTD_Buffer(IntPtr.Zero, UIntPtr.Zero, UIntPtr.Zero);
			using(var outputHandle = new ArrayHandle(outputBuffer))
			{
				var output = new ZSTD_Buffer(outputHandle, pos, (UIntPtr)outputBuffer.Length);
				do
				{
					if(output.IsFullyConsumed)
						FlushOutputBuffer(ref output);
				} while(ZSTD_compressStream2(cStream, ref output, ref input, directive).EnsureZstdSuccess() != UIntPtr.Zero);

				FlushOutputBuffer(ref output);
				pos = output.pos;
			}
		}
	}
}
