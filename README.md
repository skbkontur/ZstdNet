ZstdNet
=======

[![NuGet](https://img.shields.io/nuget/v/ZstdNet.svg)](https://www.nuget.org/packages/ZstdNet/)

**ZstdNet** is a wrapper of **Zstd** native library for .NET languages. It has the following features:

* Compression and decompression of byte arrays
* Generation of Dictionaries from a collection of samples

Streaming APIs are not implemented.

Take a look on a library reference or unit tests to explore its behavior in different situations.

Zstd
----

**Zstd**, short for Zstandard, is a new lossless compression algorithm, which
provides both good compression ratio _and_ speed for your standard compression
needs. "Standard" translates into everyday situations which neither look for
highest possible ratio (which LZMA and ZPAQ cover) nor extreme speeds (which
LZ4 covers). Zstandard is licensed under [BSD 3-Clause License](Native/LICENSE).

**Zstd** is developed by Yann Collet and the source is available at:
https://github.com/Cyan4973/zstd

The motivation to develop of the algorithm, ways of use and its properties are
explained in the blog that introduces the library:
http://fastcompression.blogspot.com/2015/01/zstd-stronger-compression-algorithm.html

The benefits of the dictionary mode are described here:
http://fastcompression.blogspot.ru/2016/02/compressing-small-data.html

Reference
---------

### Requirements

*ZstdNet* requires *Zstdlib* >= v1.0.0. Both 32-bit and 64-bit versions are supported.
The corresponding DLLs (compiled from v1.3.3 using Visual C++) are included in this repository.

### Exceptions

The wrapper throws `ZstdException` in case of malformed data or an error inside *Zstdlib*.
If the given destination buffer is too small, `InsufficientMemoryException` is thrown away.

### Compressor class

Allocates buffers for compression. Instances of this class are **not** thread-safe.

* `new Compressor()`

  `new Compressor(CompressionOptions options)`

  Constructors allow specifying compression options. Otherwise, default values will be used for `CompressionOptions`.
  Options will be exposed in `Options` read-only field.

  Note that `Compressor` class implements `IDisposable`.
  If you use a lot of instances of this class,
  it's recommended to call `Dispose` to avoid loading on the finalizer thread. For example:

  ```c#
  using (var compressor = new Compressor()) {
	  compressedData = compressor.Wrap(sourceData);
  }
  ```

* `byte[] Wrap(byte[] src)`

  `byte[] Wrap(ArraySegment<byte> src)`

  `int Wrap(byte[] src, byte[] dst, int offset)`

  `int Wrap(ArraySegment<byte> src, byte[] dst, int offset)`

  These methods compress data and save it in a new or
  an existing buffer (in such case a length of saved data will be returned).

* `static int GetCompressBound(int size)`

  Returns required destination buffer size for source data of size `size`.

### CompressionOptions class

Stores compression options and "digested" (for compression) information
from a compression dictionary, if present.
Instances of this class **are** thread-safe. They can be shared across threads to avoid
performance and memory overhead.

* `new CompressionOptions(byte[] dict, int compressionLevel = DefaultCompressionLevel)`

  `new CompressionOptions(int compressionLevel)`

  Options:
    - `byte[] dict` &mdash; compression dictionary.
      It can be read from a file or generated with `DictBuilder` class.
	  Default is `null` (no dictionary).
    - `int compressionLevel` &mdash; compression level.
  	  Should be in range from 1 to `CompressionOptions.MaxCompressionLevel` (currently 22).
  	  Default is `CompressionOptions.DefaultCompressionLevel` (currently 3).

  Specified options will be exposed in read-only fields.

  Note that `CompressionOptions` class implements `IDisposable`.
  If you use a lot of instances of this class,
  it's recommended to call `Dispose` to avoid loading on the finalizer thread. For example:

  ```c#
  using (var options = new CompressionOptions(dict, compressionLevel: 5))
  using (var compressor = new Compressor(options)) {
  	  compressedData = compressor.Wrap(sourceData);
  }
  ```

### Decompressor class

Allocates buffers for performing decompression. Instances of this class are **not** thread-safe.

* `new Decompressor()`

  `new Decompressor(DecompressionOptions options)`

  Constructors allow specifying decompression options. Otherwise, default values for `DecompressionOptions` will be used.
  Options will be exposed in `Options` read-only field.

  Note that `Decompressor` class implements `IDisposable`.
  If you use a lot of instances of this class,
  it's recommended to call `Dispose` to avoid loading on the finalizer thread. For example:

  ```c#
  using (var decompressor = new Decompressor()) {
	  decompressedData = decompressor.Unwrap(compressedData);
  }
  ```

* `byte[] Unwrap(byte[] src, size_t maxDecompressedSize = size_t.MaxValue)`

  `byte[] Unwrap(ArraySegment<byte> src, size_t maxDecompressedSize = size_t.MaxValue)`

  `int Unwrap(byte[] src, byte[] dst, int offset)`

  `int Unwrap(ArraySegment<byte> src, byte[] dst, int offset, bool bufferSizePrecheck = true)`

  These methods decompress data and save it in a new or
  an existing buffer (in such case a length of saved data will be returned).

  Data can be saved to a new buffer only if a field with decompressed data size
  is present in compressed data. You can limit size of the new buffer with
  `maxDecompressedSize` parameter (it's necessary to do this on untrusted data).

  If `bufferSizePrecheck` flag is set and the decompressed field length is specified,
  the size of the destination buffer will be checked before actual decompression.

  Note that if this field is malformed (and is less than actual decompressed data size),
  *Zstdlib* still doesn't allow a buffer overflow to happen during decompression.

* `static ulong GetDecompressedSize(byte[] src)`

  Reads a field with decompressed data size in compressed data.

### DecompressionOptions class

Stores decompression options and "digested" (for decompression) information
from a compression dictionary, if present.
Instances of this class **are** thread-safe. They can be shared across threads to avoid
performance and memory overhead.

* `new DecompressionOptions(byte[] dict)`

  Options:
    - `byte[] dict` &mdash; compression dictionary.
      It can be read from a file or generated with `DictBuilder` class.
	    Default is `null` (no dictionary).

  Specified options will be exposed in read-only fields.

  Note that `CompressionOptions` class implements `IDisposable`.
  If you use a lot of instances of this class,
  it's recommended to call `Dispose` to avoid loading on the finalizer thread. For example:

  ```c#
  using (var options = new DecompressionOptions(dict))
  using (var decompressor = new Decompressor(options)) {
  	  decompressedData = decompressor.Unwrap(compressedData);
  }
  ```

### DictBuilder static class

* `static byte[] TrainFromBuffer(ICollection<byte[]> samples, int dictCapacity = DefaultDictCapacity)`

  Generates a compression dictionary from a collection of samples.

  Options:
    - `int dictCapacity` &mdash; maximal dictionary size in bytes.
	  Default is `DictBuilder.DefaultDictCapacity`, currently 110 KiB (the default in zstd utility).

Wrapper Authors
---------------

Copyright (c) 2016-2017 [SKB Kontur](https://kontur.ru/eng/about)

*ZstdNet* is distributed under [BSD 3-Clause License](LICENSE).
