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

		public byte[] Unwrap(byte[] data, size_t maxDecompressedSize = size_t.MaxValue)
		{
			return Unwrap(new ArraySegment<byte>(data), maxDecompressedSize);
		}

		public byte[] Unwrap(ArraySegment<byte> data, size_t maxDecompressedSize = size_t.MaxValue)
		{
			/* NOTES about ZSTD_getDecompressedSize():

			- Decompressed size can be very large (64-bits value),
			potentially larger than what local system can handle as a single memory segment.
			In which case, it's necessary to use streaming mode to decompress data.

			- Decompressed size could be wrong or intentionally modified!
			Always ensure result fits within application's authorized limits! */

			if(data.Count == 0)
				return new byte[0];

			ulong dstCapacity;
			size_t dstSize;
			byte[] dst;
			using(var src = new ArraySegmentPtr(data))
			{
				dstCapacity = ExternMethods.ZSTD_getDecompressedSize(src, (size_t) data.Count);
				if(dstCapacity == 0)
					throw new ZstdException("Can't decompress data with unspecified decompressed size (streaming mode is not implemented)");
				if(dstCapacity > maxDecompressedSize)
					throw new ArgumentOutOfRangeException(string.Format("Decompressed size is too big ({0} bytes > authorized {1} bytes)", dstCapacity, maxDecompressedSize));
				dst = new byte[dstCapacity];

				if (ddict == IntPtr.Zero)
					dstSize = ExternMethods.ZSTD_decompressDCtx(dctx, dst, (size_t)dstCapacity, src, (size_t) data.Count);
				else
					dstSize = ExternMethods.ZSTD_decompress_usingDDict(dctx, dst, (size_t)dstCapacity, src, (size_t) data.Count, ddict);
			}
			dstSize.EnsureZstdSuccess();

			if (dstSize != dstCapacity)
				throw new ZstdException("Invalid decompressed size specified in the data");
			return dst;
		}

		private readonly IntPtr dctx, ddict;
	}
}
