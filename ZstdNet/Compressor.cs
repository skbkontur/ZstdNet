using System;
#if BUILD64
using size_t = System.UInt64;
#else
using size_t = System.UInt32;
#endif

namespace ZstdNet
{
	public class Compressor : IDisposable
	{
		public Compressor(byte[] dict = null, int compressionLevel = DefaultCompressionLevel)
		{
			this.compressionLevel = compressionLevel;

			cctx = ExternMethods.ZSTD_createCCtx().EnsureZstdSuccess();
			if (dict != null)
				cdict = ExternMethods.ZSTD_createCDict(dict, (size_t)dict.Length, compressionLevel).EnsureZstdSuccess();
		}

		public static int MaxCompressionLevel
		{
			get { return ExternMethods.ZSTD_maxCLevel(); }
		}

		public const int DefaultCompressionLevel = 3; // Used by zstd utility by default

		~Compressor()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
		}

		private void Dispose(bool disposing)
		{
			if (disposed)
				return;
			disposed = true;

			if (cdict != IntPtr.Zero)
				ExternMethods.ZSTD_freeCDict(cdict);
			ExternMethods.ZSTD_freeCCtx(cctx);

			if (disposing)
				GC.SuppressFinalize(this);
		}

		private bool disposed = false;

		public byte[] Wrap(byte[] src)
		{
			return Wrap(new ArraySegment<byte>(src));
		}

		public byte[] Wrap(ArraySegment<byte> src)
		{
			if (src.Count == 0)
				return new byte[0];

			var dstCapacity = ExternMethods.ZSTD_compressBound((size_t)src.Count);
			var dst = new byte[dstCapacity];

			var dstSize = Wrap(src, dst, 0);

			if((int)dstCapacity == dstSize)
				return dst;
			var result = new byte[dstSize];
			Array.Copy(dst, result, dstSize);
			return result;
		}

		public int Wrap(byte[] src, byte[] dst, int offset)
		{
			return Wrap(new ArraySegment<byte>(src), dst, offset);
		}

		public int Wrap(ArraySegment<byte> src, byte[] dst, int offset)
		{
			if (offset < 0 || offset >= dst.Length)
				throw new ArgumentOutOfRangeException(nameof(offset));

			if (src.Count == 0)
				return 0;

			var dstCapacity = dst.Length - offset;
			size_t dstSize;
			using(var srcPtr = new ArraySegmentPtr(src))
			using(var dstPtr = new ArraySegmentPtr(new ArraySegment<byte>(dst, offset, dstCapacity)))
			{
				if(cdict == IntPtr.Zero)
					dstSize = ExternMethods.ZSTD_compressCCtx(cctx, dstPtr, (size_t)dstCapacity, srcPtr, (size_t)src.Count, compressionLevel);
				else
					dstSize = ExternMethods.ZSTD_compress_usingCDict(cctx, dstPtr, (size_t)dstCapacity, srcPtr, (size_t)src.Count, cdict);
			}
			dstSize.EnsureZstdSuccess();
			return (int)dstSize;
		}

		private readonly IntPtr cctx, cdict;
		private readonly int compressionLevel;
	}
}
