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
			Dictionary = dict;

			dctx = ExternMethods.ZSTD_createDCtx().EnsureZstdSuccess();
			if (dict != null)
				ddict = ExternMethods.ZSTD_createDDict(dict, (size_t)dict.Length).EnsureZstdSuccess();
		}

		~Decompressor()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
		}

		private void Dispose(bool disposing)
		{
			if(disposed)
				return;
			disposed = true;

			if (ddict != IntPtr.Zero)
				ExternMethods.ZSTD_freeDDict(ddict);
			ExternMethods.ZSTD_freeDCtx(dctx);

			if(disposing)
				GC.SuppressFinalize(this);
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

			ulong expectedDstSize;
			using(var srcPtr = new ArraySegmentPtr(src))
				expectedDstSize = ExternMethods.ZSTD_getDecompressedSize(srcPtr, (size_t)src.Count);
			if(expectedDstSize == 0)
				throw new ZstdException("Can't create buffer for data with unspecified decompressed size (provide your own buffer to Unwrap instead)");
			if(expectedDstSize > maxDecompressedSize)
				throw new ArgumentOutOfRangeException(string.Format("Decompressed size is too big ({0} bytes > authorized {1} bytes)", expectedDstSize, maxDecompressedSize));
			var dst = new byte[expectedDstSize];

			int dstSize;
			try
			{
				dstSize = Unwrap(src, dst, 0, false);
			}
			catch (InsufficientMemoryException)
			{
				throw new ZstdException("Invalid decompressed size");
			}

			if ((int)expectedDstSize != dstSize)
				throw new ZstdException("Invalid decompressed size specified in the data");
			return dst;
		}

		public int Unwrap(byte[] src, byte[] dst, int offset)
		{
			return Unwrap(new ArraySegment<byte>(src), dst, offset);
		}

		public int Unwrap(ArraySegment<byte> src, byte[] dst, int offset, bool bufferSizePrecheck = true)
		{
			if (offset < 0 || offset >= dst.Length)
				throw new ArgumentOutOfRangeException(nameof(offset));

			if (src.Count == 0)
				return 0;

			var dstCapacity = dst.Length - offset;
			using (var srcPtr = new ArraySegmentPtr(src))
			{
				if (bufferSizePrecheck)
				{
					var expectedDstSize = ExternMethods.ZSTD_getDecompressedSize(srcPtr, (size_t) src.Count);
					if ((int)expectedDstSize > dstCapacity)
						throw new InsufficientMemoryException("Buffer size is less than specified decompressed data size");
				}

				size_t dstSize;
				using (var dstPtr = new ArraySegmentPtr(new ArraySegment<byte>(dst, offset, dstCapacity)))
				{
					if (ddict == IntPtr.Zero)
						dstSize = ExternMethods.ZSTD_decompressDCtx(dctx, dstPtr, (size_t) dstCapacity, srcPtr, (size_t) src.Count);
					else
						dstSize = ExternMethods.ZSTD_decompress_usingDDict(dctx, dstPtr, (size_t) dstCapacity, srcPtr, (size_t) src.Count,
							ddict);
				}
				dstSize.EnsureZstdSuccess();
				return (int) dstSize;
			}
		}

		public readonly byte[] Dictionary;

		private readonly IntPtr dctx, ddict;
	}
}
