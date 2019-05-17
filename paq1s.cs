/*

  PAQ1s - simple implementation of (part of) PAQ1

  author: Ruslan Odintsov, shitpoet@gmail.com, 2019
  licence: GPL

*/

using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using static System.Console;
using static System.Math;
using static System.IO.File;


/* arithmetic encoder/decoder */

class Ar {
  const uint msb = 0x80000000;
  const uint mask = msb - 1;
  const int width = 32;
  const int shift = 31;
  uint lo = 0;
  uint hi = 0xffffffff;
  Queue<int> bits;
  uint y;

  public Ar() { // for encoding
    bits = new Queue<int>();
  }

  public Ar(Queue<int> bits) { // for decoding
    this.bits = bits;
    y = 0;
    for (var i = width - 1; i >= 0; i--) {
      y = y | ((uint)read() << i);
    }
  }

  bool check(uint lo, uint med, uint hi) {
    return med - 1 < hi;
  }

  uint read() {
    if (bits.Count > 0) {
      uint bit = (uint)bits.Dequeue();
      return bit;
    } else {
      return 0;
    }
  }

  void write(uint bit) {
    bits.Enqueue((int)bit);
  }

  public void encode(int x, int n0, int n1) {
    var n = n0 + n1;
    var p = 1.0 * n0 / n;
    var med = (uint)Floor(lo + p * (hi - lo)) + 1;
    if (check(lo, med, hi)) {
      // 0 -> lo .. med-1, 1 -> med .. hi
      if (x == 0) {
        hi = med - 1;
      } else {
        lo = med;
      }
      while ((lo & msb) == (hi & msb)) {
        write((lo & msb) >> shift);
        lo = ((lo & mask) << 1) | 0;
        hi = ((hi & mask) << 1) | 1;
      }
    } else {
      WriteLine("ar overflow");
    }
  }

  // to be called at the end of stream
  public Queue<int> flush() {
    while (lo > 0) {
      write((lo & msb) >> shift);
      lo = (lo & mask) << 1;
    }
    return bits;
  }

  public int decode(int n0, int n1) {
    var n = n0 + n1;
    var p = 1.0 * n0 / n;
    var med = (uint)Floor(lo + p * (hi - lo)) + 1;
    if (check(lo, med, hi)) {
      int bit = 0;
      if (y < med) {
        hi = med - 1;
        bit = 0;
      } else {
        lo = med;
        bit = 1;
      }
      while ((lo & msb) == (hi & msb)) {
        lo = ((lo & mask) << 1) | 0;
        hi = ((hi & mask) << 1) | 1;
        y  = ((y & mask) << 1) | read();
      }
      return bit;
    } else {
      WriteLine("dar overflow");
      return 0;
    }
  }
}


/* compressor core */

class App {

  int len(Array a) {
    return a.Length;
  }

  string format_int(int x) {
    return x.ToString("0 000");
  }

  string format_float(double x) {
    return x.ToString("0.000");
  }

  // read bytes as array of bits
  // MSB first - order is important
  // it has noticiable effect on compression ratio
  int[] read_bits(string fn) {
    var bytes = ReadAllBytes(fn);
    var n = len(bytes);
    var bits = new int[n * 8];
    var m = 0;
    foreach (var x in bytes) {
      for (var i = 7; i >= 0; i--) { // msb first
        //bits[m++] = (x >> i) & 1;
        bits[m++] = (int)( (uint)( ((uint)x >> i) & 1 ) );
      }
    }
    return bits;
  }

  void write_bits(string fn, int[] bits) {
    var m = len(bits);
    var n = m / 8 + (m % 8 != 0 ? 1 : 0);
    var bytes = new byte[n];
    var i = 0;
    var j = 0;
    while (i < m) {
      var max_di = Min(7, m - 1 - i);
      var shift = 7;
      for (var di = max_di; di >= 0; di--) { // msb first
        var bit = bits[i + (max_di - di)];
        bytes[j] = (byte)( bytes[j] | (bit << shift) );
        shift--;
      }
      i += 8;
      j++;
    }
    WriteAllBytes(fn, bytes);
  }

  // compare contexts (arrays of bits) by value
  public class CtxComparer : IEqualityComparer<int[]> {
    public bool Equals(int[] a, int[] b) {
      for (int i = 0; i < a.Length; i++) {
        if (a[i] != b[i]) {
          return false;
        }
      }
      return true;
    }
    public int GetHashCode(int[] a) {
      int hash = 17;
      for (int i = 0; i < a.Length; i++) {
        unchecked {
          hash = hash * 23 + a[i];
        }
      }
      return hash;
    }
  }

  class Model {
    // byte order of model in range 0..N-1
    int order;
    // context - `order + 1` bytes
    // last byte is partial - 0-7 bits
    int[] ctx;
    // counters: key - context, value - counters pair (n0,n1)
    Dictionary<int[], int[]> c;

    public Model(int order) {
      // order is zero-based: 0..N-1
      this.order = order;
      // so length of context in bytes is `order + 1`
      this.ctx = new int[order + 1];
      // leading 1 is used to differenciate 0-7 bit contexts:
      // 0010 is not the same as 0100, through they both
      // contain zeroes only
      this.ctx[order] = 1;
      this.c = new Dictionary<int[], int[]>(new CtxComparer());
    }

    void update_context(int bit) {
      var o = order;
      ctx[o] = (ctx[o] << 1) | bit;
      if (ctx[o] >= 256) { // new byte
        ctx[o] = ctx[o] & 0xff;
        // shift bytes of context to create a new one
        for (var i = 0; i < o; i++) {
          ctx[i] = ctx[i + 1];
        }
        ctx[o] = 1; // reset last byte to special leading 1
      }
    }

    public void update(int bit) {
      var a = bit;
      var b = 1 - a;

      var el = c[ctx];
      if (el[a] < 255) el[a]++;
      if (el[b] > 0) {
        el[b] = el[b] / 2 + 1;
      }

      update_context(bit);
    }

    public int[] predict() {
      if (!c.ContainsKey(ctx)) {
        // context is seen for first time: (0,0)
        // note: we use Clone() here because Dictionary
        // stroes references to keys, and not their
        // copies, so when `update_context` is called
        // the dictionary will work incorrectly
        // without Clone() here
        c[ctx.Clone() as int[]] = new int[2];
      }
      return c[ctx];
    }
  }

  const int N = 8; // number of non-stationary contexts

  Model[] m = new Model[N];

  int[] predict() {
    int n0 = 1;
    int n1 = 1;
    for (var i = 0; i < N; i++) {
      var w = (i + 1) * (i + 1);
      var n = m[i].predict();
      n0 += w * n[0];
      n1 += w * n[1];
    }
    return new int[] { n0, n1 };
  }

  void update(int bit) {
    for (var i = 0; i < N; i++) {
      m[i].update(bit);
    }
  }

  void compress_file(string fn, string cfn) {
    var bits = read_bits(fn);
    var bs = len(bits);

    var ar = new Ar();
    for (var i = 0; i < N; i++) {
      m[i] = new Model(i);
    }

    foreach (var bit in bits ) {
      var n = predict();
      ar.encode(bit, n[0], n[1]);
      update(bit);
    }

    var cbits_queue = ar.flush();
    int[] cbits = cbits_queue.ToArray();
    write_bits(cfn, cbits);

    var cbs = len(cbits);
    WriteLine($"orig size: {format_int(bs / 8)}");
    WriteLine($"comp size: {format_int(len(cbits) / 8)}");
    WriteLine($"ratio: {format_float(cbs * 100.0 / bs)}%");
    var bpc = cbs * 1.0 / (bs / 8);
    WriteLine($"{format_float(bpc)} bpc");

    var ok = check(fn, cfn, bs, cbs);
    WriteLine(ok ? "ok" : "check failed");
  }

  // check whether compressed file can be
  // decompressd to original one
  bool check(string fn, string cfn, int bs, int cbs) {
    var obits = read_bits(fn);
    var cbits = read_bits(cfn);
    var cbits_list = cbits.ToList();
    var cbits_queue = new Queue<int>(cbits_list);
    var dar = new Ar(cbits_queue);

    // reset models
    for (var i = 0; i < N; i++) {
      m[i] = new Model(i);
    }

    for (var i = 0; i < bs; i++) {
      var n = predict();
      var bit = dar.decode(n[0], n[1]);
      if (bit != obits[i]) {
        WriteLine($"decompression error at {i}-th bit");
        WriteLine($"{obits[i]} was expected instead of {bit}");
        return false;
      }
      update(bit);
    }
    return true;
  }

  // progream entry point
  public App(string[] args) {
    if (args.Length == 2) {
      var fn = args[0];
      var cfn = args[1];
      compress_file(fn, cfn);
    } else {
      WriteLine("usage: paq1s uncompressed-file destination-file");
      WriteLine("note: there's no ability to decompress a file ");
      WriteLine("      using paq1s, but there is built-in check ");
      WriteLine("      for correct decompression. ");
    }
  }
}

class Program {
  static void Main(string[] args) {
    new App(args);
  }
}
