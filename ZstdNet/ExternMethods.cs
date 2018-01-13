using System;
using System.Diagnostics;
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
			if(Environment.OSVersion.Platform == PlatformID.Win32NT)
				SetWinDllDirectory();
		}

		private static void SetWinDllDirectory()
		{
			string path;

			var location = Assembly.GetExecutingAssembly().Location;
			if(string.IsNullOrEmpty(location) || (path = Path.GetDirectoryName(location)) == null)
			{
				Trace.TraceWarning($"{nameof(ZstdNet)}: Failed to get executing assembly location");
				return;
			}

			// Nuget package
			if(Path.GetFileName(path).StartsWith("net", StringComparison.Ordinal) && Path.GetFileName(Path.GetDirectoryName(path)) == "lib" && File.Exists(Path.Combine(path, "../../zstdnet.nuspec")))
				path = Path.Combine(path, "../../build");

			var platform = Environment.Is64BitProcess ? "x64" : "x86";
			if(!SetDllDirectory(Path.Combine(path, platform)))
				Trace.TraceWarning($"{nameof(ZstdNet)}: Failed to set DLL directory to '{path}'");
		}

		[DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern bool SetDllDirectory(string path);

		private const string DllName = "libzstd";

		[DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
		public static extern size_t ZDICT_trainFromBuffer(byte[] dictBuffer, size_t dictBufferCapacity, byte[] samplesBuffer, size_t[] samplesSizes, uint nbSamples);
        internal static object ZSTD_flushStream(IntPtr zSTD_CStream, ref object outBuffer) => throw new NotImplementedException();
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



        #region Streaming APIs
        //Compression
        //ZSTD_CStream* ZSTD_createCStream(void);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr ZSTD_createCStream();


        //size_t ZSTD_freeCStream(ZSTD_CStream* zcs);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_freeCStream(IntPtr zcs);

        //size_t ZSTD_initCStream(ZSTD_CStream* zcs, int compressionLevel);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_initCStream(IntPtr zcs, int compressionLevel);



        /// <summary>
        /// Use repetitively to consume input stream. 
        /// The function will automatically update both pos fields. 
        /// Note that it may not consume the entire input, in which case pos &lt; size, and it's up to the caller to present again remaining data.
        /// </summary>
        /// <param name="zcs"></param>
        /// <param name="output"></param>
        /// <param name="input"></param>
        /// <returns>
        /// A size hint, preferred nb of bytes to use as input for next function call or an error code, which can be tested using ZSTD_isError(). 
        /// 
        /// Note 1 : it's just a hint, to help latency a little, any other value will work fine. 
        /// Note 2 : size hint is guaranteed to be &lt= ZSTD_CStreamInSize()
        /// </returns>
        /// <remarks>
        /// size_t ZSTD_compressStream(ZSTD_CStream* zcs, ZSTD_outBuffer* output, ZSTD_inBuffer* input);
        /// </remarks>
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_compressStream(IntPtr zcs, ref ZSTD_Buffer output, ref ZSTD_Buffer input);


        //size_t ZSTD_flushStream(ZSTD_CStream* zcs, ZSTD_outBuffer* output);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_flushStream(IntPtr zcs, ref ZSTD_Buffer output);


        //size_t ZSTD_endStream(ZSTD_CStream* zcs, ZSTD_outBuffer* output);
        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        public static extern size_t ZSTD_endStream(IntPtr zcs, ref ZSTD_Buffer output);


        //Decompression


        [StructLayout(LayoutKind.Sequential)]
        internal struct ZSTD_Buffer
        {
            public ZSTD_Buffer(ArraySegmentPtr segmentPtr)
            {
                buffer = segmentPtr;
                size = (size_t)segmentPtr.Length;
                pos = default(size_t);
            }

            /// <summary>
            /// Start of buffer
            /// </summary>
            public IntPtr buffer;

            /// <summary>
            /// Size of output buffer
            /// </summary>
            public size_t size;

            /// <summary>
            /// Position where writing stopped. Will be updated. Necessarily 0 <= pos <= size
            /// </summary>
            public size_t pos;
        }


        //[StructLayout(LayoutKind.Sequential)]
        //internal struct ZSTD_outBuffer
        //{
        //    public ZSTD_outBuffer(ArraySegmentPtr segmentPtr)
        //    {
        //        dst = segmentPtr;
        //        size = default(size_t);
        //        pos = default(size_t);
        //    }

        //    /// <summary>
        //    /// Start of output buffer
        //    /// </summary>
        //    public IntPtr dst;

        //    /// <summary>
        //    /// Size of output buffer
        //    /// </summary>
        //    public size_t size;

        //    /// <summary>
        //    /// Position where writing stopped. Will be updated. Necessarily 0 <= pos <= size
        //    /// </summary>
        //    public size_t pos;
        //}

        //[StructLayout(LayoutKind.Sequential)]
        //internal struct ZSTD_inBuffer
        //{
        //    public ZSTD_inBuffer(ArraySegmentPtr segmentPtr)
        //    {
        //        src = segmentPtr;
        //        size = (size_t)segmentPtr.Length;
        //        pos = default(size_t);
        //    }

        //    /// <summary>
        //    /// Start of input buffer
        //    /// </summary>
        //    public readonly IntPtr src;

        //    /// <summary>
        //    /// Size of input buffer
        //    /// </summary>
        //    public size_t size;

        //    /// <summary>
        //    /// Position where reading stopped. Will be updated. Necessarily 0 <= pos <= size
        //    /// </summary>
        //    public size_t pos;
        //}
        #endregion
    }


}

