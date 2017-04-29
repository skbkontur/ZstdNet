using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using size_t = System.UIntPtr;

namespace ZstdNet
{
	internal static class ExternMethods
	{
		static ExternMethods()
		{
			var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
			if (assemblyPath == null)
				throw new InvalidOperationException("Failed to get assembly directory");
			var platform = Environment.Is64BitProcess ? "x64" : "x86";

			var ok = SetDllDirectory(Path.Combine(assemblyPath, platform));
			if (!ok)
				throw new InvalidOperationException("Failed to set DLL directory");
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern bool SetDllDirectory(string path);

		private const string DllName = "libzstd.dll";

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern size_t ZDICT_trainFromBuffer(byte[] dictBuffer, size_t dictBufferCapacity, byte[] samplesBuffer, size_t[] samplesSizes, uint nbSamples);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern uint ZDICT_isError(size_t code);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr ZDICT_getErrorName(size_t code);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr ZSTD_createCCtx();
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern size_t ZSTD_freeCCtx(IntPtr cctx);
		
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr ZSTD_createDCtx();
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern size_t ZSTD_freeDCtx(IntPtr cctx);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern size_t ZSTD_compressCCtx(IntPtr ctx, IntPtr dst, size_t dstCapacity, IntPtr src, size_t srcSize, int compressionLevel);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern size_t ZSTD_decompressDCtx(IntPtr ctx, IntPtr dst, size_t dstCapacity, IntPtr src, size_t srcSize);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr ZSTD_createCDict(byte[] dict, size_t dictSize, int compressionLevel);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern size_t ZSTD_freeCDict(IntPtr cdict);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern size_t ZSTD_compress_usingCDict(IntPtr cctx, IntPtr dst, size_t dstCapacity, IntPtr src, size_t srcSize, IntPtr cdict);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr ZSTD_createDDict(byte[] dict, size_t dictSize);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern size_t ZSTD_freeDDict(IntPtr ddict);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern size_t ZSTD_decompress_usingDDict(IntPtr dctx, IntPtr dst, size_t dstCapacity, IntPtr src, size_t srcSize, IntPtr ddict);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern ulong ZSTD_getDecompressedSize(IntPtr src, size_t srcSize);

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern int ZSTD_maxCLevel();
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern size_t ZSTD_compressBound(size_t srcSize);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern uint ZSTD_isError(size_t code);
		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern IntPtr ZSTD_getErrorName(size_t code);
	}
}
