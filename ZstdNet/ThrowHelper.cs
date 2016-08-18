using System;
using System.Runtime.InteropServices;
using size_t = System.UInt64;

namespace ZstdNet
{
	internal static class ReturnValueExtensions
	{
		public static size_t EnsureSuccess(this size_t returnValue)
		{
			if(ExternMethods.ZSTD_isError(returnValue) != 0)
				throw new ZstdException(Marshal.PtrToStringAnsi(ExternMethods.ZSTD_getErrorName(returnValue)));
			return returnValue;
		}

		public static IntPtr EnsureSuccess(this IntPtr returnValue)
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
