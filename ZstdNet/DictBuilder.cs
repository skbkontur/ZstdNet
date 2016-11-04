using System;
using System.Collections.Generic;
using System.Linq;
using size_t = System.UIntPtr;

namespace ZstdNet
{
	public static class DictBuilder
	{
		public static byte[] TrainFromBuffer(ICollection<byte[]> samples, int dictCapacity = DefaultDictCapacity)
		{
			var samplesBuffer = samples.SelectMany(sample => sample).ToArray();
			var samplesSizes = samples.Select(sample => (size_t)sample.Length).ToArray();
			var dictBuffer = new byte[dictCapacity];
			var dictSize = (int)ExternMethods.ZDICT_trainFromBuffer(dictBuffer, (size_t)dictCapacity, samplesBuffer, samplesSizes, (uint)samples.Count).EnsureZdictSuccess();

			if (dictCapacity == dictSize)
				return dictBuffer;
			var result = new byte[dictSize];
			Array.Copy(dictBuffer, result, dictSize);
			return result;
		}

		public const int DefaultDictCapacity = 112640; // Used by zstd utility by default
	}
}
