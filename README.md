# FastProtocolConverter
Converts raw bytes (e. g. from a hardware device) data to an instance of a class or vice versa

At a glance
Support for
* uint16|32|64, int16|32|64, decimal, single, double, byte, sbyte 
* Guid, DateTime, Boolean, Enums, IPAddress, strings
* int[], string[], bool[]


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



