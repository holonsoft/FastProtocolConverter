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

## Effect of the v4 correctness work

Measured as an A/B against a git worktree at `bcea658`, same benchmark code on both sides,
`--job medium`. Two rounds were run in opposite order to separate a real cost from machine drift.

**Allocations are byte identical on both sides in every row.** The hardening added no garbage.

Timing, round 2 (the clean one, after `EffectiveFieldSize` was cached):

| Method | old | new | delta |
|---|---:|---:|---:|
| Read  LE      | 118.09 ns | 125.74 ns | +6.5% |
| Read  BE      | 146.21 ns | 146.90 ns | +0.5% |
| Read  reused  | 115.58 ns | 121.92 ns | +5.5% |
| Write LE      | 245.33 ns | 247.98 ns | +1.1% |
| Write BE      | 376.86 ns | 371.78 ns | -1.3% |
| Read  string  |  73.96 ns |  76.98 ns | +4.1% |
| Write string  | 166.05 ns | 171.43 ns | +3.2% |

The **write path is the control**: the correctness work did not touch it for these POCOs, and it
measures at +1.1% and -1.3%, which is zero within the run to run spread of this machine (about 5%
between two runs of the same binary). That the control lands on zero is what makes the read side
credible: the read path costs roughly **4 to 6 percent**, which is the per field bounds check that
turns a truncated frame into a ProtocolConverterException instead of a raw BCL exception.

Round 1 showed much larger deltas (+10 to +28%). Two causes: `EffectiveFieldSize` was still a
computed property re evaluated per field per message, and the round suffered drift (the write
control read +11% there, and two rows flipped sign between rounds). Caching the size removed the
first, running both suites back to back removed the second.

Conclusion: the correctness work is not a speed up and was never meant to be. It costs a few percent
on reading, nothing on writing, and no extra allocation. The speed up comes from the targets below.

## After the thread safety fix

Removing the shared mutable state also removed a string format and a string parse per enum field
(`int.ToString()` plus `Enum.Parse(type, string)` became `Enum.ToObject(type, int)`).

| Method | old | now | delta | old B | now B |
|---|---:|---:|---:|---:|---:|
| Read  LE      | 118.1 ns | 104.0 ns | -11.9% |  344 |  344 |
| Read  BE      | 146.2 ns | 128.5 ns | -12.1% |  568 |  568 |
| Read  reused  | 115.6 ns | 105.2 ns |  -9.0% |  288 |  288 |
| Write LE      | 245.3 ns | 239.5 ns |  -2.4% |  888 |  888 |
| Write BE      | 376.9 ns | 350.6 ns |  -7.0% | 1528 | 1528 |
| Read  string  |  74.0 ns |  74.5 ns |  +0.8% |  288 |  288 |
| Write string  | 166.1 ns | 164.4 ns |  -1.0% |  384 |  472 |

The 88 extra bytes on the string write path are the price of correctness: the encoding buffer is now
local to the call instead of living on the shared field info. The buffer is only allocated for a
protocol that actually contains strings and is pre sized from the declared maximum length, so a
protocol without strings allocates exactly as much as before. Phase 3 removes it entirely.

## Phase 2, step 1: compiled accessors instead of reflection

`FastInvoke.BuildUntypedGetter` existed in the codebase but was never called, so the write path ran
`FieldInfo.GetValue` for every field of every message. Wiring it up removed the last eleven live
reflection calls in both directions.

Measured as an A/B of the same tree with and without the change, back to back on the same machine,
because comparing against a run from a different time of day is meaningless at this resolution:

| Method | FieldInfo.GetValue | compiled | delta |
|---|---:|---:|---:|
| Read  LE      | 122.76 ns | 126.12 ns |  +2.7% |
| Read  BE      | 152.01 ns | 150.79 ns |  -0.8% |
| Read  reused  | 123.48 ns | 125.83 ns |  +1.9% |
| Write LE      | 285.79 ns | 237.36 ns | **-16.9%** |
| Write BE      | 414.45 ns | 364.97 ns | **-11.9%** |
| Read  string  |  89.59 ns |  73.20 ns | **-18.3%** |
| Write string  | 191.11 ns | 180.51 ns |  -5.5% |

The first three rows are the control: nothing on the read path of a string free POCO was touched, and
they move by at most 2.7%, which is the noise floor of this machine. Every row that was touched moved
far outside its error bar.

Allocation is unchanged everywhere. `BuildUntypedGetter` returns `Func<T, object>`, so a value type
field still boxes on the way out exactly as `GetValue` did. Removing that box needs the typed
accessors, which is the next step.

## Targets for v4

1. Replace `FieldInfo.GetValue` with the compiled getter that already exists unused in `FastInvoke`.
2. Replace `Action<T, object>` with typed accessors so value types stop boxing in both directions.
3. Replace `BitConverter.GetBytes` plus `.Reverse()` with `BinaryPrimitives` writing into a
   caller supplied or pooled buffer.
4. Cache the `TypeCode` per field instead of calling `Type.GetTypeCode` per field per message.
