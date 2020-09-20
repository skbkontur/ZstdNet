using System;
using size_t = System.UIntPtr;

namespace ZstdNet
{
	public class Decompressor : IDisposable
	{
		public Decompressor()
			: this(new DecompressionOptions(null))
		{}

		public Decompressor(DecompressionOptions options)
		{
			Options = options;
			dctx = ExternMethods.ZSTD_createDCtx().EnsureZstdSuccess();
		}

		~Decompressor() => Dispose(false);

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if(dctx == IntPtr.Zero)
				return;

			ExternMethods.ZSTD_freeDCtx(dctx);

			dctx = IntPtr.Zero;
		}

		public byte[] Unwrap(byte[] src, int maxDecompressedSize = int.MaxValue)
			=> Unwrap(new ArraySegment<byte>(src), maxDecompressedSize);

		public byte[] Unwrap(ArraySegment<byte> src, int maxDecompressedSize = int.MaxValue)
			=> Unwrap((ReadOnlySpan<byte>)src, maxDecompressedSize);

		public byte[] Unwrap(ReadOnlySpan<byte> src, int maxDecompressedSize = int.MaxValue)
		{
			var expectedDstSize = GetDecompressedSize(src);
			if(expectedDstSize > (ulong)maxDecompressedSize)
				throw new ArgumentOutOfRangeException($"Decompressed size is too big ({expectedDstSize} bytes > authorized {maxDecompressedSize} bytes)");

			var dst = new byte[expectedDstSize];

			int dstSize;
			try
			{
				dstSize = Unwrap(src, new Span<byte>(dst), false);
			}
			catch(InsufficientMemoryException)
			{
				throw new ZstdException("Invalid decompressed size");
			}

			if((int)expectedDstSize != dstSize)
				throw new ZstdException("Invalid decompressed size specified in the data");

			return dst;
		}

		public static ulong GetDecompressedSize(byte[] src)
			=> GetDecompressedSize(new ReadOnlySpan<byte>(src));

		public static ulong GetDecompressedSize(ArraySegment<byte> src)
			=> GetDecompressedSize((ReadOnlySpan<byte>)src);

		public static ulong GetDecompressedSize(ReadOnlySpan<byte> src)
		{
			var size = ExternMethods.ZSTD_getFrameContentSize(src, (size_t)src.Length);
			if(size == ExternMethods.ZSTD_CONTENTSIZE_UNKNOWN)
				throw new ZstdException("Decompressed size cannot be determined");
			if(size == ExternMethods.ZSTD_CONTENTSIZE_ERROR)
				throw new ZstdException("Decompressed size determining error (e.g. invalid magic number, srcSize too small)");
			return size;
		}

		public int Unwrap(byte[] src, byte[] dst, int offset, bool bufferSizePrecheck = true)
			=> Unwrap(new ReadOnlySpan<byte>(src), dst, offset, bufferSizePrecheck);

		public int Unwrap(ArraySegment<byte> src, byte[] dst, int offset, bool bufferSizePrecheck = true)
			=> Unwrap((ReadOnlySpan<byte>)src, dst, offset, bufferSizePrecheck);

		public int Unwrap(ReadOnlySpan<byte> src, byte[] dst, int offset, bool bufferSizePrecheck = true)
		{
			if(offset < 0 || offset > dst.Length)
				throw new ArgumentOutOfRangeException(nameof(offset));

			return Unwrap(src, new Span<byte>(dst, offset, dst.Length - offset), bufferSizePrecheck);
		}

		public int Unwrap(ReadOnlySpan<byte> src, Span<byte> dst, bool bufferSizePrecheck = true)
		{
			if(bufferSizePrecheck)
			{
				var expectedDstSize = GetDecompressedSize(src);
				if((int)expectedDstSize > dst.Length)
					throw new InsufficientMemoryException("Buffer size is less than specified decompressed data size");
			}

			var dstSize = Options.Ddict == IntPtr.Zero
				? ExternMethods.ZSTD_decompressDCtx(dctx, dst, (size_t)dst.Length, src, (size_t)src.Length)
				: ExternMethods.ZSTD_decompress_usingDDict(dctx, dst, (size_t)dst.Length, src, (size_t)src.Length, Options.Ddict);

			return (int)dstSize.EnsureZstdSuccess();
		}

		public readonly DecompressionOptions Options;

		private IntPtr dctx;
	}
}
