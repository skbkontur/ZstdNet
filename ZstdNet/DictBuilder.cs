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
			var ms = new MemoryStream();
			var samplesSizes = samples.Select(sample =>
			{
				ms.Write(sample, 0, sample.Length);
				return (size_t)sample.Length;
			}).ToArray();

			var dictBuffer = new byte[dictCapacity];
			var dictSize = (int)ExternMethods
				.ZDICT_trainFromBuffer(dictBuffer, (size_t)dictCapacity, ms.GetBuffer(), samplesSizes, (uint)samplesSizes.Length)
				.EnsureZdictSuccess();

			if(dictCapacity != dictSize)
				Array.Resize(ref dictBuffer, dictSize);

			return dictBuffer;
		}

		public const int DefaultDictCapacity = 112640; // Used by zstd utility by default
	}
}
