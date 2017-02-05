using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using size_t = System.UIntPtr;

namespace ZstdNet
{
	public static class DictBuilder
	{
		public static byte[] TrainFromBuffer(IEnumerable<byte[]> samples, int dictCapacity = DefaultDictCapacity)
		{
			var ms = new MemoryStream ();
			var samplesSizes = new List<size_t> ();
			foreach (var sample in samples) {
				samplesSizes.Add ((size_t) sample.Length);
				ms.Write (sample, 0, sample.Length);
			}
			var samplesBuffer = ms.ToArray ();

			var dictBuffer = new byte[dictCapacity];
			var dictSize = (int)ExternMethods
				.ZDICT_trainFromBuffer(dictBuffer, (size_t)dictCapacity, samplesBuffer, samplesSizes.ToArray(), (uint)samplesSizes.Count)
				.EnsureZdictSuccess();
			if (dictCapacity != dictSize) {
				Array.Resize<byte> (ref dictBuffer, dictSize);
			}
			return dictBuffer;
		}

		public const int DefaultDictCapacity = 112640; // Used by zstd utility by default
	}
}
