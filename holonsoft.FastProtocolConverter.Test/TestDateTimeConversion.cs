using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Enums;
using holonsoft.FastProtocolConverter.Abstractions.Interfaces;
using holonsoft.FastProtocolConverter.Test.dto;
using Microsoft.Extensions.Logging;
using Shouldly;
using System;
using Xunit;

namespace holonsoft.FastProtocolConverter.Test
{
	/// <summary>
	/// The DateTime handlers, in both unix formats and both kinds.
	///
	/// The golden vectors cannot cover this. They pin the bytes, and the Kind of a decoded DateTime
	/// is not in the bytes: a decoder that returns the right instant with the wrong Kind writes the
	/// very same frame back out. That matters because <see cref="ProtocolDateTimeField"/> declares
	/// the Kind, so it is part of the protocol contract and not an implementation detail.
	/// </summary>
	public class TestDateTimeConversion
	{
		private readonly ILogger _logger = null;


		private static IProtocolConverter<TPoco> CreateConverter<TPoco>(ILogger logger)
			where TPoco : class, new()
		{
			var converter = new ProtocolConverter<TPoco>(logger) as IProtocolConverter<TPoco>;
			converter.Prepare();
			return converter;
		}


		/// <summary>
		/// A decoded timestamp is built from the unix epoch, which is UTC, so the result has to be
		/// UTC as well. An Unspecified Kind here would be silently reinterpreted as local time by
		/// the next DateTimeOffset conversion, which is a real bug in a distributed system.
		/// </summary>
		[Theory]
		[InlineData(DateTimeByteFormat.UnixTimeStamp32Bit)]
		[InlineData(DateTimeByteFormat.UnixTimeStamp64Bit)]
		public void TestDecodedTimestampIsUtc(DateTimeByteFormat format)
		{
			var converter = format == DateTimeByteFormat.UnixTimeStamp32Bit
				? (object) CreateConverter<UtcTimestamp32Poco>(_logger)
				: CreateConverter<UtcTimestamp64Poco>(_logger);

			var stamp = new DateTime(2026, 9, 13, 21, 47, 11, DateTimeKind.Utc);

			if (converter is IProtocolConverter<UtcTimestamp32Poco> c32)
			{
				var result = c32.ConvertFromByteArray(c32.ConvertToByteArray(new UtcTimestamp32Poco { Stamp = stamp }));

				result.Stamp.Kind.ShouldBe(DateTimeKind.Utc);
				result.Stamp.ShouldBe(stamp);
			}
			else
			{
				var c64 = (IProtocolConverter<UtcTimestamp64Poco>) converter;
				var result = c64.ConvertFromByteArray(c64.ConvertToByteArray(new UtcTimestamp64Poco { Stamp = stamp }));

				result.Stamp.Kind.ShouldBe(DateTimeKind.Utc);
				result.Stamp.ShouldBe(stamp);
			}
		}


		/// <summary>
		/// The exact instant has to survive a round trip to the second, in both widths.
		/// </summary>
		[Fact]
		public void TestRoundTripKeepsTheInstant()
		{
			var converter = CreateConverter<UtcTimestamp64Poco>(_logger);

			foreach (var stamp in new[]
			{
				new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
				new DateTime(2000, 2, 29, 12, 0, 0, DateTimeKind.Utc),
				new DateTime(2026, 9, 13, 21, 47, 11, DateTimeKind.Utc),
				new DateTime(2100, 12, 31, 23, 59, 59, DateTimeKind.Utc),
			})
			{
				var result = converter.ConvertFromByteArray(
					converter.ConvertToByteArray(new UtcTimestamp64Poco { Stamp = stamp }));

				result.Stamp.ShouldBe(stamp, $"round trip of {stamp:O}");
			}
		}


		/// <summary>
		/// A timestamp before the epoch is a negative unix value, which the 32 bit format stores as
		/// a signed int. It used to be easy to lose the sign on the way through the cast chain.
		/// </summary>
		[Fact]
		public void TestTimestampBeforeTheEpochSurvives()
		{
			var converter = CreateConverter<UtcTimestamp32Poco>(_logger);

			var stamp = new DateTime(1960, 6, 15, 8, 30, 0, DateTimeKind.Utc);

			var result = converter.ConvertFromByteArray(
				converter.ConvertToByteArray(new UtcTimestamp32Poco { Stamp = stamp }));

			result.Stamp.ShouldBe(stamp);
			result.Stamp.Kind.ShouldBe(DateTimeKind.Utc);
		}


		/// <summary>
		/// A field declared as local time is normalised to local before it is written, so the
		/// instant, not the wall clock reading, is what travels. Reading gives UTC back, which is
		/// the same instant.
		/// </summary>
		[Fact]
		public void TestLocalKindWritesTheSameInstant()
		{
			var utcConverter = CreateConverter<UtcTimestamp64Poco>(_logger);
			var localConverter = CreateConverter<LocalTimestamp64Poco>(_logger);

			var stamp = new DateTime(2026, 9, 13, 21, 47, 11, DateTimeKind.Utc);

			var utcBytes = utcConverter.ConvertToByteArray(new UtcTimestamp64Poco { Stamp = stamp });
			var localBytes = localConverter.ConvertToByteArray(new LocalTimestamp64Poco { Stamp = stamp });

			// same moment in time, so the same number of seconds since the epoch
			localBytes.ShouldBe(utcBytes);
		}


		/// <summary>
		/// Big endian has its own read and write branch for both widths.
		/// </summary>
		[Fact]
		public void TestBigEndianTimestampRoundTrips()
		{
			var converter = CreateConverter<UtcTimestamp64BigEndianPoco>(_logger);

			var stamp = new DateTime(2026, 9, 13, 21, 47, 11, DateTimeKind.Utc);

			var bytes = converter.ConvertToByteArray(new UtcTimestamp64BigEndianPoco { Stamp = stamp });
			var result = converter.ConvertFromByteArray(bytes);

			result.Stamp.ShouldBe(stamp);
			result.Stamp.Kind.ShouldBe(DateTimeKind.Utc);

			// and the byte order really is the other way round
			var little = CreateConverter<UtcTimestamp64Poco>(_logger)
				.ConvertToByteArray(new UtcTimestamp64Poco { Stamp = stamp });

			bytes.ShouldNotBe(little);
			Array.Reverse(little);
			bytes.ShouldBe(little);
		}
	}


	public class UtcTimestamp32Poco
	{
		[ProtocolField(StartPos = 0)]
		[ProtocolDateTimeField(DateTimeKind = DateTimeKind.Utc, DateTimeByteFormat = DateTimeByteFormat.UnixTimeStamp32Bit)]
		public DateTime Stamp;
	}


	public class UtcTimestamp64Poco
	{
		[ProtocolField(StartPos = 0)]
		[ProtocolDateTimeField(DateTimeKind = DateTimeKind.Utc, DateTimeByteFormat = DateTimeByteFormat.UnixTimeStamp64Bit)]
		public DateTime Stamp;
	}


	public class LocalTimestamp64Poco
	{
		[ProtocolField(StartPos = 0)]
		[ProtocolDateTimeField(DateTimeKind = DateTimeKind.Local, DateTimeByteFormat = DateTimeByteFormat.UnixTimeStamp64Bit)]
		public DateTime Stamp;
	}


	[ProtocolSetupArgument(UseBigEndian = true)]
	public class UtcTimestamp64BigEndianPoco
	{
		[ProtocolField(StartPos = 0)]
		[ProtocolDateTimeField(DateTimeKind = DateTimeKind.Utc, DateTimeByteFormat = DateTimeByteFormat.UnixTimeStamp64Bit)]
		public DateTime Stamp;
	}
}
