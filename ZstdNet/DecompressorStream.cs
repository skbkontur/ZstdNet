using System;
using System.IO;
using static ZstdNet.ExternMethods;

namespace ZstdNet
{
	public class DecompressorStream : Stream
	{
		private readonly IntPtr dStream;
		private readonly Stream innerStream;
		private readonly ArraySegmentPtr inputBuffer;

		private ZSTD_Buffer inputBufferState;

		public DecompressorStream(Stream stream)
			: this(stream, null)
		{}

		public DecompressorStream(Stream stream, DecompressionOptions options)
		{
			innerStream = stream;

			dStream = ZSTD_createDStream();
			if(options == null || options.Ddict == IntPtr.Zero)
				ZSTD_initDStream(dStream);
			else
				ZSTD_initDStream_usingDDict(dStream, options.Ddict);

			inputBuffer = CreateInputBuffer();
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
			inputBufferState = new ZSTD_Buffer(inputBuffer);
			inputBufferState.pos = inputBufferState.size;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			using(var outputBufferPtr = new ArraySegmentPtr(buffer, offset, count))
			{
				var outputBufferState = new ZSTD_Buffer(outputBufferPtr);
				while(!outputBufferState.IsFullyConsumed)
				{
					if(inputBufferState.IsFullyConsumed && !TryRefreshInputBuffer())
						break;

					ZSTD_decompressStream(dStream, ref outputBufferState, ref inputBufferState).EnsureZstdSuccess();
				}

				return outputBufferState.IntPos - offset; //return change in output position as number of read bytes
			}
		}

		private bool TryRefreshInputBuffer()
		{
			int bytesRead = innerStream.Read(inputBuffer.Array, 0, inputBuffer.Length);

			inputBufferState.pos = UIntPtr.Zero;
			inputBufferState.size = (UIntPtr)bytesRead;

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
			ZSTD_freeDStream(dStream);
			inputBuffer.Dispose();
		}
	}
}
