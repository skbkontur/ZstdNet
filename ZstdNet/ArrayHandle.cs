using System;
using System.Runtime.InteropServices;

namespace ZstdNet
{
	internal struct ArrayHandle : IDisposable
	{
		public ArrayHandle(byte[] array)
			: this(array, 0)
		{}

		public ArrayHandle(byte[] array, int offset)
		{
			this.array = array;
			this.offset = offset;
			handle = GCHandle.Alloc(array, GCHandleType.Pinned);
		}

		public static implicit operator IntPtr(ArrayHandle pinned)
			=> Marshal.UnsafeAddrOfPinnedArrayElement(pinned.array, pinned.offset);

		public void Dispose() => handle.Free();

		private readonly byte[] array;
		private readonly int offset;
		private GCHandle handle;
	}
}
