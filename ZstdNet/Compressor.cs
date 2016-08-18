using System;
using size_t = System.UInt64;

namespace ZstdNet
{
	public class Compressor : IDisposable
	{
		public Compressor(byte[] dict = null, int compressionLevel = DefaultCompressionLevel)
		{
			this.compressionLevel = compressionLevel;

			cctx = ExternMethods.ZSTD_createCCtx().EnsureSuccess();
			if (dict != null)
				cdict = ExternMethods.ZSTD_createCDict(dict, (size_t)dict.Length, compressionLevel).EnsureSuccess();
		}

		public const int DefaultCompressionLevel = 3; // Used by zstd utility by default

		~Compressor()
		{
			Dispose();
		}

		public void Dispose()
		{
			if (disposed)
				return;
			disposed = true;

			if (cdict != IntPtr.Zero)
				ExternMethods.ZSTD_freeCDict(cdict);
			ExternMethods.ZSTD_freeCCtx(cctx);
		}

		private bool disposed = false;

		public byte[] Wrap(byte[] data)
		{
			return Wrap(new ArraySegment<byte>(data));
		}

		public byte[] Wrap(ArraySegment<byte> data)
		{
			var dstCapacity = ExternMethods.ZSTD_compressBound((size_t)data.Count);
			var dst = new byte[dstCapacity];

			size_t dstSize;
			using (var src = new ArraySegmentPtr(data))
			{
				if (cdict == IntPtr.Zero)
					dstSize = ExternMethods.ZSTD_compressCCtx(cctx, dst, dstCapacity, src, (size_t)data.Count, compressionLevel);
				else
					dstSize = ExternMethods.ZSTD_compress_usingCDict(cctx, dst, dstCapacity, src, (size_t)data.Count, cdict);
			}
			dstSize.EnsureSuccess();

			var result = new byte[dstSize];
			Array.Copy(dst, result, (int) dstSize);
			return result;
		}

		private readonly IntPtr cctx, cdict;
		private readonly int compressionLevel;
	}
}
