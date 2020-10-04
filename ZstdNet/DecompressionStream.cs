using System;
using System.IO;
using static ZstdNet.ExternMethods;

namespace ZstdNet
{
	public class DecompressionStream : Stream
	{
		private readonly Stream innerStream;
		private readonly byte[] inputBuffer;

		private IntPtr dStream;
		private UIntPtr pos;
		private UIntPtr size;

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

			inputBuffer = new byte[bufferSize > 0 ? bufferSize : (int)ZSTD_DStreamInSize().EnsureZstdSuccess()];
			pos = size = (UIntPtr)inputBuffer.Length;
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

			using(var inputBufferHandle = new ArrayHandle(inputBuffer, 0))
			using(var outputBufferHandle = new ArrayHandle(buffer, offset))
			{
				var input = new ZSTD_Buffer(inputBufferHandle, pos, size);
				var output = new ZSTD_Buffer(outputBufferHandle, UIntPtr.Zero, (UIntPtr)count);

				while(!output.IsFullyConsumed && (!input.IsFullyConsumed || FillInputBuffer(ref input) > 0))
					ZSTD_decompressStream(dStream, ref output, ref input).EnsureZstdSuccess();

				pos = input.pos;
				size = input.size;

				return (int)output.pos;
			}
		}

		private int FillInputBuffer(ref ZSTD_Buffer input)
		{
			int bytesRead = innerStream.Read(inputBuffer, 0, inputBuffer.Length);

			input.size = (UIntPtr)bytesRead;
			input.pos = UIntPtr.Zero;

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
			dStream = IntPtr.Zero;
		}
	}
}
