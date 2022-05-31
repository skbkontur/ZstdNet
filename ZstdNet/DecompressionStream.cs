using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static ZstdNet.ExternMethods;

namespace ZstdNet
{
	public class DecompressionStream : Stream
	{
		private readonly Stream innerStream;
		private readonly byte[] inputBuffer;
		private readonly int bufferSize;
#if !(NET45 || NETSTANDARD2_0)
		private readonly Memory<byte> inputMemory;
#endif

		private IntPtr dStream;
		private UIntPtr pos;
		private UIntPtr size;

		public readonly DecompressionOptions Options;

		public DecompressionStream(Stream stream)
			: this(stream, null)
		{}

		public DecompressionStream(Stream stream, int bufferSize)
			: this(stream, null, bufferSize)
		{}

		public DecompressionStream(Stream stream, DecompressionOptions options, int bufferSize = 0)
		{
			if(stream == null)
				throw new ArgumentNullException(nameof(stream));
			if(!stream.CanRead)
				throw new ArgumentException("Stream is not readable", nameof(stream));
			if(bufferSize < 0)
				throw new ArgumentOutOfRangeException(nameof(bufferSize));

			innerStream = stream;

			dStream = ZSTD_createDStream().EnsureZstdSuccess();
			ZSTD_DCtx_reset(dStream, ZSTD_ResetDirective.ZSTD_reset_session_only).EnsureZstdSuccess();

			Options = options;
			if(options != null)
			{
				options.ApplyDecompressionParams(dStream);

				if(options.Ddict != IntPtr.Zero)
					ZSTD_DCtx_refDDict(dStream, options.Ddict).EnsureZstdSuccess();
			}

			this.bufferSize = bufferSize > 0 ? bufferSize : (int)ZSTD_DStreamInSize().EnsureZstdSuccess();
			inputBuffer = ArrayPool<byte>.Shared.Rent(this.bufferSize);
#if !(NET45 || NETSTANDARD2_0)
			inputMemory = new Memory<byte>(inputBuffer, 0, this.bufferSize);
#endif
			pos = size = (UIntPtr)this.bufferSize;
		}

#if !(NET45 || NETSTANDARD2_0)
		public override int Read(Span<byte> buffer)
		{
			EnsureNotDisposed();

			return ReadInternal(buffer);
		}

		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
		{
			EnsureNotDisposed();

			return ReadInternalAsync(buffer, cancellationToken);
		}
#endif

		public override int Read(byte[] buffer, int offset, int count)
		{
			EnsureParamsValid(buffer, offset, count);
			EnsureNotDisposed();

			return ReadInternal(new Span<byte>(buffer, offset, count));
		}

		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			EnsureParamsValid(buffer, offset, count);
			EnsureNotDisposed();

#if !(NET45 || NETSTANDARD2_0)
			return ReadInternalAsync(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
#else
			return ReadInternalAsync(new Memory<byte>(buffer, offset, count), cancellationToken);
#endif
		}

		private int ReadInternal(Span<byte> buffer)
		{
			var input = new ZSTD_Buffer(pos, size);
			var output = new ZSTD_Buffer(UIntPtr.Zero, (UIntPtr)buffer.Length);

			var inputSpan = new Span<byte>(inputBuffer, 0, bufferSize);

			while(!output.IsFullyConsumed && (!input.IsFullyConsumed || FillInputBuffer(inputSpan, ref input) > 0))
				Decompress(buffer, ref output, ref input);

			pos = input.pos;
			size = input.size;

			return (int)output.pos;
		}

		private async
#if !(NET45 || NETSTANDARD2_0)
			ValueTask<int>
#else
			Task<int>
#endif
			ReadInternalAsync(Memory<byte> buffer, CancellationToken cancellationToken)
		{
			var input = new ZSTD_Buffer(pos, size);
			var output = new ZSTD_Buffer(UIntPtr.Zero, (UIntPtr)buffer.Length);

			while(!output.IsFullyConsumed)
			{
				if(input.IsFullyConsumed)
				{
					int bytesRead;
#if !(NET45 || NETSTANDARD2_0)
					if((bytesRead = await innerStream.ReadAsync(inputMemory, cancellationToken).ConfigureAwait(false)) == 0)
#else
					if((bytesRead = await innerStream.ReadAsync(inputBuffer, 0, bufferSize, cancellationToken).ConfigureAwait(false)) == 0)
#endif
						break;

					input.size = (UIntPtr)bytesRead;
					input.pos = UIntPtr.Zero;
				}

				Decompress(buffer.Span, ref output, ref input);
			}

			pos = input.pos;
			size = input.size;

			return (int)output.pos;
		}

		private unsafe void Decompress(Span<byte> buffer, ref ZSTD_Buffer output, ref ZSTD_Buffer input)
		{
			fixed(void* inputBufferHandle = &inputBuffer[0])
			fixed(void* outputBufferHandle = &MemoryMarshal.GetReference(buffer))
			{
				input.buffer = new IntPtr(inputBufferHandle);
				output.buffer = new IntPtr(outputBufferHandle);

				ZSTD_decompressStream(dStream, ref output, ref input).EnsureZstdSuccess();
			}
		}

		private int FillInputBuffer(Span<byte> inputSpan, ref ZSTD_Buffer input)
		{
#if !(NET45 || NETSTANDARD2_0)
			int bytesRead = innerStream.Read(inputSpan);
#else
			int bytesRead = innerStream.Read(inputBuffer, 0, inputSpan.Length);
#endif

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

		public override void Flush() => throw new NotSupportedException();

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		protected override void Dispose(bool disposing)
		{
			if(dStream == IntPtr.Zero)
				return;

			ZSTD_freeDStream(dStream);

			if(inputBuffer != null)
				ArrayPool<byte>.Shared.Return(inputBuffer);

			dStream = IntPtr.Zero;
		}

		private void EnsureParamsValid(byte[] buffer, int offset, int count)
		{
			if(buffer == null)
				throw new ArgumentNullException(nameof(buffer));
			if(offset < 0)
				throw new ArgumentOutOfRangeException(nameof(offset));
			if(count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));
			if(count > buffer.Length - offset)
				throw new ArgumentException("The sum of offset and count is greater than the buffer length");
		}

		private void EnsureNotDisposed()
		{
			if(dStream == IntPtr.Zero)
				throw new ObjectDisposedException(nameof(DecompressionStream));
		}
	}
}
