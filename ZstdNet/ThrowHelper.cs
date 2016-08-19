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
				ThrowException(Marshal.PtrToStringAnsi(ExternMethods.ZDICT_getErrorName(returnValue)));
			return returnValue;
		}

		public static size_t EnsureZstdSuccess(this size_t returnValue)
		{
			if(ExternMethods.ZSTD_isError(returnValue) != 0)
				ThrowException(Marshal.PtrToStringAnsi(ExternMethods.ZSTD_getErrorName(returnValue)));
			return returnValue;
		}

		private static void ThrowException(string message)
		{
			if (message == "Destination buffer is too small")
				throw new InsufficientMemoryException(message);
			throw new ZstdException(message);
		}

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
