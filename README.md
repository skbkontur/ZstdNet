ZstdNet
=======

[![NuGet](https://img.shields.io/nuget/v/ZstdNet.svg)](https://www.nuget.org/packages/ZstdNet/)

**ZstdNet** is a wrapper of **Zstd** native library for .NET languages. It has the following features:

* Compression and decompression of byte arrays
* Streaming compression and decompression
* Generation of Dictionaries from a collection of samples

Take a look on a library reference or unit tests to explore its behavior in different situations.

Zstd
----

**Zstd**, short for Zstandard, is a fast lossless compression algorithm, which
provides both good compression ratio _and_ speed for your standard compression
needs. "Standard" translates into everyday situations which neither look for
highest possible ratio (which LZMA and ZPAQ cover) nor extreme speeds (which
LZ4 covers). Zstandard is licensed under [BSD 3-Clause License](ZstdNet/build/LICENSE).

**Zstd** is initially developed by Yann Collet and the source is available at:
https://github.com/facebook/zstd

The motivation to develop of the algorithm, ways of use and its properties are
explained in the blog that introduces the library:
http://fastcompression.blogspot.com/2015/01/zstd-stronger-compression-algorithm.html

The benefits of the dictionary mode are described here:
http://fastcompression.blogspot.ru/2016/02/compressing-small-data.html

Reference
---------

### Requirements

*ZstdNet* requires *libzstd* >= v1.4.0. Both 32-bit and 64-bit versions are supported.
The corresponding DLLs are included in this repository cross-compiled using
`(i686|x86_64)-w64-mingw32-gcc -DZSTD_MULTITHREAD -DZSTD_LEGACY_SUPPORT=0 -pthread -s`.
Note that `ZSTD_LEGACY_SUPPORT=0` means "do not support legacy formats" to minimize the binary size.

### Exceptions

The wrapper throws `ZstdException` in case of malformed data or an error inside *libzstd*.
If the given destination buffer is too small, `ZstdException` with `ZSTD_error_dstSize_tooSmall`
error code is thrown away.
Check [zstd_errors.h](https://github.com/facebook/zstd/blob/v1.4.5/lib/common/zstd_errors.h#L52) for more info.

### Compressor class

Block compression implementation. Instances of this class are **not thread-safe**.
Consider using `ThreadStatic` or pool of compressors for bulk processing.

* Constructor allow specifying compression options. Otherwise, default values will be used for `CompressionOptions`.

  ```c#
  Compressor();
  Compressor(CompressionOptions options);
  ```

  Options will be exposed in `Options` read-only field.

  Note that `Compressor` class implements `IDisposable`.
  If you use a lot of instances of this class,
  it's recommended to call `Dispose` to avoid loading on the finalizer thread. For example:

  ```c#
  using var compressor = new Compressor();
  var compressedData = compressor.Wrap(sourceData);
  ```

* `Wrap` compress data and save it in a new or an existing buffer (in such case a length of saved data will be returned).

  ```c#
  byte[] Wrap(byte[] src);
  byte[] Wrap(ArraySegment<byte> src);
  byte[] Wrap(ReadOnlySpan<byte> src);
  int Wrap(byte[] src, byte[] dst, int offset);
  int Wrap(ArraySegment<byte> src, byte[] dst, int offset);
  int Wrap(ReadOnlySpan<byte> src, byte[] dst, int offset);
  int Wrap(ReadOnlySpan<byte> src, Span<byte> dst);
  ```

  Note that on buffers close to 2GB `Wrap` tries its best, but if `src` is uncompressible and its size is too large,
  `ZSTD_error_dstSize_tooSmall` will be thrown. `Wrap` method call will only be reliable for a buffer size such that
  `GetCompressBoundLong(size) <= 0x7FFFFFC7`. Consider using streaming compression API on large data inputs.

* `GetCompressBound` returns required destination buffer size for source data of size `size`.

  ```c#
  static int GetCompressBound(int size);
  static ulong GetCompressBoundLong(ulong size);
  ```

### CompressionStream class

Implementation of streaming compression. The stream is write-only.

* Constructor

  ```c#
  CompressionStream(Stream stream);
  CompressionStream(Stream stream, int bufferSize);
  CompressionStream(Stream stream, CompressionOptions options, int bufferSize = 0);
  ```

  Options:
    - `Stream stream` &mdash; output stream for writing compressed data.
    - `CompressionOptions options`
      Default is `CompressionOptions.Default` with default compression level.
    - `int bufferSize` &mdash; buffer size used for compression buffer.
      Default is the result of calling `ZSTD_CStreamOutSize` which guarantees to successfully flush at least one complete compressed block (currently ~128KB).

  The buffer for compression is allocated using `ArrayPool<byte>.Shared.Rent()`.

  Note that `CompressionStream` class implements `IDisposable` and `IAsyncDisposable`.
  If you use a lot of instances of this class,
  it's recommended to call `Dispose` or `DisposeAsync` to avoid loading on the finalizer thread. For example:

  ```c#
  await using var compressionStream = new CompressionStream(outputStream, zstdBufferSize);
  await inputStream.CopyToAsync(compressionStream, copyBufferSize);
  ```

### CompressionOptions class

Stores compression options and "digested" (for compression) information
from a compression dictionary, if present.
Instances of this class **are thread-safe**. They can be shared across threads to avoid
performance and memory overhead.

* Constructor

  ```c#
  CompressionOptions(int compressionLevel);
  CompressionOptions(byte[] dict, int compressionLevel = DefaultCompressionLevel);
  CompressionOptions(byte[] dict, IReadOnlyDictionary<ZSTD_cParameter, int> advancedParams, int compressionLevel = DefaultCompressionLevel);
  ```

  Options:
    - `byte[] dict` &mdash; compression dictionary.
      It can be read from a file or generated with `DictBuilder` class.
      Default is `null` (no dictionary).
    - `int compressionLevel` &mdash; compression level.
      Should be in range from `CompressionOptions.MinCompressionLevel` to `CompressionOptions.MaxCompressionLevel` (currently 22).
      Default is `CompressionOptions.DefaultCompressionLevel` (currently 3).
    - `IReadOnlyDictionary<ZSTD_cParameter, int> advancedParams` &mdash; advanced API provides a way
      to set specific parameters during compression. For example, it allows you to compress with multiple threads,
      enable long distance matching mode and more.
      Check [zstd.h](https://github.com/facebook/zstd/blob/v1.4.5/lib/zstd.h#L265) for additional info.

  Specified options will be exposed in read-only fields.

  Note that `CompressionOptions` class implements `IDisposable`.
  If you use a lot of instances of this class,
  it's recommended to call `Dispose` to avoid loading on the finalizer thread. For example:

  ```c#
  using var options = new CompressionOptions(dict, compressionLevel: 5);
  using var compressor = new Compressor(options);
  var compressedData = compressor.Wrap(sourceData);
  ```

### Decompressor class

Block decompression implementation. Instances of this class are **not thread-safe**.
Consider using `ThreadStatic` or pool of decompressors for bulk processing.

* Constructor allow specifying decompression options. Otherwise, default values for `DecompressionOptions` will be used.

  ```c#
  new Decompressor();
  new Decompressor(DecompressionOptions options);
  ```

  Options will be exposed in `Options` read-only field.

  Note that `Decompressor` class implements `IDisposable`.
  If you use a lot of instances of this class,
  it's recommended to call `Dispose` to avoid loading on the finalizer thread. For example:

  ```c#
  using var decompressor = new Decompressor();
  var decompressedData = decompressor.Unwrap(compressedData);
  ```

* `Unwrap` decompress data and save it in a new or an existing buffer (in such case a length of saved data will be returned).

  ```c#
  byte[] Unwrap(byte[] src, int maxDecompressedSize = int.MaxValue);
  byte[] Unwrap(ArraySegment<byte> src, int maxDecompressedSize = int.MaxValue);
  byte[] Unwrap(ReadOnlySpan<byte> src, int maxDecompressedSize = int.MaxValue);
  int Unwrap(byte[] src, byte[] dst, int offset, bool bufferSizePrecheck = true);
  int Unwrap(ArraySegment<byte> src, byte[] dst, int offset, bool bufferSizePrecheck = true);
  int Unwrap(ReadOnlySpan<byte> src, byte[] dst, int offset, bool bufferSizePrecheck = true);
  int Unwrap(ReadOnlySpan<byte> src, Span<byte> dst, bool bufferSizePrecheck = true);
  ```

  Data can be saved to a new buffer only if a field with decompressed data size
  is present in compressed data. You can limit size of the new buffer with
  `maxDecompressedSize` parameter (it's necessary to do this on untrusted data).

  If `bufferSizePrecheck` flag is set and the decompressed field length is specified,
  the size of the destination buffer will be checked before actual decompression.

  Note that if this field is malformed (and is less than actual decompressed data size),
  *libzstd* still doesn't allow a buffer overflow to happen during decompression.

* `GetDecompressedSize` reads a field with decompressed data size stored in compressed data.

  ```c#
  static ulong GetDecompressedSize(byte[] src);
  static ulong GetDecompressedSize(ArraySegment<byte> src);
  static ulong GetDecompressedSize(ReadOnlySpan<byte> src);
  ```

### DecompressionStream class

Implementation of streaming decompression. The stream is read-only.

* Constructor

  ```c#
  DecompressionStream(Stream stream);
  DecompressionStream(Stream stream, int bufferSize);
  DecompressionStream(Stream stream, DecompressionOptions options, int bufferSize = 0);
  ```

  Options:
    - `Stream stream` &mdash; input stream for reading raw data.
    - `DecompressionOptions options`
      Default is `null` (no dictionary).
    - `int bufferSize` &mdash; buffer size used for decompression buffer.
      Default is the result of calling `ZSTD_DStreamInSize` &mdash; recommended size for input buffer (currently ~128KB).

  The buffer for decompression is allocated using `ArrayPool<byte>.Shared.Rent()`.

  Note that `DecompressionStream` class implements `IDisposable` and `IAsyncDisposable`.
  If you use a lot of instances of this class,
  it's recommended to call `Dispose` or `DisposeAsync` to avoid loading on the finalizer thread. For example:

  ```c#
  await using var decompressionStream = new DecompressionStream(inputStream, zstdBufferSize);
  await decompressionStream.CopyToAsync(outputStream, copyBufferSize);
  ```

### DecompressionOptions class

Stores decompression options and "digested" (for decompression) information
from a compression dictionary, if present.
Instances of this class **are thread-safe**. They can be shared across threads to avoid
performance and memory overhead.

* Constructor

  ```c#
  DecompressionOptions();
  DecompressionOptions(byte[] dict);
  DecompressionOptions(byte[] dict, IReadOnlyDictionary<ZSTD_dParameter, int> advancedParams);
  ```

  Options:
    - `byte[] dict` &mdash; compression dictionary.
      It can be read from a file or generated with `DictBuilder` class.
      Default is `null` (no dictionary).
    - `IReadOnlyDictionary<ZSTD_dParameter, int> advancedParams` &mdash; advanced decompression API
      that allows you to set parameters like maximum memory usage.
      Check [zstd.h](https://github.com/facebook/zstd/blob/v1.4.5/lib/zstd.h#L513) for additional info.

  Specified options will be exposed in read-only fields.

  Note that `CompressionOptions` class implements `IDisposable`.
  If you use a lot of instances of this class,
  it's recommended to call `Dispose` to avoid loading on the finalizer thread. For example:

  ```c#
  using var options = new DecompressionOptions(dict);
  using var decompressor = new Decompressor(options);
  var decompressedData = decompressor.Unwrap(compressedData);
  ```

### DictBuilder static class

* `TrainFromBuffer` generates a compression dictionary from a collection of samples.

  ```c#
  static byte[] TrainFromBuffer(IEnumerable<byte[]> samples, int dictCapacity = DefaultDictCapacity);
  ```

  Options:
    - `int dictCapacity` &mdash; maximal dictionary size in bytes.
      Default is `DictBuilder.DefaultDictCapacity`, currently 110 KiB (the default in zstd utility).

Wrapper Authors
---------------

Copyright (c) 2016-present [SKB Kontur](https://kontur.ru/eng/about)

*ZstdNet* is distributed under [BSD 3-Clause License](LICENSE).
