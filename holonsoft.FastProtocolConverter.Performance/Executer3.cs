// unset

using holonsoft.FastProtocolConverter.Abstractions.Exceptions;
using holonsoft.FastProtocolConverter.Performance.dto;
using System;
using System.Collections.Generic;

namespace holonsoft.FastProtocolConverter.Performance
{
	public class Executer3
	{
		internal double MeasureTimeFor750KButSmall28BytePocos(int threshold)
		{
			// delivered by external source, so we prepare it here once
			var pocoByteList = new List<byte>();
			pocoByteList.AddRange(BitConverter.GetBytes(UInt64.MaxValue));
			pocoByteList.AddRange(BitConverter.GetBytes(UInt32.MaxValue));
			pocoByteList.AddRange(BitConverter.GetBytes(float.MaxValue));
			pocoByteList.AddRange(BitConverter.GetBytes(float.MinValue));
			pocoByteList.AddRange(BitConverter.GetBytes(Double.MaxValue));

			var pocoBytes = pocoByteList.ToArray();

			var converter = new ProtocolConverter<A28BytesPocoForPerformanceTesting>(null);
			converter.Prepare();

			// step 1 - convert once make sure that all works properly
			var result = new A28BytesPocoForPerformanceTesting
			{
				UInt64Field = BitConverter.ToUInt64(pocoBytes, 0),
				UInt32Field = BitConverter.ToUInt32(pocoBytes, 8),
				FloatField1 = BitConverter.ToSingle(pocoBytes, 12),
				FloatField2 = BitConverter.ToSingle(pocoBytes, 16),
				DoubleField = BitConverter.ToDouble(pocoBytes, 20)
			};

			var testOk = result.DoubleField == Double.MaxValue
									&& result.FloatField1 == float.MaxValue
									&& result.FloatField2 == float.MinValue
									&& result.UInt32Field == UInt32.MaxValue
									&& result.UInt64Field == UInt64.MaxValue;

			if (!testOk)
			{
				throw new ProtocolConverterException("Something is wrong in definition of POCO");
			}

			var startTime = DateTime.UtcNow;

			for (int i = 0; i < threshold; i++)
			{
				result = new A28BytesPocoForPerformanceTesting
				{
					UInt64Field = BitConverter.ToUInt64(pocoBytes, 0),
					UInt32Field = BitConverter.ToUInt32(pocoBytes, 8),
					FloatField1 = BitConverter.ToSingle(pocoBytes, 12),
					FloatField2 = BitConverter.ToSingle(pocoBytes, 16),
					DoubleField = BitConverter.ToDouble(pocoBytes, 20)
				};
			}

			var endTime = DateTime.UtcNow;

			return (endTime - startTime).TotalMilliseconds;
		}
	}
}