using System;
using System.Collections.Generic;
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

		public CompressionOptions(byte[] dict, IReadOnlyDictionary<ZSTD_cParameter, int> advancedParams, int compressionLevel = DefaultCompressionLevel)
			: this(dict, compressionLevel)
		{
			if(advancedParams == null)
				return;

			foreach(var param in advancedParams)
			{
				var bounds = ExternMethods.ZSTD_cParam_getBounds(param.Key);
				bounds.error.EnsureZstdSuccess();

				if(param.Value < bounds.lowerBound || param.Value > bounds.upperBound)
					throw new ArgumentOutOfRangeException(nameof(advancedParams), $"Advanced parameter '{param.Key}' is out of range [{bounds.lowerBound}, {bounds.upperBound}]");
			}

			this.AdvancedParams = advancedParams;
		}

		internal void ApplyCompressionParams(IntPtr cctx)
		{
			if(AdvancedParams == null || !AdvancedParams.ContainsKey(ZSTD_cParameter.ZSTD_c_compressionLevel))
				ExternMethods.ZSTD_CCtx_setParameter(cctx, ZSTD_cParameter.ZSTD_c_compressionLevel, CompressionLevel).EnsureZstdSuccess();

			if(AdvancedParams == null)
				return;

			foreach(var param in AdvancedParams)
				ExternMethods.ZSTD_CCtx_setParameter(cctx, param.Key, param.Value).EnsureZstdSuccess();
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
		public readonly IReadOnlyDictionary<ZSTD_cParameter, int> AdvancedParams;

		internal IntPtr Cdict;
	}
}
