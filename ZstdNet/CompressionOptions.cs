using System;
using size_t = System.UIntPtr;

namespace ZstdNet
{
	public class CompressionOptions : IDisposable
	{
		public CompressionOptions(byte[] dict, int compressionLevel = DefaultCompressionLevel)
			: this(compressionLevel)
		{
			Dictionary = dict;

			if(dict != null)
				Cdict = ExternMethods.ZSTD_createCDict(dict, (size_t)dict.Length, compressionLevel).EnsureZstdSuccess();
			else
				GC.SuppressFinalize(this); // No unmanaged resources
		}

		public CompressionOptions(int compressionLevel)
		{
			CompressionLevel = compressionLevel;
		}

		~CompressionOptions()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if(disposed)
				return;

			if(Cdict != IntPtr.Zero)
				ExternMethods.ZSTD_freeCDict(Cdict);

			disposed = true;
		}

		private bool disposed = false;

		public static int MaxCompressionLevel
		{
			get { return ExternMethods.ZSTD_maxCLevel(); }
		}

		public const int DefaultCompressionLevel = 3; // Used by zstd utility by default

		public readonly int CompressionLevel;
		public readonly byte[] Dictionary;

		internal readonly IntPtr Cdict;
	}
}
