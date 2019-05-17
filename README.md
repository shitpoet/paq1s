# paq1s

Readable PAQ1 implementation.

This source code is reimplementation of (part of) PAQ1 compressor in C#.

The goal of this implementation is study of PAQ1 algorithm.

The program is not optimized for speed or memory usage, but gives paractially the same compression results as original PAQ1 without additional models (m2-m4).

## Differencies

Differencies between original `paq1.cpp` and `paq1s.cs`:

* All "additional" modesls are removed (string match, word, cyclic aka fixed-length record). This reduces compression, but not dramatically. The main (most general) direct context model is here.
* Simplified counters (like Counter3 in original PAQ1 source code)
* Uses standard C# hashtable (Dictionary)
* Some other non-essential functionaly is omitted (file decompression, writing and reading of PAQ archive header)

This implementation gives approximately the same results as original PAQ1 with models `m2`-`m4` disabled.

## Test

Results on concatenated Calgary:

    orig size: 3 141 622 bytes
    comp size:   744 453 bytes
    ratio: 23.696%
    1.896 bpc

Original PAQ1 without m1-m2 models gives 751 734 bytes (this probably includes small PAQ1 file header).

Size differencies are due to no file header, different arithmetic coder, no hash collisions in Dictionary and (probably to very small extent accroding to Matt's paper) to simplified storage of counters (PAQ1 stores counters in compact approximated form).


## See also

 * <a href='https://cs.fit.edu/~mmahoney/compression/paq.html'>Site on PAQ compressors</a> by its author - Matt Mahoney

 * Original <a href='http://www.mattmahoney.net/dc/paq1.pdf'>paper on PAQ1</a> algorithm by Matt Mahoney

 * Unofficial <a href='https://github.com/shitpoet/paq1'>repository with original PAQ1 source code</a>

## Licence

GPL
