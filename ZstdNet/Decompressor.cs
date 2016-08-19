using System;
#if BUILD64
using size_t = System.UInt64;
#else
using size_t = System.UInt32;
#endif

namespace ZstdNet
{
	public class Decompressor : IDisposable
	{
		public Decompressor(byte[] dict = null)
		{
			dctx = ExternMethods.ZSTD_createDCtx().EnsureZstdSuccess();
			if (dict != null)
				ddict = ExternMethods.ZSTD_createDDict(dict, (size_t)dict.Length).EnsureZstdSuccess();
		}

		~Decompressor()
		{
			Dispose();
		}

		public void Dispose()
		{
			if(disposed)
				return;
			disposed = true;

			if (ddict != IntPtr.Zero)
				ExternMethods.ZSTD_freeDDict(ddict);
			ExternMethods.ZSTD_freeDCtx(dctx);
		}

		private bool disposed = false;

		public byte[] Unwrap(byte[] src, size_t maxDecompressedSize = size_t.MaxValue)
		{
			return Unwrap(new ArraySegment<byte>(src), maxDecompressedSize);
		}

		public byte[] Unwrap(ArraySegment<byte> src, size_t maxDecompressedSize = size_t.MaxValue)
		{
			if(src.Count == 0)
				return new byte[0];

			ulong dstCapacity;
			using(var srcPtr = new ArraySegmentPtr(src))
				dstCapacity = ExternMethods.ZSTD_getDecompressedSize(srcPtr, (size_t)src.Count);
			if(dstCapacity == 0)
				throw new ZstdException("Can't create buffer for data with unspecified decompressed size (provide your own buffer to Unwrap instead)");
			if(dstCapacity > maxDecompressedSize)
				throw new ArgumentOutOfRangeException(string.Format("Decompressed size is too big ({0} bytes > authorized {1} bytes)", dstCapacity, maxDecompressedSize));
			var dst = new byte[dstCapacity];

			var dstSize = Unwrap(src, dst, 0);

			if ((int)dstCapacity != dstSize)
				throw new ZstdException("Invalid decompressed size specified in the data");
			return dst;
		}

		public int Unwrap(byte[] src, byte[] dst, int offset)
		{
			return Unwrap(new ArraySegment<byte>(src), dst, offset);
		}

		public int Unwrap(ArraySegment<byte> src, byte[] dst, int offset)
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
				if(ddict == IntPtr.Zero)
					dstSize = ExternMethods.ZSTD_decompressDCtx(dctx, dstPtr, (size_t)dstCapacity, srcPtr, (size_t)src.Count);
				else
					dstSize = ExternMethods.ZSTD_decompress_usingDDict(dctx, dstPtr, (size_t)dstCapacity, srcPtr, (size_t)src.Count, ddict);
			}
			dstSize.EnsureZstdSuccess();
			return (int)dstSize;
		}

		private readonly IntPtr dctx, ddict;
	}
}
