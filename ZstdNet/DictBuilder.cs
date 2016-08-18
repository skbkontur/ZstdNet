using System;
using System.Collections.Generic;
using System.Linq;
using size_t = System.UInt64;

namespace ZstdNet
{
	public static class DictBuilder
	{
		public static byte[] TrainFromBuffer(ICollection<byte[]> samples, int dictCapacity = DefaultDictCapacity)
		{
			var samplesBuffer = samples.SelectMany(sample => sample).ToArray();
			var samplesSizes = samples.Select(sample => (size_t)sample.Length).ToArray();
			var dictBuffer = new byte[dictCapacity];
			var dictSize = ExternMethods.ZDICT_trainFromBuffer(dictBuffer, (size_t)dictCapacity, samplesBuffer, samplesSizes, (uint)samples.Count).EnsureSuccess();

			var result = new byte[dictSize];
			Array.Copy(dictBuffer, result, (int)dictSize);
			return result;
		}

		public const int DefaultDictCapacity = 112640; // Used by zstd utility by default
	}
}
