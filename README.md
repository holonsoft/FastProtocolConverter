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



Of copurse you can define more complex protocols like

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


TBD
