using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;

namespace holonsoft.FastProtocolConverter.dto
{
	/// <summary>
	/// contains infos per field in POCO
	/// </summary>
	public class ConverterFieldInfo<T>
	{
		/// <summary>
		/// Field attributes to define and control behaviour of converter
		/// </summary>
		public ProtocolFieldAttribute Attribute { get; }

		/// <summary>
		/// Additional attribute data if underlying field is a string
		/// </summary>
		public ProtocolStringFieldAttribute StrAttribute { get; }

		/// <summary>
		/// Additional attribute data for fields (STRING and ENUM are not supported)
		/// </summary>
		public ProtocolFieldRangeAttribute RangeAttribute { get; }

		/// <summary>
		/// Additional attribute for DATETIME field conversion
		/// </summary>
		public ProtocolDateTimeFieldAttribute DateTimeAttribute { get; }

		/// <summary>
		/// Additional attribute for BYTE fields to mark them as used for padding (with repeat option)
		/// </summary>
		public ProtocolBytePaddingAttribute BytePaddingAttribute { get; }

		/// <summary>
		/// shortcut flag to indicate whether field type is string
		/// </summary>
		public bool IsString { get; }

		/// <summary>
		/// shortcut flag to indicate whether field type is string
		/// </summary>
		public bool IsEnum { get; }

		/// <summary>
		/// Shortcut flag to indicate that the byte should be treated as several bit values
		/// </summary>
		public bool IsBitValue { get; }


		public bool IsDateTime { get; }


		public bool IsPaddingByte { get; }


		public bool IsByte { get; }


		public bool IsGuid { get; }

		/// <summary>
		/// Underlying field
		/// </summary>
		public FieldInfo FieldInfo { get; }

		public Action<T, object> Setter { get; }

		/// <summary>
		/// Compiled accessor for reading the field value. Replaces FieldInfo.GetValue, which was
		/// real reflection executed for every field of every message on the write path.
		/// </summary>
		public Func<T, object> Getter { get; }

		/// <summary>
		/// Strongly typed accessors for the field, so a value type never has to be boxed on the way
		/// in or out. The concrete delegate type is Action&lt;T, X&gt; resp. Func&lt;T, X&gt; where X
		/// is the field type, or the primitive the protocol carries for an enum field. The per type
		/// handlers know X and cast, which costs a type check instead of an allocation.
		/// Null for types without a typed path, for example string, where nothing is boxed anyway.
		/// </summary>
		public Delegate TypedSetter { get; }

		/// <inheritdoc cref="TypedSetter"/>
		public Delegate TypedGetter { get; }


		/// <summary>
		/// Assigns the field without boxing. TField must match the type the accessor was built for,
		/// which the per type handlers know.
		/// </summary>
		public void Set<TField>(T target, TField value)
			=> ((Action<T, TField>) TypedSetter)(target, value);


		/// <summary>
		/// Reads the field without boxing, counterpart of <see cref="Set{TField}"/>.
		/// </summary>
		public TField Get<TField>(T source)
			=> ((Func<T, TField>) TypedGetter)(source);


		/// <summary>
		/// Builds the typed pair for the field. For an enum the protocol carries a primitive, so the
		/// accessor is typed on that primitive and the compiled expression converts.
		/// </summary>
		private void BuildTypedAccessors(FieldInfo fieldInfo, out Delegate setter, out Delegate getter)
		{
			// An enum is always carried as int here: the read path assembles the raw value as an int
			// for every DestinationType it supports, and the compiled expression converts to the enum.
			var accessAs = IsEnum ? typeof(int) : fieldInfo.FieldType;

			if (IsString || accessAs == typeof(string))
			{
				setter = null;
				getter = null;
				return;
			}

			var setterBuilder = typeof(FastInvoke)
				.GetMethod(nameof(FastInvoke.BuildTypedFieldSetter))
				.MakeGenericMethod(typeof(T), accessAs);

			var getterBuilder = typeof(FastInvoke)
				.GetMethod(nameof(FastInvoke.BuildTypedFieldGetter))
				.MakeGenericMethod(typeof(T), accessAs);

			setter = (Delegate) setterBuilder.Invoke(null, [fieldInfo]);
			getter = (Delegate) getterBuilder.Invoke(null, [fieldInfo]);
		}

		/// <summary>
		/// Field size, depends on type and will be calculated only for primitives, string and enum is set to -1
		/// </summary>
		public int ExpectedFieldSize { get; }

		/// <summary>
		/// Number of bytes this field really occupies in the byte array.
		/// In contrast to <see cref="ExpectedFieldSize"/> this resolves padding bytes to their repeat
		/// count, fixed length strings to their reserved length and enums without an explicit
		/// destination type to the four bytes the converter actually writes for them.
		/// Returns -1 only for variable length strings, whose size is known at runtime.
		/// Use this for every bounds, minimum length and overlap calculation.
		/// </summary>
		/// <remarks>
		/// Resolved once during construction. It is invariant for the lifetime of the field info and
		/// is read for every field of every message, so it must not be recomputed on each access.
		/// </remarks>
		public int EffectiveFieldSize { get; private set; }


		private void ResolveEffectiveFieldSize()
		{
			if (IsPaddingByte)
			{
				EffectiveFieldSize = BytePaddingAttribute.Padding;
				return;
			}

			if (IsString)
			{
				EffectiveFieldSize = (StrAttribute != null) && StrAttribute.IsFixedLengthString
					? StrAttribute.StringMaxLengthInByteArray
					: -1;
				return;
			}

			// DestinationType.None and .Default are written as Int32, see WriteFieldValueToArray
			if (IsEnum)
			{
				EffectiveFieldSize = ExpectedFieldSize > 0 ? ExpectedFieldSize : 4;
				return;
			}

			EffectiveFieldSize = ExpectedFieldSize;
		}

		/// <summary>
		/// Shortcut to Field name
		/// </summary>
		public string FieldName => FieldInfo.Name;

		/// <summary>
		/// TypeCode of the underlying field, resolved once. Type.GetTypeCode used to be called for
		/// every field of every message in both directions.
		/// </summary>
		public TypeCode FieldTypeCode { get; }

		public bool UseRangeCheck { get; } = false;


		public FieldRangeValue<sbyte> RangeSByte { get; }
		public FieldRangeValue<int> RangeInt { get; }
		public FieldRangeValue<uint> RangeUInt { get; }
		public FieldRangeValue<long> RangeInt64 { get; }
		public FieldRangeValue<ulong> RangeUInt64 { get; }
		public FieldRangeValue<short> RangeInt16 { get; }
		public FieldRangeValue<ushort> RangeUInt16 { get; }
		public FieldRangeValue<decimal> RangeDecimal { get; }
		public FieldRangeValue<float> RangeSingle { get; }
		public FieldRangeValue<double> RangeDouble { get; }

		/// <param name="rangeCulture">
		/// Culture for the string limits of a ProtocolFieldRangeAttribute. Null means invariant,
		/// which is the default. See ProtocolSetupArgument.RangeCulture.
		/// </param>
		public ConverterFieldInfo(FieldInfo fieldInfo, ProtocolFieldAttribute attribute, CultureInfo rangeCulture = null)
		{
			rangeCulture ??= CultureInfo.InvariantCulture;

			FieldInfo = fieldInfo;
			FieldTypeCode = Type.GetTypeCode(fieldInfo.FieldType);

			Setter = FastInvoke.BuildUntypedSetter<T>(fieldInfo);
			Getter = FastInvoke.BuildUntypedGetter<T>(fieldInfo);


			Attribute = attribute;

			IsString = FieldInfo.FieldType == typeof(string);
			IsEnum = FieldInfo.FieldType.IsEnum;
			IsDateTime = FieldInfo.FieldType == typeof(DateTime);
			IsGuid = FieldInfo.FieldType == typeof(Guid);
			IsByte = FieldInfo.FieldType == typeof(byte);

			IsBitValue = Attribute.TypeInByteArray == DestinationType.Bits;

			// needs Attribute, IsEnum and IsString, and must happen before the early return for byte
			BuildTypedAccessors(fieldInfo, out var typedSetter, out var typedGetter);
			TypedSetter = typedSetter;
			TypedGetter = typedGetter;

			if (IsDateTime)
			{
				var x = FieldInfo
					.GetCustomAttributes(false)
					.FirstOrDefault(y => y.GetType() == typeof(ProtocolDateTimeFieldAttribute));

				if (x != null)
				{
					DateTimeAttribute = (ProtocolDateTimeFieldAttribute) x;
				}
				else
				{
					throw new ArgumentException("you must provide a ProtocolDateTimeFieldAttribute");
				}

				ExpectedFieldSize = DateTimeAttribute.DateTimeByteFormat == DateTimeByteFormat.UnixTimeStamp32Bit ? 4 : 8;
			}
			
			if (IsByte)
			{
				var x = FieldInfo
					.GetCustomAttributes(false)
					.FirstOrDefault(y => y.GetType() == typeof(ProtocolBytePaddingAttribute));

				if (x != null)
				{
					BytePaddingAttribute = (ProtocolBytePaddingAttribute) x;

					IsPaddingByte = true;
				}

				ExpectedFieldSize = 1;

				ResolveEffectiveFieldSize();
				return;
			}

			if (! (IsDateTime || IsByte))
			{
				ExpectedFieldSize = IsString || IsEnum ? -1 : Marshal.SizeOf(FieldInfo.FieldType);
			}
			
			if (FieldInfo.FieldType == typeof(bool))
			{
				ExpectedFieldSize = 1;
			}

			if (IsEnum)
			{
				switch (Attribute.TypeInByteArray)
				{
					case DestinationType.Byte:
						ExpectedFieldSize = 1;
						break;
					case DestinationType.Default:
						break;
					case DestinationType.Int16:
					case DestinationType.UInt16:
						ExpectedFieldSize = 2;
						break;
					case DestinationType.Int32:
					case DestinationType.UInt32:
						ExpectedFieldSize = 4;
						break;
					case DestinationType.Int64:
					case DestinationType.UInt64:
						ExpectedFieldSize = 8;
						break;
					default:
						throw new ArgumentOutOfRangeException($"the cohosen enum TypeInByteArray is not supported for {FieldName}" );
				}
			}


			if (IsString)
			{
				var x = FieldInfo
					.GetCustomAttributes(false)
					.FirstOrDefault(y => y.GetType() == typeof(ProtocolStringFieldAttribute));

				if (x != null)
				{
					StrAttribute = (ProtocolStringFieldAttribute) x;
				}
			}


			// everything the size depends on is known now: ExpectedFieldSize, the padding attribute
			// and the string attribute. The range block below only adds range values and may return
			// early, so resolve the size here to cover every remaining exit of this constructor.
			ResolveEffectiveFieldSize();

			if (!(IsString || IsEnum))
			{
				var r = FieldInfo
					.GetCustomAttributes(false)
					.FirstOrDefault(y => y.GetType() == typeof(ProtocolFieldRangeAttribute));

				if (r == null) return;


				UseRangeCheck = true;

				var rawRangeAttribute = (ProtocolFieldRangeAttribute) r;

				switch (FieldTypeCode)
				{
					case TypeCode.Int64:
						RangeInt64 = new FieldRangeValue<Int64>(rawRangeAttribute, rangeCulture);
						break;
					case TypeCode.UInt64:
						RangeUInt64 = new FieldRangeValue<UInt64>(rawRangeAttribute, rangeCulture);
						break;
					case TypeCode.Int32:
						RangeInt = new FieldRangeValue<int>(rawRangeAttribute, rangeCulture);
						break;
					case TypeCode.UInt32:
						RangeUInt = new FieldRangeValue<uint>(rawRangeAttribute, rangeCulture);
						break;
					case TypeCode.Int16:
						RangeInt16 = new FieldRangeValue<Int16>(rawRangeAttribute, rangeCulture);
						break;
					case TypeCode.UInt16:
						RangeUInt16 = new FieldRangeValue<UInt16>(rawRangeAttribute, rangeCulture);
						break;
					case TypeCode.SByte:
						RangeSByte = new FieldRangeValue<sbyte>(rawRangeAttribute, rangeCulture);
						break;
					// NOTE: byte fields never reach this switch, the constructor returns early for them.
					// Range support for byte is therefore not available, see the IsByte block above.
					case TypeCode.Decimal:
						RangeDecimal = new FieldRangeValue<decimal>(rawRangeAttribute, rangeCulture);
						break;
					case TypeCode.Single:
						RangeSingle = new FieldRangeValue<Single>(rawRangeAttribute, rangeCulture);
						break;
					case TypeCode.Double:
						RangeDouble = new FieldRangeValue<Double>(rawRangeAttribute, rangeCulture);
						break;
				}
			}
		}


		public bool IsInRange(object val)
		{
			switch (FieldTypeCode)
			{
				case TypeCode.Int64:
					return RangeInt64.IsInRange((long) val);
				case TypeCode.UInt64:
					return RangeUInt64.IsInRange((ulong) val);
				case TypeCode.Int32:
					return RangeInt.IsInRange((int) val);
				case TypeCode.UInt32:
					return RangeUInt.IsInRange((uint) val);
				case TypeCode.Int16:
					return RangeInt16.IsInRange((Int16) val);
				case TypeCode.UInt16:
					return RangeUInt16.IsInRange((UInt16) val);
				case TypeCode.SByte:
					return RangeSByte.IsInRange((sbyte) val);
				case TypeCode.Decimal:
					return RangeDecimal.IsInRange((decimal) val);
				case TypeCode.Single:
					return RangeSingle.IsInRange((Single) val);
				case TypeCode.Double:
					return RangeDouble.IsInRange((double) val);
			}

			return false;
		}
	}
}
