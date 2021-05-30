using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace holonsoft.FastProtocolConverter.Performance
{
	class Program
	{
		private static int _loopCount = 100;
		private static int _convertToCount = 750000;

		static void Main(string[] args)
		{
			Console.WriteLine("Starting test for " + _convertToCount + " conversions, performed " + _loopCount + " times");

			PerformanceWithActivatorCreate();
			PerformanceWithInstanceNew();
			PerformanceWithHardCodedConversion();

			Console.WriteLine("Press return/enter");
			Console.ReadLine();
		}


		private static void PerformanceWithActivatorCreate()
		{
			var executer = new Executer1();
			var listOfDurations = new List<double>();

			// Test with Activator.CreateInstance()
			for (int i = 0; i < _loopCount; i++)
			{
				listOfDurations.Add(executer.MeasureTimeFor750KButSmall28BytePocos(_convertToCount));
			}

			ShowStatistics(listOfDurations, "Test with Activator.CreateInstance()");
		}

		private static void PerformanceWithInstanceNew()
		{
			var executer = new Executer2();
			var listOfDurations = new List<double>();

			// Test with Activator.CreateInstance()
			for (int i = 0; i < _loopCount; i++)
			{
				listOfDurations.Add(executer.MeasureTimeFor750KButSmall28BytePocos(_convertToCount));
			}

			ShowStatistics(listOfDurations, "Test with new()");
		}


		private static void PerformanceWithHardCodedConversion()
		{
			var executer = new Executer3();
			var listOfDurations = new List<double>();

			// Test with hard conversion
			for (int i = 0; i < _loopCount; i++)
			{
				listOfDurations.Add(executer.MeasureTimeFor750KButSmall28BytePocos(_convertToCount));
			}

			ShowStatistics(listOfDurations, "Test hard coded conversion with BitConverter");
		}



		private static void ShowStatistics(List<double> listOfDurations, string hint)
		{
			var minValue = listOfDurations.Min();
			var maxValue = listOfDurations.Max();
			var avgValue = listOfDurations.Average();

			Console.WriteLine(hint);
			Console.WriteLine("conversion of " + _convertToCount + " bytesarrays, looped " + _loopCount);
			Console.WriteLine("Min duration was (in ms) " + minValue);
			Console.WriteLine("Max duration was (in ms) " + maxValue);
			Console.WriteLine("Avg duration is (in ms) " + avgValue);
			Console.WriteLine("=> elements/sec " + ((int) ((_convertToCount * 1000) / avgValue)).ToString(CultureInfo.InvariantCulture));

			Console.WriteLine(Enumerable.Repeat('-', 20));
		}
	}
}
