using System;
using System.Runtime.InteropServices;

namespace ZstdNet
{
	internal struct ArraySegmentPtr : IDisposable
	{
		public ArraySegmentPtr(ArraySegment<byte> segment) : this(segment.Array, segment.Offset, segment.Count)
		{
		}

        public ArraySegmentPtr(byte[] array) : this(array, 0, array.Length)
        {

        }

        public ArraySegmentPtr(byte[] buffer, int offset, int count)
        {
            handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            this.offset = offset;
            this.count = count;
        }

        public byte[] Array => handle.Target as byte[];
        public int Length => count;

        public static implicit operator IntPtr(ArraySegmentPtr pinner)
		{
			return Marshal.UnsafeAddrOfPinnedArrayElement(pinner.Array, pinner.offset);
		}

		public void Dispose()
		{
			handle.Free();
		}
        
        private GCHandle handle;
		private readonly int offset;
        private readonly int count;
    }
}
