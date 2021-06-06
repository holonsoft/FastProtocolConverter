// unset

using System;

namespace holonsoft.FastProtocolConverter.Abstractions.Attributes
{
	public class ProtocolBytePaddingAttribute : Attribute
	{
		private int _padding;
		public int Padding
		{
			get => _padding;
			set
			{
				if (value < 1) throw new ArgumentOutOfRangeException(@"Padding must be greater than zero");
				_padding = value;
			}
		}
	}
}