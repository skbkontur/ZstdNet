using System;
using size_t = System.UInt64;

namespace ZstdNet
{
	public class Decompressor : IDisposable
	{
		public Decompressor(byte[] dict = null)
		{
			dctx = ExternMethods.ZSTD_createDCtx().EnsureSuccess();
			if (dict != null)
				ddict = ExternMethods.ZSTD_createDDict(dict, (size_t)dict.Length).EnsureSuccess();
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

		public byte[] Unwrap(byte[] data)
		{
			return Unwrap(new ArraySegment<byte>(data));
		}

		public byte[] Unwrap(ArraySegment<byte> data)
		{
			// NOTE: Unwrap now can be used only on trusted data,
			// NOTE: because we can't trust ZSTD_getDecompressedSize(), and it can be very big (https://github.com/Cyan4973/zstd/blob/master/lib/zstd.h#L83).

			size_t dstCapacity, dstSize;
			byte[] dst;
			using(var src = new ArraySegmentPtr(data))
			{
				dstCapacity = ExternMethods.ZSTD_getDecompressedSize(src, (size_t) data.Count);
				if(dstCapacity == 0)
					throw new ZstdException("Can't decompress data with unspecified decompressed size (streaming mode is not implemented)");
				dst = new byte[dstCapacity];

				if (ddict == IntPtr.Zero)
					dstSize = ExternMethods.ZSTD_decompressDCtx(dctx, dst, dstCapacity, src, (size_t) data.Count);
				else
					dstSize = ExternMethods.ZSTD_decompress_usingDDict(dctx, dst, dstCapacity, src, (size_t) data.Count, ddict);
			}
			dstSize.EnsureSuccess();

			if (dstSize != dstCapacity)
				throw new ZstdException("Invalid decompressed size specified in the data");
			return dst;
		}

		private readonly IntPtr dctx, ddict;
	}
}
