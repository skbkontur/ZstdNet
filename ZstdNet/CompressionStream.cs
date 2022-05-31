using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static ZstdNet.ExternMethods;

namespace ZstdNet
{
	public class CompressionStream : Stream
	{
		private readonly Stream innerStream;
		private readonly byte[] outputBuffer;
		private readonly int bufferSize;
#if !(NET45 || NETSTANDARD2_0)
		private readonly ReadOnlyMemory<byte> outputMemory;
#endif

		private IntPtr cStream;
		private UIntPtr pos;

		public readonly CompressionOptions Options;

		public CompressionStream(Stream stream)
			: this(stream, CompressionOptions.Default)
		{}

		public CompressionStream(Stream stream, int bufferSize)
			: this(stream, CompressionOptions.Default, bufferSize)
		{}

		public CompressionStream(Stream stream, CompressionOptions options, int bufferSize = 0)
		{
			if(stream == null)
				throw new ArgumentNullException(nameof(stream));
			if(!stream.CanWrite)
				throw new ArgumentException("Stream is not writable", nameof(stream));
			if(bufferSize < 0)
				throw new ArgumentOutOfRangeException(nameof(bufferSize));

			innerStream = stream;

			cStream = ZSTD_createCStream().EnsureZstdSuccess();
			ZSTD_CCtx_reset(cStream, ZSTD_ResetDirective.ZSTD_reset_session_only).EnsureZstdSuccess();

			Options = options;
			if(options != null)
			{
				options.ApplyCompressionParams(cStream);

				if(options.Cdict != IntPtr.Zero)
					ZSTD_CCtx_refCDict(cStream, options.Cdict).EnsureZstdSuccess();
			}

			this.bufferSize = bufferSize > 0 ? bufferSize : (int)ZSTD_CStreamOutSize().EnsureZstdSuccess();
			outputBuffer = ArrayPool<byte>.Shared.Rent(this.bufferSize);
#if !(NET45 || NETSTANDARD2_0)
			outputMemory = new ReadOnlyMemory<byte>(outputBuffer, 0, this.bufferSize);
#endif
		}

#if !(NET45 || NETSTANDARD2_0)
		public override void Write(ReadOnlySpan<byte> buffer)
		{
			EnsureNotDisposed();

			WriteInternal(buffer);
		}

		public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
		{
			EnsureNotDisposed();

			return WriteInternalAsync(buffer, cancellationToken);
		}
#endif

		public override void Write(byte[] buffer, int offset, int count)
		{
			EnsureParamsValid(buffer, offset, count);
			EnsureNotDisposed();

			WriteInternal(new Span<byte>(buffer, offset, count));
		}

		public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			EnsureParamsValid(buffer, offset, count);
			EnsureNotDisposed();

#if !(NET45 || NETSTANDARD2_0)
			return WriteInternalAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken).AsTask();
#else
			return WriteInternalAsync(new ReadOnlyMemory<byte>(buffer, offset, count), cancellationToken);
#endif
		}

		private void WriteInternal(ReadOnlySpan<byte> buffer)
		{
			if(buffer.Length == 0)
				return;

			var input = new ZSTD_Buffer(UIntPtr.Zero, (UIntPtr)buffer.Length);
			var output = new ZSTD_Buffer(pos, (UIntPtr)bufferSize);

			var outputSpan = new ReadOnlySpan<byte>(outputBuffer, 0, bufferSize);

			do
			{
				if(output.IsFullyConsumed)
				{
					FlushOutputBuffer(outputSpan.Slice(0, (int)output.pos));
					output.pos = UIntPtr.Zero;
				}

				Compress(buffer, ref output, ref input, ZSTD_EndDirective.ZSTD_e_continue);
			} while(!input.IsFullyConsumed);

			pos = output.pos;
		}

		private async
#if !(NET45 || NETSTANDARD2_0)
			ValueTask
#else
			Task
#endif
			WriteInternalAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
		{
			if(buffer.Length == 0)
				return;

			var input = new ZSTD_Buffer(UIntPtr.Zero, (UIntPtr)buffer.Length);
			var output = new ZSTD_Buffer(pos, (UIntPtr)bufferSize);

			do
			{
				if(output.IsFullyConsumed)
				{
					await FlushOutputBufferAsync(ref output, cancellationToken).ConfigureAwait(false);
					output.pos = UIntPtr.Zero;
				}

				Compress(buffer.Span, ref output, ref input, ZSTD_EndDirective.ZSTD_e_continue);
			} while(!input.IsFullyConsumed);

			pos = output.pos;
		}

		private unsafe UIntPtr Compress(ReadOnlySpan<byte> buffer, ref ZSTD_Buffer output, ref ZSTD_Buffer input, ZSTD_EndDirective directive)
		{
			fixed(void* inputHandle = &MemoryMarshal.GetReference(buffer))
			fixed(void* outputHandle = &outputBuffer[0])
			{
				input.buffer = new IntPtr(inputHandle);
				output.buffer = new IntPtr(outputHandle);

				return ZSTD_compressStream2(cStream, ref output, ref input, directive).EnsureZstdSuccess();
			}
		}

#if !(NET45 || NETSTANDARD2_0)
		private void FlushOutputBuffer(ReadOnlySpan<byte> outputSpan)
			=> innerStream.Write(outputSpan);
		private ValueTask FlushOutputBufferAsync(ref ZSTD_Buffer output, CancellationToken cancellationToken)
			=> innerStream.WriteAsync(outputMemory.Slice(0, (int)output.pos), cancellationToken);
#else
		private void FlushOutputBuffer(ReadOnlySpan<byte> outputSpan)
			=> innerStream.Write(outputBuffer, 0, outputSpan.Length);
		private Task FlushOutputBufferAsync(ref ZSTD_Buffer output, CancellationToken cancellationToken)
			=> innerStream.WriteAsync(outputBuffer, 0, (int)output.pos, cancellationToken);
#endif

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
			EnsureNotDisposed();

			FlushCompressStream(ZSTD_EndDirective.ZSTD_e_flush);
		}

		public override Task FlushAsync(CancellationToken cancellationToken)
		{
			EnsureNotDisposed();

#if !(NET45 || NETSTANDARD2_0)
			return FlushCompressStreamAsync(ZSTD_EndDirective.ZSTD_e_flush, cancellationToken).AsTask();
#else
			return FlushCompressStreamAsync(ZSTD_EndDirective.ZSTD_e_flush, cancellationToken);
#endif
		}

		private void FlushCompressStream(ZSTD_EndDirective directive)
		{
			var buffer = ReadOnlySpan<byte>.Empty;

			var input = new ZSTD_Buffer(UIntPtr.Zero, UIntPtr.Zero);
			var output = new ZSTD_Buffer(pos, (UIntPtr)bufferSize);

			var outputSpan = new ReadOnlySpan<byte>(outputBuffer, 0, bufferSize);

			do
			{
				if(output.IsFullyConsumed)
				{
					FlushOutputBuffer(outputSpan.Slice(0, (int)output.pos));
					output.pos = UIntPtr.Zero;
				}
			} while(Compress(buffer, ref output, ref input, directive) != UIntPtr.Zero);

			if(output.pos != UIntPtr.Zero)
				FlushOutputBuffer(outputSpan.Slice(0, (int)output.pos));

			pos = UIntPtr.Zero;
		}

		private async
#if !(NET45 || NETSTANDARD2_0)
			ValueTask
#else
			Task
#endif
			FlushCompressStreamAsync(ZSTD_EndDirective directive, CancellationToken cancellationToken)
		{
			var input = new ZSTD_Buffer(UIntPtr.Zero, UIntPtr.Zero);
			var output = new ZSTD_Buffer(pos, (UIntPtr)bufferSize);

			do
			{
				if(!output.IsFullyConsumed)
					continue;

				await FlushOutputBufferAsync(ref output, cancellationToken).ConfigureAwait(false);
				output.pos = UIntPtr.Zero;
			} while(Compress(ReadOnlySpan<byte>.Empty, ref output, ref input, directive) != UIntPtr.Zero);

			if(output.pos != UIntPtr.Zero)
				await FlushOutputBufferAsync(ref output, cancellationToken).ConfigureAwait(false);

			pos = UIntPtr.Zero;
		}

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

#if !(NET45 || NETSTANDARD2_0)
		public override async ValueTask DisposeAsync()
		{
			await DisposeAsyncCore().ConfigureAwait(false);
			GC.SuppressFinalize(this);
		}

		protected virtual async ValueTask DisposeAsyncCore()
		{
			if(cStream == IntPtr.Zero)
				return;

			try
			{
				await FlushCompressStreamAsync(ZSTD_EndDirective.ZSTD_e_end, CancellationToken.None).ConfigureAwait(false);
			}
			finally
			{
				ZSTD_freeCStream(cStream);

				if(outputBuffer != null)
					ArrayPool<byte>.Shared.Return(outputBuffer);

				cStream = IntPtr.Zero;
			}
		}
#endif

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

				if(outputBuffer != null)
					ArrayPool<byte>.Shared.Return(outputBuffer);

				cStream = IntPtr.Zero;
			}
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
			if(cStream == IntPtr.Zero)
				throw new ObjectDisposedException(nameof(CompressionStream));
		}
	}
}
