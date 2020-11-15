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
			var code = unchecked(0 - (uint)(ulong)returnValue); // Negate returnValue (UIntPtr)
			throw new ZstdException(unchecked((ZSTD_ErrorCode)code), message);
		}

		public static IntPtr EnsureZstdSuccess(this IntPtr returnValue)
		{
			if(returnValue == IntPtr.Zero)
				throw new ZstdException(ZSTD_ErrorCode.ZSTD_error_GENERIC, "Failed to create a structure");
			return returnValue;
		}
	}

	public class ZstdException : Exception
	{
		public ZstdException(ZSTD_ErrorCode code, string message) : base(message)
			=> Code = code;

		public ZSTD_ErrorCode Code { get; }
	}
}
