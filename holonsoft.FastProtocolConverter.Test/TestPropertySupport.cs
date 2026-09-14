using holonsoft.FastProtocolConverter.Abstractions.Enums;
using holonsoft.FastProtocolConverter.Abstractions.Exceptions;
using holonsoft.FastProtocolConverter.Abstractions.Interfaces;
using holonsoft.FastProtocolConverter.Test.dto;
using Microsoft.Extensions.Logging;
using Shouldly;
using System;
using System.Reflection;
using Xunit;

namespace holonsoft.FastProtocolConverter.Test
{
	/// <summary>
	/// Properties as protocol members, new in 4.0.
	///
	/// The test that carries the most weight is the first one: the same protocol declared once with
	/// fields and once with properties has to produce **byte identical** frames. If that holds for
	/// every supported type, property support cannot have changed the wire format, which is the only
	/// promise that really matters to somebody already exchanging frames.
	/// </summary>
	public class TestPropertySupport
	{
		private readonly ILogger _logger = null;


		private IProtocolConverter<TPoco> CreateConverter<TPoco>()
			where TPoco : class, new()
		{
			var converter = new ProtocolConverter<TPoco>(_logger) as IProtocolConverter<TPoco>;
			converter.Prepare();
			return converter;
		}


		private static readonly Guid SampleGuid = new("0f8fad5b-d9cb-469f-a165-70867728950e");
		private static readonly DateTime SampleStamp = new(2026, 9, 14, 8, 21, 0, DateTimeKind.Utc);


		/// <summary>
		/// Every supported type, declared as fields and as properties, has to give the same bytes.
		/// </summary>
		[Fact]
		public void TestPropertyPocoProducesTheSameBytesAsTheFieldPoco()
		{
			var fieldConverter = CreateConverter<AllTypesFieldPoco>();
			var propertyConverter = CreateConverter<AllTypesPropertyPoco>();

			var fromFields = fieldConverter.ConvertToByteArray(new AllTypesFieldPoco
			{
				SByteValue = -42, ByteValue = 200, BoolValue = true,
				ShortValue = -12345, UShortValue = 60000,
				IntValue = -1234567, UIntValue = 4000000000,
				LongValue = -9000000000, ULongValue = 18000000000000000000,
				FloatValue = -17.25f, DoubleValue = 1234.5678d,
				DecimalValue = -12345.6789m, GuidValue = SampleGuid,
				DateTimeValue = SampleStamp, EnumValue = MyImportantEnum.C,
				StringValue = "abc",
			});

			var fromProperties = propertyConverter.ConvertToByteArray(new AllTypesPropertyPoco
			{
				SByteValue = -42, ByteValue = 200, BoolValue = true,
				ShortValue = -12345, UShortValue = 60000,
				IntValue = -1234567, UIntValue = 4000000000,
				LongValue = -9000000000, ULongValue = 18000000000000000000,
				FloatValue = -17.25f, DoubleValue = 1234.5678d,
				DecimalValue = -12345.6789m, GuidValue = SampleGuid,
				DateTimeValue = SampleStamp, EnumValue = MyImportantEnum.C,
				StringValue = "abc",
			});

			fromProperties.ShouldBe(fromFields, "a property protocol must be byte identical to the field one");
			fromProperties.Length.ShouldBe(91);
		}


		/// <summary>
		/// And the same frame has to read back into either shape with the same values.
		/// </summary>
		[Fact]
		public void TestPropertyPocoReadsTheSameValuesAsTheFieldPoco()
		{
			var fieldConverter = CreateConverter<AllTypesFieldPoco>();
			var propertyConverter = CreateConverter<AllTypesPropertyPoco>();

			var frame = fieldConverter.ConvertToByteArray(new AllTypesFieldPoco
			{
				SByteValue = -42, ByteValue = 200, BoolValue = true,
				ShortValue = -12345, UShortValue = 60000,
				IntValue = -1234567, UIntValue = 4000000000,
				LongValue = -9000000000, ULongValue = 18000000000000000000,
				FloatValue = -17.25f, DoubleValue = 1234.5678d,
				DecimalValue = -12345.6789m, GuidValue = SampleGuid,
				DateTimeValue = SampleStamp, EnumValue = MyImportantEnum.C,
				StringValue = "abc",
			});

			var asFields = fieldConverter.ConvertFromByteArray(frame);
			var asProperties = propertyConverter.ConvertFromByteArray(frame);

			asProperties.SByteValue.ShouldBe(asFields.SByteValue);
			asProperties.ByteValue.ShouldBe(asFields.ByteValue);
			asProperties.BoolValue.ShouldBe(asFields.BoolValue);
			asProperties.ShortValue.ShouldBe(asFields.ShortValue);
			asProperties.UShortValue.ShouldBe(asFields.UShortValue);
			asProperties.IntValue.ShouldBe(asFields.IntValue);
			asProperties.UIntValue.ShouldBe(asFields.UIntValue);
			asProperties.LongValue.ShouldBe(asFields.LongValue);
			asProperties.ULongValue.ShouldBe(asFields.ULongValue);
			asProperties.FloatValue.ShouldBe(asFields.FloatValue);
			asProperties.DoubleValue.ShouldBe(asFields.DoubleValue);
			asProperties.DecimalValue.ShouldBe(asFields.DecimalValue);
			asProperties.GuidValue.ShouldBe(asFields.GuidValue);
			asProperties.DateTimeValue.ShouldBe(asFields.DateTimeValue);
			asProperties.DateTimeValue.Kind.ShouldBe(DateTimeKind.Utc);
			asProperties.EnumValue.ShouldBe(asFields.EnumValue);
			asProperties.StringValue.ShouldBe(asFields.StringValue);
		}


		/// <summary>
		/// Byte order is decided per protocol and must not care whether a member is a field.
		/// </summary>
		[Fact]
		public void TestBigEndianPropertyPocoMatchesTheFieldPoco()
		{
			var fieldConverter = CreateConverter<AllTypesFieldBigEndianPoco>();
			var propertyConverter = CreateConverter<AllTypesPropertyBigEndianPoco>();

			var fromFields = fieldConverter.ConvertToByteArray(new AllTypesFieldBigEndianPoco
			{
				ShortValue = -321, IntValue = -123456, LongValue = -9000000000,
				DoubleValue = 98765.4321d, GuidValue = SampleGuid,
			});

			var fromProperties = propertyConverter.ConvertToByteArray(new AllTypesPropertyBigEndianPoco
			{
				ShortValue = -321, IntValue = -123456, LongValue = -9000000000,
				DoubleValue = 98765.4321d, GuidValue = SampleGuid,
			});

			fromProperties.ShouldBe(fromFields);

			var back = propertyConverter.ConvertFromByteArray(fromProperties);

			back.LongValue.ShouldBe(-9000000000);
			back.GuidValue.ShouldBe(SampleGuid);
		}


		/// <summary>
		/// A POCO that grew over time and has both shapes in it.
		/// </summary>
		[Fact]
		public void TestFieldsAndPropertiesMixInOneProtocol()
		{
			var converter = CreateConverter<MixedFieldAndPropertyPoco>();

			var frame = converter.ConvertToByteArray(new MixedFieldAndPropertyPoco
			{
				FromAField = 1, FromAProperty = 2, AlsoAField = 3, AlsoAProperty = 4,
			});

			frame.Length.ShouldBe(12);

			var result = converter.ConvertFromByteArray(frame);

			result.FromAField.ShouldBe(1);
			result.FromAProperty.ShouldBe(2);
			result.AlsoAField.ShouldBe((short) 3);
			result.AlsoAProperty.ShouldBe((short) 4);
		}


		/// <summary>
		/// An init only and a private setter are both assignable through the compiled accessor, so a
		/// POCO that looks immutable from the outside can still be filled from a frame.
		/// </summary>
		[Fact]
		public void TestInitOnlyAndPrivateSetterAreFilled()
		{
			var converter = CreateConverter<InitAndPrivateSetterPoco>();

			var source = new InitAndPrivateSetterPoco { InitOnly = 4711 };
			source.SetPrivate(815);

			var frame = converter.ConvertToByteArray(source);

			var result = converter.ConvertFromByteArray(frame);

			result.InitOnly.ShouldBe(4711, "an init only property is assignable through the compiled accessor");
			result.PrivateSetter.ShouldBe(815, "so is a private setter");
		}


		/// <summary>
		/// A computed property, an indexer and a static property next to the protocol members must be
		/// skipped quietly. None of them carries the attribute, so none of them is the user's mistake.
		/// </summary>
		[Fact]
		public void TestUnrelatedMembersAreIgnored()
		{
			var converter = CreateConverter<PocoWithUnrelatedMembers>();

			var frame = converter.ConvertToByteArray(new PocoWithUnrelatedMembers { Real = 3, AlsoReal = 4 });

			frame.Length.ShouldBe(8, "only the two attributed members are part of the protocol");

			var result = converter.ConvertFromByteArray(frame);

			result.Real.ShouldBe(3);
			result.AlsoReal.ShouldBe(4);
			result.Computed.ShouldBe(6);
		}


		/// <summary>
		/// A get only property that does claim to be part of the protocol is a mistake worth naming:
		/// reading a frame would have to assign it.
		/// </summary>
		[Fact]
		public void TestGetOnlyProtocolPropertyIsRejectedWithAUsefulMessage()
		{
			var converter = new ProtocolConverter<GetOnlyProtocolPropertyPoco>(_logger)
				as IProtocolConverter<GetOnlyProtocolPropertyPoco>;

			var ex = Should.Throw<ProtocolConverterException>(() => converter.Prepare());

			ex.Message.ShouldContain("Computed");
			ex.Message.ShouldContain("setter");
		}


		/// <summary>
		/// The same property, explicitly excluded, is fine.
		/// </summary>
		[Fact]
		public void TestGetOnlyPropertyMarkedAsIgnoredIsAccepted()
		{
			var converter = CreateConverter<GetOnlyIgnoredPropertyPoco>();

			var frame = converter.ConvertToByteArray(new GetOnlyIgnoredPropertyPoco { Real = 9 });

			frame.Length.ShouldBe(4);
			converter.ConvertFromByteArray(frame).Real.ShouldBe(9);
		}


		/// <summary>
		/// A bool occupies one byte, not the four that Marshal.SizeOf reports for it. The trailing
		/// field only lands where the protocol says if that holds for properties too.
		/// </summary>
		[Fact]
		public void TestBoolPropertyOccupiesASingleByte()
		{
			var converter = CreateConverter<BoolPropertyPoco>();

			var frame = converter.ConvertToByteArray(new BoolPropertyPoco
			{
				First = true, Second = false, Trailer = 0x0A0B0C0D,
			});

			frame.Length.ShouldBe(6, "1 + 1 + 4, a bool is one byte");

			frame[0].ShouldBe((byte) 1);
			frame[1].ShouldBe((byte) 0);

			var result = converter.ConvertFromByteArray(frame);

			result.First.ShouldBeTrue();
			result.Second.ShouldBeFalse();
			result.Trailer.ShouldBe(0x0A0B0C0D);
		}


		/// <summary>
		/// Range limits work on properties, and the violation handler is handed the PropertyInfo of
		/// the member that went out of range.
		/// </summary>
		[Fact]
		public void TestRangeViolationOnAPropertyReportsThePropertyInfo()
		{
			var converter = CreateConverter<RangeOnPropertyPoco>();

			var payload = converter.ConvertToByteArray(new RangeOnPropertyPoco { Value = 500, Ratio = 99.5f });

			var reported = new System.Collections.Generic.List<string>();

			void Handler(MemberInfo member, out ConverterRangeViolationBehaviour chosen)
			{
				reported.Add($"{member.MemberType}:{member.Name}");
				chosen = ConverterRangeViolationBehaviour.SetToDefaultValue;
			}

			converter.OnRangeViolation += Handler;
			try
			{
				var result = converter.ConvertFromByteArray(payload);

				result.Value.ShouldBe(7);
				result.Ratio.ShouldBe(0.5f);
			}
			finally
			{
				converter.OnRangeViolation -= Handler;
			}

			reported.ShouldBe(["Property:Value", "Property:Ratio"]);
		}


		/// <summary>
		/// A sequence protocol of properties, including the variable length string whose length lives
		/// in a property of its own and is assigned by the converter while writing.
		/// </summary>
		[Fact]
		public void TestSequenceProtocolOfProperties()
		{
			var converter = CreateConverter<SequencePropertyPoco>();

			var source = new SequencePropertyPoco { Text = "hello world", Trailer = 4711 };

			var frame = converter.ConvertToByteArray(source);

			source.TextLength.ShouldBe(11, "the converter assigns the length property while writing");
			frame.Length.ShouldBe(4 + 11 + 4);

			var result = converter.ConvertFromByteArray(frame);

			result.TextLength.ShouldBe(11);
			result.Text.ShouldBe("hello world");
			result.Trailer.ShouldBe(4711);
		}


		/// <summary>
		/// All three write paths have to agree for a property protocol as well.
		/// </summary>
		[Fact]
		public void TestAllWriteOverloadsAgreeForAPropertyPoco()
		{
			var converter = CreateConverter<AllTypesPropertyPoco>();

			var source = new AllTypesPropertyPoco
			{
				SByteValue = 1, ByteValue = 2, BoolValue = true, ShortValue = 3, UShortValue = 4,
				IntValue = 5, UIntValue = 6, LongValue = 7, ULongValue = 8,
				FloatValue = 9.5f, DoubleValue = 10.5d, DecimalValue = 11.5m,
				GuidValue = SampleGuid, DateTimeValue = SampleStamp,
				EnumValue = MyImportantEnum.B, StringValue = "prop",
			};

			var fromArray = converter.ConvertToByteArray(source);

			converter.GetByteCount(source).ShouldBe(fromArray.Length);

			var destination = new byte[fromArray.Length];
			converter.TryConvertToByteArray(source, destination, out var written).ShouldBeTrue();
			written.ShouldBe(fromArray.Length);
			destination.ShouldBe(fromArray);

			var bufferWriter = new System.Buffers.ArrayBufferWriter<byte>();
			converter.ConvertToByteArray(source, bufferWriter);
			bufferWriter.WrittenSpan.ToArray().ShouldBe(fromArray);
		}
	}
}
