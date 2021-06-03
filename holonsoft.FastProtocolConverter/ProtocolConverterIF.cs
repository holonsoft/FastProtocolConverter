using holonsoft.FastProtocolConverter.Abstractions.Delegates;
using holonsoft.FastProtocolConverter.Abstractions.Interfaces;


namespace holonsoft.FastProtocolConverter
{
	public partial class ProtocolConverter<T> : IProtocolConverter<T>
			where T : class, new()
	{
		void IProtocolConverter<T>.Prepare() => Prepare();

		T IProtocolConverter<T>.ConvertFromByteArray(byte[] data) => ConvertFromByteArray(data);
		void IProtocolConverter<T>.ConvertFromByteArray(byte[] data, T instance) => ConvertFromByteArray(data, instance);

		byte[] IProtocolConverter<T>.ConvertToByteArray(T data) => ConvertToByteArray(data);

		event OnRangeViolationDelegate IProtocolConverter<T>.OnRangeViolation
		{
			add => this.OnRangeViolation += value;
			remove => this.OnRangeViolation -= value;
		}

		event OnSplitBitValuesDelegate<T> IProtocolConverter<T>.OnSplitBitValues
		{
			add => this.OnSplitBitValues += value;
			remove => this.OnSplitBitValues -= value;
		}

		event OnConsolidateBitValuesDelegate<T> IProtocolConverter<T>.OnConsolidateBitValues
		{
			add => this.OnConsolidateBitValues += value;
			remove => this.OnConsolidateBitValues -= value;
		}
	}
}

