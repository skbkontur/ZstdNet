ZstdNet
=======

Zstd wrapper for .NET

Description
-----------

**ZstdNet** is a wrapper of **Zstd** native library for .NET languages. The following APIs are available:

* Compression and decompression are available via `Compressor` and `Decompressor` classes.
They can use an available buffer or allocate the buffer themselves. Supported options:

    - `byte[] dict` &mdash; dictionary (loaded from a file or generated via a method described below)
    - `int compressionLevel` &mdash; compression level (currently can be varied from 1 to 22)

* Dictionary generation from a collection of samples is available
via `DictBuilder.TrainFromBuffer` static method. Supported options:

    - `int dictCapacity` &mdash; maximal dictionary size in bytes

The wrapper throws `ZstdException` in case of malformed data or an error inside *Zstdlib*.
If the given destination buffer is too small, `InsufficientMemoryException` is thrown.

Streaming APIs are not implemented yet.

Unit tests provided with this library show its behavior in different situations.

*ZstdNet* requires *Zstdlib* >= v0.8.1. Both 32-bit and 64-bit versions are supported.
The corresponding DLLs (compiled from v0.8.1 using Visual C++) are included in this repository.

Zstd
----

**Zstd**, short for Zstandard, is a new lossless compression algorithm, which
provides both good compression ratio _and_ speed for your standard compression
needs. "Standard" translates into everyday situations which neither look for
highest possible ratio (which LZMA and ZPAQ cover) nor extreme speeds (which
LZ4 covers).

**Zstd** is developed by Yann Collet and the source is available at:
https://github.com/Cyan4973/zstd

The motivation for development, the algorithm used and its properties are
explained in the blog post that introduces the library:
http://fastcompression.blogspot.com/2015/01/zstd-stronger-compression-algorithm.html

The benefits of the dictionary mode are described here:
http://fastcompression.blogspot.ru/2016/02/compressing-small-data.html

Wrapper Authors
---------------

Copyright (c) 2016 [SKB Kontur](https://kontur.ru/eng/about)

*ZstdNet* is licensed under [GNU GPL, version 3](COPYING).
