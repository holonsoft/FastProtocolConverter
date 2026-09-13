using System;
using holonsoft.FastProtocolConverter.Abstractions.Delegates;

namespace holonsoft.FastProtocolConverter.Abstractions.Interfaces
{
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
		/// Use a source span to fill the content of POCO
		/// Creates every time a new instance of POCO
		/// </summary>
		/// <remarks>
		/// This is the overload to prefer when the raw bytes already live in a buffer that is not a
		/// plain byte array, for example a rented buffer, a stack allocated frame or a slice of a
		/// larger receive buffer. It saves the copy into a byte[] that the array overloads require.
		/// </remarks>
		/// <param name="data">span with raw values</param>
		/// <returns>An instance of POCO</returns>
		T ConvertFromByteArray(ReadOnlySpan<byte> data);


		/// <summary>
		/// Use a source span to fill the content of POCO
		/// </summary>
		/// <remarks>
		/// Combined with a reused instance this is the allocation free read path: nothing is
		/// allocated per message except what the POCO itself holds, for example its strings.
		/// </remarks>
		/// <param name="data">span with raw values</param>
		/// <param name="instance">an outside created, reusable instance of a POCO</param>
		void ConvertFromByteArray(ReadOnlySpan<byte> data, T instance);

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
}