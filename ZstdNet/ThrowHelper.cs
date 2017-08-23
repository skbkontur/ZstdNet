using System;
using System.Runtime.InteropServices;
using size_t = System.UIntPtr;

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
			var code = unchecked(0 - (uint) (ulong) returnValue); // Negate returnValue (UintPtr)
			if(code == ZSTD_error_dstSize_tooSmall)
				throw new InsufficientMemoryException(message);
			throw new ZstdException(message);
		}

		// ReSharper disable once InconsistentNaming
		// NOTE that this const may change on zstdlib update (error codes API is still considered unstable) https://github.com/facebook/zstd/blob/master/lib/common/zstd_errors.h
		private const int ZSTD_error_dstSize_tooSmall = 70;

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
