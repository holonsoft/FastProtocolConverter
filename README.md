# FastProtocolConverter

Converts raw bytes, for example from a hardware device, into an instance of a class and back again.
You describe the frame with attributes on a POCO, and the converter does the rest.

Supports .NET 8, .NET 9 and .NET 10.

## Fast, and measurably so

Version 4.0 is a rewrite of both conversion paths. A 38 byte frame containing every supported
primitive, on the same machine, same benchmark code on both sides:

| | 3.6.1 | 4.0 | faster by |
|---|---:|---:|---:|
| Read, little endian | 118.09 ns / 344 B | **58.65 ns / 56 B** | 50% |
| Read, big endian | 146.21 ns / 568 B | **58.19 ns / 56 B** | 60% |
| Read into a reused instance | 115.58 ns / 288 B | **28.92 ns / 0 B** | 75% |
| Write, little endian | 245.33 ns / 888 B | **55.76 ns / 64 B** | 77% |
| Write, big endian | 376.86 ns / 1528 B | **56.06 ns / 64 B** | 85% |
| Read a frame with a string | 73.96 ns / 288 B | **28.12 ns / 160 B** | 62% |
| Write a frame with a string | 166.05 ns / 384 B | **44.08 ns / 64 B** | 74% |

**Byte order is free.** Big endian used to cost up to 55% more than little endian in both
directions. It now costs nothing, which matters if your devices speak network byte order.

**Nothing is allocated per message.** Read into an instance you keep and write into a buffer you
own, and the converter allocates **zero bytes per frame**:

```c#
    var frame = new byte[converter.GetByteCount(reading)];
    var reusable = new SensorReading();

    while (running)
    {
        var count = socket.Receive(receiveBuffer);

        converter.ConvertFromByteArray(receiveBuffer.AsSpan(0, count), reusable);   // 0 B
        Process(reusable);

        converter.TryConvertToByteArray(reply, frame, out var written);             // 0 B
        socket.Send(frame, written, SocketFlags.None);
    }
```

At 100.000 messages per second the 3.6.1 write path produced roughly 89 MB/s of garbage in little
endian and 153 MB/s in big endian, before any of your own code ran. Version 4.0 produces 6.4 MB/s,
or none at all into a reused buffer. In a long running process that is the difference between
regular gen0 collections and effectively none.

Every number above comes from a back to back A/B run of the same tree, with the rows the change
could not reach kept in the table as a control.
[`baseline-before-v4.md`](holonsoft.FastProtocolConverter.Performance/baseline-before-v4.md)
records how each one was measured and which approaches were tried and rejected. And the byte exact
golden vectors in the test suite were written once, before the rewrite, and never regenerated: the
wire format did not move a single byte.

## At a glance

* `uint16|32|64`, `int16|32|64`, `decimal`, `single`, `double`, `byte`, `sbyte`
* `Guid`, `DateTime`, `bool`, enums, strings
* Little and big endian, per protocol
* Fixed position frames and sequence based frames with variable length strings
* Fields **and properties**, including `init` only and private setters
* Read from a `byte[]` or a `ReadOnlySpan<byte>`, write to a `byte[]`, a `Span<byte>` you own, or an
  `IBufferWriter<byte>`
* Range limits per field, with a handler that decides what to do about a violation
* A prepared converter is safe to share between threads

### Wire format of the scalar types

| Type | Bytes | Notes |
|---|---|---|
| `byte`, `sbyte`, `bool` | 1 | a single byte has no byte order, `UseBigEndian` does not apply. `bool` is written as 0 or 1 and read as `value == 1` |
| `short`, `ushort` | 2 | byte order follows `UseBigEndian` |
| `int`, `uint`, `float` | 4 | byte order follows `UseBigEndian` |
| `long`, `ulong`, `double` | 8 | byte order follows `UseBigEndian` |
| `decimal` | 16 | the four component integers (low, mid, high, flags) in exactly that order. `UseBigEndian` swaps the bytes **inside** each component, the order of the components never changes. The scale is preserved, so `1.000` does not become `1` |
| `Guid` | 16 | `Guid.ToByteArray()`, all 16 bytes reversed when `UseBigEndian` is set. Note that this is a full reversal and deliberately **not** RFC 4122 byte order |
| `DateTime` | 4 or 8 | unix timestamp, see `ProtocolDateTimeFieldAttribute` |

Range limits in `ProtocolFieldRangeAttribute` are written as strings and are parsed with the
**invariant culture** by default. `MinValue = "1.100"` therefore means one point one on every machine,
regardless of the operating system locale.

If you prefer to write the limits in a different style, name the culture in the protocol definition:

```c#
    [ProtocolSetupArgument(RangeCulture = "de-DE")]
    public class MyProtocol
    {
        [ProtocolField(StartPos = 0)]
        [ProtocolFieldRange(MinValue = "1,1", MaxValue = "2,2")]
        public float Temperature;
    }
```

This is safe because the culture is declared in the source and travels with it. The culture of the
machine is never consulted. An unknown culture name fails at `Prepare()` rather than falling back to
something else, so a typo cannot silently change what a limit means.

### Reading without copying the bytes first

Raw bytes rarely arrive in a `byte[]` that starts exactly at the frame. They arrive in a socket
receive buffer, a rented buffer or a slice of something larger. The `ReadOnlySpan<byte>` overloads
read straight out of whatever holds them:

```c#
    // one receive buffer, several frames, no copy and no allocation per frame
    var bytesRead = socket.Receive(receiveBuffer);

    for (var offset = 0; offset + frameLength <= bytesRead; offset += frameLength)
    {
        converter.ConvertFromByteArray(receiveBuffer.AsSpan(offset, frameLength), reuseMe);
        Handle(reuseMe);
    }
```

Combined with a reused instance this is the cheapest read the library offers: nothing is allocated
per message except what the POCO itself holds, for example its strings.

### Thread safety

A prepared converter is safe to share between threads. `Prepare()` is not: call it once, on one
thread, before the converter is handed out.

Both conversion directions keep every piece of per message state on the stack. Reading into a
**reused instance** is of course only safe if that instance belongs to the calling thread, a POCO
handed to two threads at once is a data race like any other object.

### Fields or properties, as you prefer

A protocol member can be a field or a property. The same attributes work on both, and the produced
bytes are identical, so you can move a POCO from one to the other without touching the wire format:

```c#
    public class SensorReading
    {
        [ProtocolField(StartPos = 0)]
        public int DeviceId { get; set; }

        [ProtocolField(StartPos = 4)]
        public float Temperature { get; set; }
    }
```

A private setter or an `init` only setter is fine, so a POCO can stay immutable from the outside and
still be filled from a frame:

```c#
    [ProtocolField(StartPos = 0)]
    public int DeviceId { get; init; }
```

Properties cost nothing. The accessors are compiled expressions in both cases, and the JIT inlines
an auto property accessor into the same code a field access produces. Measured on the same 38 byte
frame: reading 58.91 ns as fields against 60.76 ns as properties, writing 57.02 ns against 56.83 ns,
with identical allocation.

A property that carries a protocol attribute but has **no** setter is rejected at `Prepare()`,
because reading a frame would have to assign it. Computed properties, indexers and static properties
without the attribute are simply ignored, so they can sit next to the protocol members undisturbed.

### Writing into a buffer you already have

Three ways to write, all producing the same bytes because they share one writer:

```c#
    // 1. the simple one, allocates the result array
    byte[] frame = converter.ConvertToByteArray(reading);

    // 2. into a buffer you own, allocates nothing
    Span<byte> buffer = stackalloc byte[converter.GetByteCount(reading)];
    if (converter.TryConvertToByteArray(reading, buffer, out var written)) { ... }

    // 3. into a pipeline, allocates nothing
    converter.ConvertToByteArray(reading, pipeWriter);
```

`GetByteCount` is exact, not an upper bound. For a protocol whose length does not depend on the
values, which is every protocol without a variable length string, it is a field read and costs
nothing.

`TryConvertToByteArray` returns `false` rather than throwing when the destination is too small, so a
loop may probe with a buffer and grow it. Nothing meaningful is written in that case.

Writing into a buffer you own costs 56.15 ns for the 38 byte frame and allocates nothing. Through an
`IBufferWriter` it is 57.17 ns, also nothing: a pipeline pays about a nanosecond for the `GetSpan`
and `Advance` pair.

### Error handling when reading

A byte array that is too short for the protocol is a protocol condition, not a programmer error, and
is always reported as `ProtocolConverterException`. This holds for both protocol styles and for every
field, so a truncated frame can never surface as a raw `ArgumentException` out of `Array.Copy` or
`BitConverter`. The converter knows the minimum length of a protocol after `Prepare()` and checks it
before the first field is read.

It's free, opensource and licensed under <a href="https://opensource.org/licenses/Apache-2.0">APACHE 2.0</a> (an OSI approved license).

You simply define a POCO and add some attributes to the fields. The following example illustrates this 

```c#

	var converter = new ProtocolConverter<DumbPoco>(_logger) as IProtocolConverter<DumbPoco>;
```

This creates a converter and cast it to the appropriate interface.
After that you call `Prepare()`, this will inspect the attributes and feed the internal logic.

The interface itself it pretty easy and straight forward to use. We use Microsoft.Logging.Abstractions as a generic logger interface. All important logging frameworks provide an adapter for it, so please feel free to use a logger of your choice.


```c#

	/// <summary>
	/// Provides the public part of the converter
	/// </summary>
	/// <typeparam name="T">T is the source/destination POCO type, restrictions: must be a class and new()</typeparam>
	public interface IProtocolConverter<T>
		where T : class, new()
    {
		/// <summary>
		/// Prepare the parser / converter, analyse the POCO vioa reflection
		/// This can only be called once per converter
		/// </summary>
		void Prepare();

		/// <summary>
		/// Use a source array to fill the content of POCO
		/// Creates every time a new instance of POCO
		/// </summary>
		/// <param name="data">byte array with raw values</param>
		/// <returns>An instance of POCO</returns>
		T ConvertFromByteArray(byte[] data);


		/// <summary>
		/// Use a source array to fill the content of POCO
		/// </summary>
		/// <param name="data">byte array with raw values</param>
		/// <param name="instance">an outside created, reusable instance of a POCO</param>
		void ConvertFromByteArray(byte[] data, T instance);

		/// <summary>
		/// Use a source span to fill the content of POCO
		/// Creates every time a new instance of POCO
		/// </summary>
		/// <param name="data">span with raw values</param>
		/// <returns>An instance of POCO</returns>
		T ConvertFromByteArray(ReadOnlySpan<byte> data);

		/// <summary>
		/// Use a source span to fill the content of POCO
		/// </summary>
		/// <param name="data">span with raw values</param>
		/// <param name="instance">an outside created, reusable instance of a POCO</param>
		void ConvertFromByteArray(ReadOnlySpan<byte> data, T instance);

		/// <summary>
		/// Converts a POCO content to a byte array
		/// </summary>
		/// <param name="data">POCO instance</param>
		/// <returns>the byte array</returns>
		byte[] ConvertToByteArray(T data);

		/// <summary>
		/// An event that will be fired in case of range violations of a field
		/// </summary>
		event OnRangeViolationDelegate OnRangeViolation;

		/// <summary>
		/// Will be triggered if a byte value in source data is marked as "BITS"
		/// </summary>
		event OnSplitBitValuesDelegate<T> OnSplitBitValues;

		/// <summary>
		/// Will be triggered if a byte value is marked as "BITS"
		/// </summary>
		event OnConsolidateBitValuesDelegate<T> OnConsolidateBitValues;
	}
	
```

As you can see, several events are provided. 

1.  `OnRangeViolation` occurs if a numercic field has defined an range and this range is violated. You can decide on the fly how to handle the value. Supported are 

```c#

/// <summary>
	/// Define the behaviour when a range violation occurs 
	/// </summary>
	[Flags]
	public enum ConverterRangeViolationBehaviour
	{
		None = 0x00,

		/// <summary>
		/// Ignore the range violation and continue
		/// </summary>
		IgnoreAndContinue = 0x01,

		/// <summary>
		/// Set to minimum value of type and continue
		/// </summary>
		SetToMinValue = IgnoreAndContinue << 1,

		/// <summary>
		/// Set to maximum value of type and continue
		/// </summary>
		SetToMaxValue = IgnoreAndContinue << 2,

		/// <summary>
		/// Set to default value of type and continue
		/// </summary>
		SetToDefaultValue = IgnoreAndContinue << 3,

		/// <summary>
		/// Stop processing
		/// </summary>
		ThrowException = IgnoreAndContinue << 4,
	}

``` 

2. `OnSplitBitValues` occurs if a field is marked as 'BITS'. You can assign the 8 bits of this special byte to appropriate fields in your POCO

3. `OnConsolidateBitValues` occurs if a field is marked as 'BITS'. You can assign the 8 bits of this special byte from appropriate fields in your POCO

Please note that only one field is marked as "BITS" and all other data holder fields should be marked as to be skipped: `[ProtocolField(IgnoreField = true)]`

With `[ProtocolSetupArgument(OffsetInByteArray = <number_of_bytes>)]` the converter skips n bytes at the beginning. This is helpful if you do not want to assign this bytes to some fields in your POCO.

> **Note:** the offset applies to reading only. `ConvertToByteArray` writes the fields of the POCO and
> nothing else, so the output is shorter than the offset protocol by exactly those n bytes and cannot
> be fed back into `ConvertFromByteArray` without prepending them yourself.
>
> This is intentional. The skipped bytes belong to a frame header that the POCO does not describe and
> whose content only the calling application knows, so the converter must not invent them. Writing n
> zero bytes would produce a frame that looks well formed but carries a meaningless header.
>
> If what you actually want are **reserved bytes that are written as well as read**, model them as a
> field with `[ProtocolBytePadding(Padding = n)]`. That mechanism is symmetric and round trips.

```c#

	/// <summary>
    /// A simple protocol definition
    /// Does not contain strings, therefore we can provide a fix start position for every field
    /// </summary>
    [ProtocolSetupArgument(OffsetInByteArray = 4)]
    public sealed class DumbPoco
    {
        [ProtocolField(StartPos = 2)]
        public int IntField;

        [ProtocolField(StartPos = 0)]
        public short ShortField;

        [ProtocolField(StartPos = 6)]
        public byte ByteField;

        [ProtocolField(StartPos = 7, TypeInByteArray = DestinationType.Int32)]
        public MyImportantEnum EnumField1;

        [ProtocolField(StartPos = 11, TypeInByteArray = DestinationType.Int16)]
        public MyImportantEnum EnumField2;

        [ProtocolField(StartPos = 13, TypeInByteArray = DestinationType.Byte)]
        public MyImportantEnum EnumField3;

        [ProtocolField(StartPos = 14)]
        public float FloatField;

        [ProtocolField(StartPos = 18)]
        public double DoubleField;

        [ProtocolField(StartPos = 26)]
        public uint UIntField;

        [ProtocolField(StartPos = 30)]
        public ushort UShortField;

        [ProtocolField(IgnoreField = true)]
        public string MyUnimportantField;
    }
	
```


Of course you can define more complex protocols like

```c#

	public class ComplexProtocol
    {
        [ProtocolField(StartPos = 0, SequenceNo = 1)]
        public int LengthOfStr1;

        [ProtocolField(StartPos = -1, SequenceNo = 2)]
        [ProtocolStringField(LengthFieldName = "LengthOfStr1")]
        public string String1;

        //--------
        // It's allowed to change position in class as long as unique sequence number is provided
        // for string fields counts: length field *must* have a lower sequence number than string field

        [ProtocolField(StartPos = -1, SequenceNo = 4)]
        [ProtocolStringField(LengthFieldName = "LengthOfStr2")]
        public string String2;

        // Please note that the length that was read depends on encoding. If source is ASCII the length is half of .net string (UTF16/Unicode)
        [ProtocolField(StartPos = -1, SequenceNo = 3)]
        public int LengthOfStr2;

        [ProtocolField(StartPos = -1, SequenceNo = 6)]
        public short ShortField;

        [ProtocolField(StartPos = -1, SequenceNo = 5)]
        public int IntField;

        [ProtocolField(StartPos = -1, SequenceNo = 7)]
        public byte ByteField;

        [ProtocolField(StartPos = -1, SequenceNo = 8, TypeInByteArray = DestinationType.Int32)]
        public MyImportantEnum EnumField1;

        [ProtocolField(StartPos = -1, SequenceNo = 9, TypeInByteArray = DestinationType.Int16)]
        public MyImportantEnum EnumField2;

        [ProtocolField(StartPos = -1, SequenceNo = 10, TypeInByteArray = DestinationType.Byte)]
        public MyImportantEnum EnumField3;
        
        [ProtocolField(StartPos = -1, SequenceNo = 11)]
        public float FloatField;

        [ProtocolField(StartPos = -1, SequenceNo = 12)]
        public double DoubleField;

        [ProtocolField(IgnoreField = true)]
        public string MyUnimportantField;

        [ProtocolField(StartPos = -1, SequenceNo = 13)]
        public uint UIntField;

        [ProtocolField(StartPos = -1, SequenceNo = 14)]
        public ushort UShortField;

		// define only ONE field (1 bye in source byte array) to be parsed and call handler for splitting it
		[ProtocolField(StartPos = -1, SequenceNo = 15, TypeInByteArray = DestinationType.Bits)]
		public bool Bit0;

		[ProtocolField(IgnoreField = true)]
		public bool Bit2;

		[ProtocolField(IgnoreField = true)]
		public bool Bit4;
	}
	
```

There are two kinds of protocol:

1. simple, you just define a `StartPos` (positon in byte array) per field and that's it.
1. complex, in this case you could not define `StartPos` but have to use SequenceNo instead. 



Several type mappings are supported, e. g. you map your `enum` to 16bit in byte array (and vice versa)

	[ProtocolField(StartPos = 11, TypeInByteArray = DestinationType.Int16)]
    public MyImportantEnum EnumField2;


Strings have a special treatment, you can convert from ASCI to UNICODE and vice versa. Please note, that length field for a string must have a lower sequence number.

	[ProtocolField(StartPos = -1, SequenceNo = 4)]
	[ProtocolStringField(LengthFieldName = "LengthOfStr2")]
	public string String2;

This defines a string field, in the byte array the `LengthOfStr2` has the length of it.

A more complex example for handling string is here

```c#

	public class ComplexProtocolFixedStringLength
    {
		[ProtocolField(StartPos = 0, SequenceNo = 1)]
		public int LengthOfStr1;

		[ProtocolField(StartPos = -1, SequenceNo = 2)]
		public int LengthOfStr2;

		// will be filled to 10 chars (ascii)
		[ProtocolField(StartPos = -1, SequenceNo = 3)]
		[ProtocolStringField(LengthFieldName = "LengthOfStr1", FillupCharWhenShorter = 'Z', StringMaxLengthInByteArray = 10, Encoder = SupportedEncoder.ASCIIEncoder)]
		public string StrField1;

		// will be shortened to 10 chars (ascii)
		[ProtocolField(StartPos = -1, SequenceNo = 4)]
		[ProtocolStringField(LengthFieldName = "LengthOfStr2", StringMaxLengthInByteArray = 10, Encoder = SupportedEncoder.ASCIIEncoder)]
		public string StrField2;
    }

```


Endianess can be achied by decoration the class

```c#

	[ProtocolSetupArgument(UseBigEndian = false)] 
    public class PocoNoBigEndianessFlag


	[ProtocolSetupArgument(UseBigEndian = true)] 
    public class PocoWithBigEndianessFlag

```


As mentioned before ranges for several field types are supported

```c#

public class PocoWithRanges
    {
        [ProtocolField(StartPos = 0)]
        [ProtocolFieldRange(MinValue = "-2200", MaxValue = "-1200", DefaultValue = "-2000")]
        public int IntField;

        [ProtocolField(StartPos = 4)]
        [ProtocolFieldRange(MinValue = "1100", MaxValue = "2200")]
        public uint UIntField;

        [ProtocolField(StartPos = 8)]
        [ProtocolFieldRange(MinValue = "1100", MaxValue = "2200")]
        public short ShortField;

        [ProtocolField(StartPos = 10)]
        [ProtocolFieldRange(MinValue = "1100", MaxValue = "2200")]
        public ushort UShortField;

        [ProtocolField(StartPos = 12)]
        [ProtocolFieldRange(MinValue = "1.100", MaxValue = "2.200")]
        public float FloatField;

        [ProtocolField(StartPos = 16)]
        [ProtocolFieldRange(MinValue = "3.1415", MaxValue = "6.282")]
        public double DoubleField;

    }


```


## Upgrading from 3.x to 4.0

The wire format did not change. A 3.x frame is a 4.0 frame, which is pinned by byte exact vectors
in the test suite. Four things behave differently.

**1. Range limits are parsed with the invariant culture.**
This is the one that can change what your program does without any compiler error. Before, the
limits in `ProtocolFieldRangeAttribute` were parsed with the culture of the machine, so
`MinValue = "-100.5"` was read as **-1005** on a German or French system and the range guard was
wrong by a factor of ten, silently and only on some machines. Limits are now parsed with the
invariant culture. If you actually wrote your limits in a local style, declare it:
`[ProtocolSetupArgument(RangeCulture = "de-DE")]`. An unknown culture name now fails at `Prepare()`
instead of falling back to something else.

**2. A truncated frame throws `ProtocolConverterException`.**
Before, a byte array that was too short surfaced as whatever the BCL happened to throw, an
`ArgumentException` out of `Array.Copy` or `BitConverter`, and for sequence protocols there was no
length check at all. If you catch `ArgumentException` around a conversion, catch
`ProtocolConverterException` instead.

**3. `IProtocolConverter<T>` has two new members**, the `ReadOnlySpan<byte>` read overloads. This
only affects you if you implement or mock the interface yourself.

**4. `Guid` respects `UseBigEndian` when writing.**
The reader always reversed the bytes for a big endian protocol, the writer ignored the flag, so a
big endian Guid did not survive a round trip through this library. Both sides agree now. If you
persisted big endian Guid frames written by 3.x, they were written little endian and will now read
differently. Little endian protocols are unaffected.

**5. A fixed length string field is now really its declared length.**
If the fill character costs more than one byte, which is any character under the unicode encoder,
and the declared length is not a multiple of that width, the padding used to overshoot. An eleven
byte unicode field was written as twelve, so every field behind it moved one byte along and the
frame could not be read back by this very converter. Frames of such a protocol written by 3.x are
malformed and will not match what 4.0 writes. Protocols whose fill character is one byte wide, or
whose declared length divides by the fill width, are unaffected, which is almost all of them.

**6. A range violation handler receives a `MemberInfo`, not a `FieldInfo`.**
A protocol member can be a property now, and `FieldInfo` cannot describe one. The fix is one word:

```c#
    // 3.x
    void OnRangeViolation(FieldInfo field, out ConverterRangeViolationBehaviour behaviour)

    // 4.0
    void OnRangeViolation(MemberInfo member, out ConverterRangeViolationBehaviour behaviour)
```

A handler that only reads `member.Name` needs nothing else. One that read `field.FieldType` has to
ask the member, since `MemberInfo` carries no type of its own:

```c#
    var type = member is PropertyInfo p ? p.PropertyType : ((FieldInfo) member).FieldType;
```

### Fixed in 4.0

* **A prepared converter is now thread safe.** Both directions kept per message state on the shared
  field info, so two threads using one converter produced wrong values. Under a 20.000 iteration
  concurrent read, 323 results came back silently wrong. Everything per message lives on the stack
  now.
* **`decimal` and `sbyte` work.** They were documented as supported and threw
  `NotImplementedException`.
* **The minimum length check is real.** It reported 14 bytes for a 272 byte protocol, so lengths
  between 14 and 271 slipped past the guard.
* **A fixed length string can no longer overflow its field**, see point 5 above.

### And it is quite a bit faster

See [Fast, and measurably so](#fast-and-measurably-so) at the top: between 50% and 85% depending on
the direction and byte order, and no allocation per message at all when you reuse the instance and
the buffer.

We hope this software is helpful for your project. Do not hesiate to contact us and ask for new features or report a bug.
