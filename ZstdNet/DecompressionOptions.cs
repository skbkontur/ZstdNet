using System;
#if BUILD64
using size_t = System.UInt64;
#else
using size_t = System.UInt32;
#endif

namespace ZstdNet
{
	public class DecompressionOptions
	{
		public DecompressionOptions(byte[] dict)
		{
			Dictionary = dict;

			if (dict != null)
				Ddict = ExternMethods.ZSTD_createDDict(dict, (size_t)dict.Length).EnsureZstdSuccess();
		}

		~DecompressionOptions()
		{
			Dispose(false);
		}

		public void Dispose()
		{
			Dispose(true);
		}

		private void Dispose(bool disposing)
		{
			if (disposed)
				return;
			disposed = true;

			if (Ddict != IntPtr.Zero)
				ExternMethods.ZSTD_freeDDict(Ddict);

			if (disposing)
				GC.SuppressFinalize(this);
		}

		private bool disposed = false;

		public readonly byte[] Dictionary;

		internal readonly IntPtr Ddict;
	}
}
