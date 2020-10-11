using System;
using size_t = System.UIntPtr;

namespace ZstdNet
{
	public class CompressionOptions : IDisposable
	{
		public CompressionOptions(int compressionLevel)
		{
			if(compressionLevel < MinCompressionLevel || compressionLevel > MaxCompressionLevel)
				throw new ArgumentOutOfRangeException(nameof(compressionLevel));

			CompressionLevel = compressionLevel;
		}

		public CompressionOptions(byte[] dict, int compressionLevel = DefaultCompressionLevel)
			: this(compressionLevel)
		{
			Dictionary = dict;

			if(dict != null)
				Cdict = ExternMethods.ZSTD_createCDict(dict, (size_t)dict.Length, compressionLevel).EnsureZstdSuccess();
			else
				GC.SuppressFinalize(this); // No unmanaged resources
		}

		~CompressionOptions() => Dispose(false);

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if(Cdict == IntPtr.Zero)
				return;

			ExternMethods.ZSTD_freeCDict(Cdict);

			Cdict = IntPtr.Zero;
		}

		public static int MinCompressionLevel => ExternMethods.ZSTD_minCLevel();
		public static int MaxCompressionLevel => ExternMethods.ZSTD_maxCLevel();

		public const int DefaultCompressionLevel = 3; // Used by zstd utility by default

		public static CompressionOptions Default { get; } = new CompressionOptions(DefaultCompressionLevel);

		public readonly int CompressionLevel;
		public readonly byte[] Dictionary;

		internal IntPtr Cdict;
	}
}
