using System;
using System.Runtime.InteropServices;
#if BUILD64
using size_t = System.UInt64;
#else
using size_t = System.UInt32;
#endif

namespace ZstdNet
{
	internal static class ReturnValueExtensions
	{
		public static size_t EnsureZdictSuccess(this size_t returnValue)
		{
			if(ExternMethods.ZDICT_isError(returnValue) != 0)
				ThrowException(returnValue, Marshal.PtrToStringAnsi(ExternMethods.ZDICT_getErrorName(returnValue)));
			return returnValue;
		}

		public static size_t EnsureZstdSuccess(this size_t returnValue)
		{
			if(ExternMethods.ZSTD_isError(returnValue) != 0)
				ThrowException(returnValue, Marshal.PtrToStringAnsi(ExternMethods.ZSTD_getErrorName(returnValue)));
			return returnValue;
		}

		private static void ThrowException(size_t returnValue, string message)
		{
			if (-unchecked((int)returnValue) == ZSTD_error_dstSize_tooSmall)
				throw new InsufficientMemoryException(message);
			throw new ZstdException(message);
		}

		// ReSharper disable once InconsistentNaming
		private const int ZSTD_error_dstSize_tooSmall = 9;

		public static IntPtr EnsureZstdSuccess(this IntPtr returnValue)
		{
			if(returnValue == IntPtr.Zero)
				throw new ZstdException("Failed to create a structure");
			return returnValue;
		}
	}

	public class ZstdException : Exception
	{
		public ZstdException(string message): base(message) { }
	}
}
