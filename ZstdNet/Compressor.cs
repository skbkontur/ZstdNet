﻿using System;
using size_t = System.UIntPtr;

namespace ZstdNet
{
	public class Compressor : IDisposable
	{
		public Compressor()
			: this(CompressionOptions.Default)
		{}

		public Compressor(CompressionOptions options)
		{
			Options = options;
			cctx = ExternMethods.ZSTD_createCCtx().EnsureZstdSuccess();
		}

		~Compressor() => Dispose(false);

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if(cctx == IntPtr.Zero)
				return;

			ExternMethods.ZSTD_freeCCtx(cctx);

			cctx = IntPtr.Zero;
		}

		public byte[] Wrap(byte[] src)
			=> Wrap(new ReadOnlySpan<byte>(src));

		public byte[] Wrap(ArraySegment<byte> src)
			=> Wrap((ReadOnlySpan<byte>)src);

		public byte[] Wrap(ReadOnlySpan<byte> src)
		{
			var dstCapacity = GetCompressBound(src.Length);
			var dst = new byte[dstCapacity];

			var dstSize = Wrap(src, new Span<byte>(dst));
			if(dstCapacity == dstSize)
				return dst;

			var result = new byte[dstSize];
			Array.Copy(dst, result, dstSize);
			return result;
		}

		public static int GetCompressBound(int size)
			=> (int)ExternMethods.ZSTD_compressBound((size_t)size);

		public int Wrap(byte[] src, byte[] dst, int offset)
			=> Wrap(new ReadOnlySpan<byte>(src), dst, offset);

		public int Wrap(ArraySegment<byte> src, byte[] dst, int offset)
			=> Wrap((ReadOnlySpan<byte>)src, dst, offset);

		public int Wrap(ReadOnlySpan<byte> src, byte[] dst, int offset)
		{
			if(offset < 0 || offset >= dst.Length)
				throw new ArgumentOutOfRangeException(nameof(offset));

			return Wrap(src, new Span<byte>(dst, offset, dst.Length - offset));
		}

		public int Wrap(ReadOnlySpan<byte> src, Span<byte> dst)
		{
			var dstSize = Options.Cdict == IntPtr.Zero
				? ExternMethods.ZSTD_compressCCtx(cctx, dst, (size_t)dst.Length, src, (size_t)src.Length, Options.CompressionLevel)
				: ExternMethods.ZSTD_compress_usingCDict(cctx, dst, (size_t)dst.Length, src, (size_t)src.Length, Options.Cdict);

			return (int)dstSize.EnsureZstdSuccess();
		}

		public readonly CompressionOptions Options;

		private IntPtr cctx;
	}
}
