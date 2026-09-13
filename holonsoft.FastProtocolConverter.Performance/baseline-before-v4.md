# Performance baseline before the v4 work

Measured at commit `bcea658`, the state of the library before the v4 renovation started.

Measured with BenchmarkDotNet on net10.0, `--job short`, `[MemoryDiagnoser]`.
Reproduce with:

```
dotnet run -c Release --project holonsoft.FastProtocolConverter.Performance -- --benchmark --filter * --job short
```

The payload is `BenchmarkPoco`, a 38 byte frame containing every supported primitive
(int, uint, short, ushort, byte, bool, float, double, long, enum).

| Method                                    | Mean      | Allocated |
|------------------------------------------ |----------:|----------:|
| Read  38 byte POCO, little endian         | 116.12 ns |     344 B |
| Read  38 byte POCO, big endian            | 143.42 ns |     568 B |
| Read  38 byte POCO into reused instance   | 113.81 ns |     288 B |
| Write 38 byte POCO, little endian         | 237.58 ns |     888 B |
| Write 38 byte POCO, big endian            | 339.97 ns |    1528 B |
| Read  POCO with fixed length string       |  65.65 ns |     288 B |
| Write POCO with fixed length string       | 160.16 ns |     384 B |

## What the numbers say

**Writing is roughly twice as expensive as reading** (237 ns against 116 ns little endian,
340 ns against 143 ns big endian). That matches the implementation: reading uses a compiled
setter delegate, writing still calls `FieldInfo.GetValue` per field, which is reflection.

**The allocation per message is far larger than the message itself.** A 38 byte frame costs
888 bytes to write, about 23 times its own size, and 1528 bytes in big endian, about 40 times.
Sources, per field:

* `FieldInfo.GetValue` boxes the value
* `BitConverter.GetBytes` allocates a fresh `byte[]`
* `.Reverse()` is LINQ, so big endian adds an iterator and an enumerator on top
* the result is accumulated in a growing `List<byte>` and then copied once more by `ToArray()`

Reading allocates less but is not free either: `Action<T, object>` boxes every value type,
and big endian allocates a temporary buffer per multi byte field.

At 100.000 messages per second the write path alone produces roughly 89 MB/s of garbage in
little endian and 153 MB/s in big endian, before any application code runs.

## Effect of the v4 bug fixes

The bounds checks added for the truncation fix cost one comparison per field. A re-run after the
fixes showed **identical allocation numbers** (344, 568, 288, 888, 1528, 288, 384 bytes) and timings
between 5 and 16 ns higher. With `--job short` the reported error was up to 110 ns on a 127 ns
measurement, so those deltas are inside the noise and no regression can be claimed or ruled out from
that run. Repeat with `--job medium` on an idle machine when an exact figure is needed.

## Targets for v4

1. Replace `FieldInfo.GetValue` with the compiled getter that already exists unused in `FastInvoke`.
2. Replace `Action<T, object>` with typed accessors so value types stop boxing in both directions.
3. Replace `BitConverter.GetBytes` plus `.Reverse()` with `BinaryPrimitives` writing into a
   caller supplied or pooled buffer.
4. Cache the `TypeCode` per field instead of calling `Type.GetTypeCode` per field per message.
