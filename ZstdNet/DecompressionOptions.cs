using System;
using System.Collections.Generic;
using size_t = System.UIntPtr;

namespace ZstdNet
{
	public class DecompressionOptions : IDisposable
	{
		public DecompressionOptions()
			: this(null)
		{}

		public DecompressionOptions(byte[] dict)
		{
			Dictionary = dict;

			if(dict != null)
				Ddict = ExternMethods.ZSTD_createDDict(dict, (size_t)dict.Length).EnsureZstdSuccess();
			else
				GC.SuppressFinalize(this); // No unmanaged resources
		}

		public DecompressionOptions(byte[] dict, IReadOnlyDictionary<ZSTD_dParameter, int> advancedParams)
			: this(dict)
		{
			if(advancedParams == null)
				return;

			foreach(var param in advancedParams)
			{
				var bounds = ExternMethods.ZSTD_dParam_getBounds(param.Key);
				bounds.error.EnsureZstdSuccess();

				if(param.Value < bounds.lowerBound || param.Value > bounds.upperBound)
					throw new ArgumentOutOfRangeException(nameof(advancedParams), $"Advanced parameter '{param.Key}' is out of range [{bounds.lowerBound}, {bounds.upperBound}]");
			}

			this.AdvancedParams = advancedParams;
		}

		internal void ApplyDecompressionParams(IntPtr dctx)
		{
			if(AdvancedParams == null)
				return;

			foreach(var param in AdvancedParams)
				ExternMethods.ZSTD_DCtx_setParameter(dctx, param.Key, param.Value).EnsureZstdSuccess();
		}

		~DecompressionOptions() => Dispose(false);

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing)
		{
			if(Ddict == IntPtr.Zero)
				return;

			ExternMethods.ZSTD_freeDDict(Ddict);

			Ddict = IntPtr.Zero;
		}

		public readonly byte[] Dictionary;
		public readonly IReadOnlyDictionary<ZSTD_dParameter, int> AdvancedParams;

		internal IntPtr Ddict;
	}
}
