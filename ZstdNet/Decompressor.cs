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
		{
			var expectedDstSize = GetDecompressedSize(src);
			if(expectedDstSize > (ulong)maxDecompressedSize)
				throw new ArgumentOutOfRangeException($"Decompressed size is too big ({expectedDstSize} bytes > authorized {maxDecompressedSize} bytes)");
			var dst = new byte[expectedDstSize];

			int dstSize;
			try
			{
				dstSize = Unwrap(src, dst, 0, false);
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
			=> GetDecompressedSize(new ArraySegment<byte>(src));

		public static ulong GetDecompressedSize(ArraySegment<byte> src)
		{
			using(var srcPtr = new ArraySegmentPtr(src))
				return GetDecompressedSize(srcPtr, src.Count);
		}

		private static ulong GetDecompressedSize(ArraySegmentPtr srcPtr, int count)
		{
			var size = ExternMethods.ZSTD_getFrameContentSize(srcPtr, (size_t)count);
			if(size == ExternMethods.ZSTD_CONTENTSIZE_UNKNOWN)
				throw new ZstdException("Decompressed size cannot be determined");
			if(size == ExternMethods.ZSTD_CONTENTSIZE_ERROR)
				throw new ZstdException("Decompressed size determining error (e.g. invalid magic number, srcSize too small)");
			return size;
		}

		public int Unwrap(byte[] src, byte[] dst, int offset, bool bufferSizePrecheck = true)
			=> Unwrap(new ArraySegment<byte>(src), dst, offset, bufferSizePrecheck);

		public int Unwrap(ArraySegment<byte> src, byte[] dst, int offset, bool bufferSizePrecheck = true)
		{
			if(offset < 0 || offset > dst.Length)
				throw new ArgumentOutOfRangeException(nameof(offset));

			var dstCapacity = dst.Length - offset;
			using(var srcPtr = new ArraySegmentPtr(src))
			{
				if(bufferSizePrecheck)
				{
					var expectedDstSize = GetDecompressedSize(srcPtr, src.Count);
					if((int)expectedDstSize > dstCapacity)
						throw new InsufficientMemoryException("Buffer size is less than specified decompressed data size");
				}

				size_t dstSize;
				using(var dstPtr = new ArraySegmentPtr(new ArraySegment<byte>(dst, offset, dstCapacity)))
				{
					if(Options.Ddict == IntPtr.Zero)
						dstSize = ExternMethods.ZSTD_decompressDCtx(dctx, dstPtr, (size_t) dstCapacity, srcPtr, (size_t) src.Count);
					else
						dstSize = ExternMethods.ZSTD_decompress_usingDDict(dctx, dstPtr, (size_t) dstCapacity, srcPtr, (size_t) src.Count, Options.Ddict);
				}

				dstSize.EnsureZstdSuccess();
				return (int)dstSize;
			}
		}

		public readonly DecompressionOptions Options;

		private IntPtr dctx;
	}
}
