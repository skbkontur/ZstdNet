using System;
using size_t = System.UIntPtr;

namespace ZstdNet
{
	public class DecompressionOptions : IDisposable
	{
		public DecompressionOptions(byte[] dict)
		{
			Dictionary = dict;

			if(dict != null)
				Ddict = ExternMethods.ZSTD_createDDict(dict, (size_t)dict.Length).EnsureZstdSuccess();
			else
				GC.SuppressFinalize(this); // No unmanaged resources
		}

		~DecompressionOptions()
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

			if(Ddict != IntPtr.Zero)
				ExternMethods.ZSTD_freeDDict(Ddict);

			disposed = true;
		}

		private bool disposed = false;

		public readonly byte[] Dictionary;

		internal readonly IntPtr Ddict;
	}
}
