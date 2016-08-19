using System;
using System.Runtime.InteropServices;
#if BUILD64
using size_t = System.UInt64;
#else
using size_t = System.UInt32;
#endif

namespace ZstdNet
{
	internal static class ExternMethods
	{
#if BUILD64
		private const string DllName = "zstdlib_x64.dll";
#else
		private const string DllName = "zstdlib_x86.dll";
#endif

		[DllImport(DllName)]
		public static extern size_t ZDICT_trainFromBuffer(byte[] dictBuffer, size_t dictBufferCapacity, byte[] samplesBuffer, size_t[] samplesSizes, uint nbSamples);
		[DllImport(DllName)]
		public static extern uint ZDICT_isError(size_t code);
		[DllImport(DllName)]
		public static extern IntPtr ZDICT_getErrorName(size_t code);

		[DllImport(DllName)]
		public static extern IntPtr ZSTD_createCCtx();
		[DllImport(DllName)]
		public static extern size_t ZSTD_freeCCtx(IntPtr cctx);
		
		[DllImport(DllName)]
		public static extern IntPtr ZSTD_createDCtx();
		[DllImport(DllName)]
		public static extern size_t ZSTD_freeDCtx(IntPtr cctx);

		[DllImport(DllName)]
		public static extern size_t ZSTD_compressCCtx(IntPtr ctx, IntPtr dst, size_t dstCapacity, IntPtr src, size_t srcSize, int compressionLevel);
		[DllImport(DllName)]
		public static extern size_t ZSTD_decompressDCtx(IntPtr ctx, IntPtr dst, size_t dstCapacity, IntPtr src, size_t srcSize);

		[DllImport(DllName)]
		public static extern IntPtr ZSTD_createCDict(byte[] dict, size_t dictSize, int compressionLevel);
		[DllImport(DllName)]
		public static extern size_t ZSTD_freeCDict(IntPtr cdict);
		[DllImport(DllName)]
		public static extern size_t ZSTD_compress_usingCDict(IntPtr cctx, IntPtr dst, size_t dstCapacity, IntPtr src, size_t srcSize, IntPtr cdict);

		[DllImport(DllName)]
		public static extern IntPtr ZSTD_createDDict(byte[] dict, size_t dictSize);
		[DllImport(DllName)]
		public static extern size_t ZSTD_freeDDict(IntPtr ddict);
		[DllImport(DllName)]
		public static extern size_t ZSTD_decompress_usingDDict(IntPtr dctx, IntPtr dst, size_t dstCapacity, IntPtr src, size_t srcSize, IntPtr ddict);

		[DllImport(DllName)]
		public static extern ulong ZSTD_getDecompressedSize(IntPtr src, size_t srcSize);

		[DllImport(DllName)]
		public static extern int ZSTD_maxCLevel();
		[DllImport(DllName)]
		public static extern size_t ZSTD_compressBound(size_t srcSize);
		[DllImport(DllName)]
		public static extern uint ZSTD_isError(size_t code);
		[DllImport(DllName)]
		public static extern IntPtr ZSTD_getErrorName(size_t code);
	}
}
