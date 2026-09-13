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

## Phase 2, step 2 and 3: cached TypeCode and typed accessors

Cached TypeCode (A/B, same tree with and without):

  Read LE -7.0%, Read BE -6.0%, Read reused -5.7%, Write LE -12.5%,
  Write BE -3.6%, Read string -5.0%, Write string -4.1%

Typed accessors, which remove the box on every value type in both directions:

| Method | boxing | typed | time | alloc |
|---|---:|---:|---:|---|
| Read  LE      | 118.86 ns |  94.78 ns | -20.3% | 344 -> 104 B |
| Read  BE      | 148.85 ns | 115.31 ns | -22.5% | 568 -> 328 B |
| Read  reused  | 114.23 ns |  90.71 ns | -20.6% | 288 ->  48 B |
| Write LE      | 209.75 ns | 204.25 ns |  -2.6% | 888 -> 648 B |
| Write BE      | 333.25 ns | 336.31 ns |  +0.9% | 1528 -> 1288 B |
| Read  string  |  71.39 ns |  73.42 ns |  +2.8% | 288 -> 264 B |
| Write string  | 182.10 ns | 155.78 ns | -14.5% | 472 -> 448 B |

Reading into a reused instance allocates 48 bytes instead of 288.

The write path barely moves in time because it is no longer dominated by the box: what is left is
BitConverter.GetBytes allocating a byte[] per field, LINQ Reverse for big endian, and the growing
List<byte>. That is exactly what Phase 3 removes.

## Cumulative against the shipped 3.6.1 code (bcea658)

| Method | before | now | time | alloc |
|---|---:|---:|---:|---|
| Read  LE      | 118.09 ns |  94.78 ns | -19.7% | 344 -> 104 B |
| Read  reused  | 115.58 ns |  90.71 ns | -21.5% | 288 ->  48 B |
| Write LE      | 245.33 ns | 204.25 ns | -16.7% | 888 -> 648 B |

## Phase 3: allocation free conversion

Removed, all of them once per field per message:

* `BitConverter.GetBytes` allocated a byte[] for every multi byte field written
* LINQ `Reverse` allocated an iterator on top of that for every big endian field, which is why big
  endian used to cost almost twice what little endian did
* the result `List<byte>` grew from nothing, it is sized from the protocol now
* reading big endian built a scratch byte[] per field to feed BitConverter, `BinaryPrimitives`
  needs none
* a Guid allocated a byte[16] on read, plus a LINQ reverse and another array for big endian
* a string allocated a byte[] on write (`Encoding.GetBytes`), another for the fill character and a
  string for `char.ToString()`, and a byte[] on read to copy into before decoding

The encoder and the encoded fill bytes are resolved once while preparing. The fill bytes are produced
with the real encoder on purpose: `(byte) fillChar` would differ from the ASCII fallback for any
character above 0x7F and would silently change the wire format.

## Result, against the shipped 3.6.1 code (bcea658)

| Method | before | now | time | alloc |
|---|---:|---:|---:|---|
| Read  LE      | 118.09 ns |  91.79 ns | -22.3% |  344 -> 104 B |
| Read  BE      | 146.21 ns |  93.33 ns | -36.2% |  568 -> 104 B |
| Read  reused  | 115.58 ns |  94.81 ns | -18.0% |  288 ->  48 B |
| Write LE      | 245.33 ns | 134.93 ns | -45.0% |  888 -> 208 B |
| Write BE      | 376.86 ns | 134.06 ns | -64.4% | 1528 -> 208 B |
| Read  string  |  73.96 ns |  64.69 ns | -12.5% |  288 -> 208 B |
| Write string  | 166.05 ns | 144.35 ns | -13.1% |  384 -> 296 B |

Byte order is free now, in both directions. What is left is structural: the result array itself, the
List and its backing array on the write side, and the POCO on the read side.

## Phase 4, step 1: reading straight out of a ReadOnlySpan

The converter could only be handed a `byte[]`. Bytes that arrive anywhere else, a socket receive
buffer, a rented buffer, a slice of a larger frame, had to be copied into a fresh array first, and
that copy is bigger than everything the converter allocates per message.

`ConvertFromByteArray` now has `ReadOnlySpan<byte>` overloads in both shapes. The span version is
the implementation, the array versions keep their null check and delegate to it.

Reading a 38 byte frame that sits at a non zero offset inside a larger receive buffer:

| | time | alloc |
|---|---:|---:|
| copy into an array first | 132.05 ns | 168 B |
| span, no copy            |  75.13 ns | 104 B |
| span, reused instance    |  77.98 ns |  48 B |

Roughly 50 ns of that gap turned out not to be the copy at all but the guard clause the array
overload runs and the span overload does not, which is what the next step is about.

## Phase 4, step 2: the guard clauses

Both conversion directions opened with `holonsoft.FluentConditions`:

```csharp
IsPrepared.Requires("Prepare()").IsTrue();
data.Requires(nameof(data)).IsNotNull();
```

That package is **published as a non optimized build**. Its assembly carries
`DebuggableAttribute` with `IsJITOptimizerDisabled`, so the JIT neither optimizes nor inlines
anything inside it, in every process that consumes it. BenchmarkDotNet refuses to benchmark
against such a reference at all, which is how it was found.

A fluent validator chain is the wrong tool for a per message guard clause regardless of how it was
built, so both directions now use a plain compare with the throw in a separate non inlined method.
The exception types are unchanged (`ArgumentNullException`, `ArgumentOutOfRangeException`), so
code that catches them keeps working, and the original message text is kept. `Prepare()` still
uses the fluent version, it runs once per converter.

A/B of the same tree with and without the change, back to back:

| Method | fluent guards | plain guards | delta |
|---|---:|---:|---:|
| Read  from buffer, copy first | 131.69 ns |  72.42 ns | **-45.0%** |
| Read  from buffer, span       |  74.09 ns |  69.63 ns |  -6.0% |
| Read  from buffer, span reuse |  75.58 ns |  60.55 ns | -19.9% |
| Read  LE                      | 133.07 ns |  68.17 ns | **-48.8%** |
| Read  BE                      | 125.90 ns |  67.37 ns | **-46.5%** |
| Read  reused                  | 125.75 ns |  59.59 ns | **-52.6%** |
| Write LE                      | 129.14 ns | 110.74 ns | -14.2% |
| Write BE                      | 131.19 ns | 112.02 ns | -14.6% |
| Read  string                  |  66.76 ns |  37.70 ns | **-43.5%** |
| Write string                  | 138.26 ns | 119.97 ns | -13.2% |

Allocation is unchanged in every row, the validator was a struct.

What the A/B shows is that every row improved and that reading gained far more than writing. What
it does not show is why. Both directions removed exactly the same two guards, one on a reference
type and one on `bool`, yet reading saved about 65 ns and writing about 18 ns. So the saving is
not simply the cost of the two calls, and no measurement here isolates the rest of it. The likely
remainder is an inlining cascade, an opaque call at the top of a method blocks the JIT from
inlining what follows, and the read path had more to gain from that than the write path. That is
a hypothesis, not a result.

The reads are now faster than the write path for the first time in this library's history.

## Cumulative against the shipped 3.6.1 code (bcea658)

| Method | before | now | time | alloc |
|---|---:|---:|---:|---|
| Read  LE      | 118.09 ns |  68.17 ns | **-42.3%** |  344 -> 104 B |
| Read  BE      | 146.21 ns |  67.37 ns | **-53.9%** |  568 -> 104 B |
| Read  reused  | 115.58 ns |  59.59 ns | **-48.4%** |  288 ->  48 B |
| Write LE      | 245.33 ns | 110.74 ns | **-54.9%** |  888 -> 208 B |
| Write BE      | 376.86 ns | 112.02 ns | **-70.3%** | 1528 -> 208 B |
| Read  string  |  73.96 ns |  37.70 ns | **-49.0%** |  288 -> 208 B |
| Write string  | 166.05 ns | 119.97 ns | **-27.8%** |  384 -> 296 B |

Reading a frame out of a receive buffer without copying it first is 132.05 ns -> 72.42 ns against
what a caller had to write before, and 60.55 ns into a reused instance.

## Phase 4, step 3: the DateTime path

The same non optimized package problem applied to `holonsoft.FluentDateTime`, which sat in both
conversion directions and which **no benchmark covered**: `BenchmarkPoco` has no DateTime field, so
nothing in the suite ever ran that code. Guid and decimal were unmeasured for the same reason.
A `BenchmarkAdvancedPoco` with two timestamps, a Guid and a decimal was added first, so the change
could be measured instead of assumed.

The two uses turned out to be very different:

* reading used `DateTimeExtensions.UnixEpoch`, a `static readonly DateTime`. Reading a static field
  costs nothing even in an unoptimized assembly, there is no call to inline.
* writing used `dtf.ToUnixTimeSeconds()`, a real extension method, so a non inlinable call into the
  unoptimized assembly for every DateTime field of every message.

Both were replaced by their in box equivalents, `DateTime.UnixEpoch` and
`new DateTimeOffset(dtf).ToUnixTimeSeconds()`, which is literally what the extension method did.
The package reference is gone, so `holonsoft.FluentDateTime` no longer reaches a consumer of this
library at all.

`DateTime.UnixEpoch` is `1970-01-01T00:00:00` with `DateTimeKind.Utc`, byte for byte and kind for
kind the same value FluentDateTime declared, so nothing about the decoded value changes.

A/B of the same tree with and without the change, back to back:

| Method | FluentDateTime | in box | delta |
|---|---:|---:|---:|
| Write DateTime/Guid/decimal LE | 66.70 ns | 60.04 ns | **-10.0%** |
| Write DateTime/Guid/decimal BE | 65.51 ns | 61.27 ns |  -6.5% |
| Read  DateTime/Guid/decimal LE | 58.18 ns | 59.42 ns |  +2.1% |
| Read  DateTime/Guid/decimal BE | 58.66 ns | 58.08 ns |  -1.0% |

The other **ten rows are the control**: the change cannot reach them and they all stay within 3%,
drifting slightly upward in the second run. The two write rows moved outside their error bars and
against that drift, the two read rows did not move, which is what the code predicts. About 3 ns
per DateTime field written.

### Why the golden vectors cannot cover this

The `Kind` of a decoded DateTime is not in the bytes. A decoder that returns the right instant with
the wrong `Kind` writes the very same frame back out, so a byte exact vector stays green while
every consumer that hands the value to a `DateTimeOffset` silently reinterprets it as local time.
`ProtocolDateTimeField` declares the Kind, which makes it part of the protocol contract, so
`TestDateTimeConversion` asserts it directly, along with round trips in both widths, both byte
orders, and a timestamp before the epoch.

## Phase 4, step 4: writing into a buffer the caller owns

Three changes, and the one that mattered most was not the one this step set out to make.

**A caller supplied buffer.** `TryConvertToByteArray(T, Span<byte>, out int)` writes into a buffer
the caller owns, and `GetByteCount(T)` says exactly how big it has to be. The Try pattern was chosen
over `IBufferWriter<byte>`: it is what the BCL uses for this, and `IBufferWriter` earns its
indirection only when the sink spans several segments. Both entry points share one `ByteWriter`
cursor, so there is exactly one copy of the field writing logic and no way for the two overloads to
drift apart.

**The result array is allocated once, at its exact size.** The size is known before a byte is
written, so `ConvertToByteArray` allocates the result and writes straight into it. It used to be a
`List<byte>`, which cost the list object, its backing array, and a `ToArray` copy on top.

A pooled buffer was tried first and **rejected on measurement**: it cut allocation by 46% but cost
between 3% and 23% in time, because renting and returning costs more than the single allocation it
saves on a frame this small. It was then kept for a while as a fallback for the case that the
calculated size and the writer ever disagree, and dropped again after instrumenting it proved that
no test in the suite ever reaches it. A fallback that never runs is a fallback that is never
verified, and it silently re ran the whole write, including the part that assigns the length fields
back onto the caller's POCO. The size is now checked on every call and a mismatch throws, because it
could only ever mean a bug in this library, and handing back a half written or zero padded frame
that still looks well formed is the worst failure mode available here.

**`SortedList` was boxing an enumerator per message, in both directions.**
`SortedList<K,V>.GetEnumerator()` returns `IEnumerator<KeyValuePair<K,V>>` rather than its own
struct enumerator, so every `foreach` over the field list put a boxed enumerator on the heap, once
per converted message, on the read side as well as the write side. That is 48 bytes per conversion
and it has been there far longer than this renovation. The field entries are now also kept as plain
arrays, built once in `Prepare()`.

A/B of the same tree with and without all three, back to back:

| Method | before | after | time | alloc |
|---|---:|---:|---:|---|
| Read  from buffer, copy first  |  97.74 ns |  65.31 ns | -33% | 168 -> 120 B |
| Read  from buffer, span        |  64.72 ns |  33.29 ns | -49% | 104 ->  56 B |
| Read  from buffer, span reuse  |  57.51 ns |  28.97 ns | -50% |  48 -> **0 B** |
| Read  LE                       |  93.06 ns |  59.33 ns | -36% | 104 ->  56 B |
| Read  BE                       |  92.07 ns |  58.95 ns | -36% | 104 ->  56 B |
| Read  reused                   |  58.56 ns |  29.21 ns | -50% |  48 -> **0 B** |
| Write LE                       | 105.36 ns |  59.75 ns | -43% | 208 ->  64 B |
| Write BE                       | 107.72 ns |  59.85 ns | -44% | 208 ->  64 B |
| Read  DateTime/Guid/decimal LE |  57.32 ns |  37.06 ns | -35% | 112 ->  64 B |
| Read  DateTime/Guid/decimal BE |  58.22 ns |  38.50 ns | -34% | 112 ->  64 B |
| Write DateTime/Guid/decimal LE |  60.29 ns |  41.47 ns | -31% | 264 ->  72 B |
| Write DateTime/Guid/decimal BE |  59.72 ns |  44.29 ns | -26% | 264 ->  72 B |
| Write into a reused buffer     |         - |  58.64 ns |    - |     **0 B** |
| Read  string                   |  42.87 ns |  28.73 ns | -33% | 208 -> 160 B |
| Write string                   | 112.88 ns | 119.55 ns | **+5.9%** | 296 -> 152 B |

There is no control row this time, every path was touched. The string write is the one row that
moved the wrong way, and it is also the one row that still allocates a `List<byte>` scratch buffer
per call. Encoding straight into the destination is the obvious next step and should take that row
to 64 bytes as well.

**Reading into a reused instance and writing into a reused buffer now allocate nothing at all.**
That is the shape a signal processing loop wants: no garbage per message, at any rate.

## A note for the other holonsoft packages

`holonsoft.FluentConditions` 3.0.1, `holonsoft.FluentDateTime` 2.1.1 and `holonsoft.Utils` 1.10.1
are all published to NuGet as non optimized builds. Every one of them sets
`GeneratePackageOnBuild`, so the `.nupkg` contains whatever configuration was built last, and a
build from the IDE defaults to Debug. This library packs with an explicit `-c Release` in
`publish.yml`, so it is not affected itself.

## Targets for v4

1. Replace `FieldInfo.GetValue` with the compiled getter that already exists unused in `FastInvoke`.
2. Replace `Action<T, object>` with typed accessors so value types stop boxing in both directions.
3. Replace `BitConverter.GetBytes` plus `.Reverse()` with `BinaryPrimitives` writing into a
   caller supplied or pooled buffer.
4. Cache the `TypeCode` per field instead of calling `Type.GetTypeCode` per field per message.
