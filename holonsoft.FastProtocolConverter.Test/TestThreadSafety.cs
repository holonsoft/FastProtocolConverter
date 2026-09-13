using holonsoft.FastProtocolConverter.Abstractions.Interfaces;
using holonsoft.FastProtocolConverter.Test.dto;
using Microsoft.Extensions.Logging;
using Shouldly;
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace holonsoft.FastProtocolConverter.Test
{
	/// <summary>
	/// A converter is prepared once and then used for every message, which in a high rate signal
	/// environment means many threads share one instance. Nothing in the public API says otherwise,
	/// so a prepared converter has to be safe for concurrent use.
	///
	/// These are stress tests: they are deterministic once the converter is correct, and they fail
	/// with wrong values (not with an exception) while it is not.
	/// </summary>
	public class TestThreadSafety
	{
		private readonly ILogger _logger = null;

		private const int Iterations = 20_000;

		private static readonly MyImportantEnum[] EnumValues =
			[MyImportantEnum.A, MyImportantEnum.B, MyImportantEnum.C, MyImportantEnum.D];


		private static IProtocolConverter<ConcurrencyPoco> CreateConverter(ILogger logger)
		{
			var converter = new ProtocolConverter<ConcurrencyPoco>(logger) as IProtocolConverter<ConcurrencyPoco>;
			converter.Prepare();
			return converter;
		}


		/// <summary>
		/// Reading an enum used a byte[4] scratch buffer that lived on the converter, so two threads
		/// decoding different frames overwrote each other and produced a wrong enum value silently.
		/// </summary>
		[Fact]
		public void TestConcurrentReadsDoNotInterfere()
		{
			var converter = CreateConverter(_logger);

			// one distinct payload per iteration, prepared up front so the test measures reading only
			var payloads = Enumerable.Range(0, Iterations)
				.Select(i => new
				{
					Id = i,
					Enum = EnumValues[i % EnumValues.Length],
					Code = "C" + i.ToString(CultureInfo.InvariantCulture),
				})
				.Select(x => new
				{
					x.Id,
					x.Enum,
					x.Code,
					Bytes = converter.ConvertToByteArray(new ConcurrencyPoco { Id = x.Id, EnumField = x.Enum, Code = x.Code }),
				})
				.ToArray();

			var failures = new ConcurrentBag<string>();

			Parallel.ForEach(payloads, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, item =>
			{
				var result = converter.ConvertFromByteArray(item.Bytes);

				if (result.Id != item.Id) failures.Add($"Id {result.Id} != {item.Id}");
				if (result.EnumField != item.Enum) failures.Add($"Enum {result.EnumField} != {item.Enum} for Id {item.Id}");
				if (result.Code != item.Code) failures.Add($"Code '{result.Code}' != '{item.Code}'");
			});

			failures.ShouldBeEmpty($"{failures.Count} of {Iterations} concurrent reads returned wrong data");
		}


		/// <summary>
		/// Writing a string buffered the encoded bytes in a List that lived on the shared field info,
		/// so two threads serialising different values produced mixed up or truncated output.
		/// </summary>
		[Fact]
		public void TestConcurrentWritesDoNotInterfere()
		{
			var converter = CreateConverter(_logger);

			var sources = Enumerable.Range(0, Iterations)
				.Select(i => new ConcurrencyPoco
				{
					Id = i,
					EnumField = EnumValues[i % EnumValues.Length],
					Code = "Code" + i.ToString(CultureInfo.InvariantCulture),
				})
				.ToArray();

			var failures = new ConcurrentBag<string>();

			Parallel.ForEach(sources, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, source =>
			{
				var bytes = converter.ConvertToByteArray(source);

				if (bytes.Length != 24)
				{
					failures.Add($"length {bytes.Length} != 24 for Id {source.Id}");
					return;
				}

				// decode without the converter, so a shared state bug cannot cancel itself out
				var id = BitConverter.ToInt32(bytes, 0);
				var code = System.Text.Encoding.ASCII.GetString(bytes, 8, 16).TrimEnd('\0');

				if (id != source.Id) failures.Add($"Id {id} != {source.Id}");
				if (code != source.Code) failures.Add($"Code '{code}' != '{source.Code}'");
			});

			failures.ShouldBeEmpty($"{failures.Count} of {Iterations} concurrent writes produced wrong data");
		}


		/// <summary>
		/// Both directions at the same time on one converter, which is the realistic pattern for a
		/// request/response protocol handled by a thread pool.
		/// </summary>
		[Fact]
		public void TestConcurrentReadAndWriteDoNotInterfere()
		{
			var converter = CreateConverter(_logger);

			var failures = new ConcurrentBag<string>();

			Parallel.For(0, Iterations, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
			{
				var source = new ConcurrencyPoco
				{
					Id = i,
					EnumField = EnumValues[i % EnumValues.Length],
					Code = "X" + i.ToString(CultureInfo.InvariantCulture),
				};

				var roundTrip = converter.ConvertFromByteArray(converter.ConvertToByteArray(source));

				if (roundTrip.Id != source.Id) failures.Add($"Id {roundTrip.Id} != {source.Id}");
				if (roundTrip.EnumField != source.EnumField) failures.Add($"Enum mismatch for Id {source.Id}");
				if (roundTrip.Code != source.Code) failures.Add($"Code '{roundTrip.Code}' != '{source.Code}'");
			});

			failures.ShouldBeEmpty($"{failures.Count} of {Iterations} concurrent round trips produced wrong data");
		}
	}
}
