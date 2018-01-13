using System;
using System.IO;
using static ZstdNet.ExternMethods;

namespace ZstdNet
{
	public class CompressorStream : Stream
	{
		private readonly IntPtr cStream;
		private readonly Stream innerStream;
		private readonly ArraySegmentPtr outputBuffer;

		private ZSTD_Buffer outputBufferState;

		public CompressorStream(Stream stream)
			: this(stream, CompressionOptions.DefaultCompressionOptions)
		{}

		public CompressorStream(Stream stream, CompressionOptions options)
		{
			innerStream = stream;

			cStream = ZSTD_createCStream();
			if(options.Cdict == IntPtr.Zero)
				ZSTD_initCStream(cStream, options.CompressionLevel).EnsureZstdSuccess();
			else
				ZSTD_initCStream_usingCDict(cStream, options.Cdict).EnsureZstdSuccess();

			outputBuffer = CreateOutputBuffer();
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
			outputBufferState = new ZSTD_Buffer(outputBuffer);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			using(var inBuffer = new ArraySegmentPtr(buffer, offset, count))
			{
				var inputBufferState = new ZSTD_Buffer(inBuffer);
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
			innerStream.Write(outputBuffer.Array, 0, outputBufferState.IntPos);
			outputBufferState.pos = UIntPtr.Zero;
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
			innerStream.Flush();
		}

		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
		public override void SetLength(long value) => throw new NotSupportedException();
		public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		protected override void Dispose(bool disposing)
		{
			try
			{
				var endResult = ZSTD_endStream(cStream, ref outputBufferState).EnsureZstdSuccess();
				if(UIntPtr.Zero != endResult)
				{
					FlushOutputBuffer();

					ZSTD_endStream(cStream, ref outputBufferState).EnsureZstdSuccess();
				}

				FlushOutputBuffer();
			}
			finally
			{
				ZSTD_freeCStream(cStream);
				outputBuffer.Dispose();
			}
		}
	}
}
