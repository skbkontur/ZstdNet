using System;
using System.Runtime.InteropServices;

namespace ZstdNet
{
	internal class ArraySegmentPtr : IDisposable
	{
		public ArraySegmentPtr(ArraySegment<byte> segment)
		{
			handle = GCHandle.Alloc(segment.Array, GCHandleType.Pinned);
			arr = segment.Array;
			offset = segment.Offset;
		}

		public static implicit operator IntPtr(ArraySegmentPtr pinner)
		{
			return Marshal.UnsafeAddrOfPinnedArrayElement(pinner.arr, pinner.offset);
		}

		public void Dispose()
		{
			handle.Free();
		}

		private GCHandle handle;
		private readonly byte[] arr;
		private readonly int offset;
	}
}
