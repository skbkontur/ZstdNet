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

			options.ApplyDecompressionParams(dctx);
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
				throw new ZstdException(ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall, $"Decompressed content size {expectedDstSize} is greater than {nameof(maxDecompressedSize)} {maxDecompressedSize}");
			if(expectedDstSize > Consts.MaxByteArrayLength)
				throw new ZstdException(ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall, $"Decompressed content size {expectedDstSize} is greater than max possible byte array size {Consts.MaxByteArrayLength}");

			var dst = new byte[expectedDstSize];

			var dstSize = Unwrap(src, new Span<byte>(dst), false);
			if(expectedDstSize != (ulong)dstSize)
				throw new ZstdException(ZSTD_ErrorCode.ZSTD_error_GENERIC, "Decompressed content size specified in the src data frame is invalid");

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
				throw new ZstdException(ZSTD_ErrorCode.ZSTD_error_GENERIC, "Decompressed content size is not specified");
			if(size == ExternMethods.ZSTD_CONTENTSIZE_ERROR)
				throw new ZstdException(ZSTD_ErrorCode.ZSTD_error_GENERIC, "Decompressed content size cannot be determined (e.g. invalid magic number, srcSize too small)");
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
				if(expectedDstSize > (ulong)dst.Length)
					throw new ZstdException(ZSTD_ErrorCode.ZSTD_error_dstSize_tooSmall, "Destination buffer size is less than specified decompressed content size");
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
