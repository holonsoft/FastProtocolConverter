using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;
using holonsoft.FluentConditions;
using holonsoft.FastProtocolConverter.Abstractions.Attributes;
using holonsoft.FastProtocolConverter.Abstractions.Delegates;
using holonsoft.FastProtocolConverter.Abstractions.Exceptions;
using holonsoft.FastProtocolConverter.Abstractions.Interfaces;
using holonsoft.FastProtocolConverter.dto;


namespace holonsoft.FastProtocolConverter
{
    public partial class ProtocolConverter<T>
        where T : class, new()
    {
        private readonly ILogger _logger;
        private readonly string _moduleName = nameof(ProtocolConverter<T>);

        private T _templateInstance = null;
        private int _totalMinLength;

        private readonly SortedList<int, ConverterFieldInfo<T>> _fieldListFixPos = new SortedList<int, ConverterFieldInfo<T>>();
        private readonly SortedList<int, ConverterFieldInfo<T>> _fieldListSeqPos = new SortedList<int, ConverterFieldInfo<T>>();
        private readonly SortedList<string, ConverterFieldInfo<T>> _fieldListByName = new SortedList<string, ConverterFieldInfo<T>>();

        private readonly Dictionary<Type, object> _converterList = new Dictionary<Type, object>();

        private readonly byte[] _converterHelper = new byte[4];
        private int _globalOffsetInByteArray;

        /// <summary>
        /// Culture for the string limits of ProtocolFieldRangeAttribute. Invariant unless the
        /// protocol definition names a different one, never taken from the environment.
        /// </summary>
        private CultureInfo _rangeCulture = CultureInfo.InvariantCulture;


        private CultureInfo ResolveRangeCulture(string cultureName)
        {
            if (string.IsNullOrWhiteSpace(cultureName)) return CultureInfo.InvariantCulture;

            try
            {
                // predefinedOnly, otherwise ICU happily invents a custom culture for any string that
                // merely looks like a language tag, and a typo would silently change what a limit means
                return CultureInfo.GetCultureInfo(cultureName, predefinedOnly: true);
            }
            catch (CultureNotFoundException ex)
            {
                var msg = $"RangeCulture '{cultureName}' of {typeof(T).Name} is not a known culture name";
                _logger?.Log(LogLevel.Critical, $"{_moduleName}{MethodBase.GetCurrentMethod()?.Name} {msg}");

                throw new ProtocolConverterException(msg, ex);
            }
        }

        public bool UseBigEndian { get; set; } = false;

        public bool IsPrepared { get; private set; } = false;

        public OnRangeViolationDelegate OnRangeViolation { get; set; }

				public OnSplitBitValuesDelegate<T> OnSplitBitValues { get; set; }
				
				public OnConsolidateBitValuesDelegate<T> OnConsolidateBitValues { get; set; }

				public ProtocolConverter(ILogger logger)
        {
            _logger = logger;
        }

        private void Prepare()
        {
            IsPrepared.Requires(nameof(IsPrepared)).IsFalse(); 
            
            _templateInstance = Activator.CreateInstance<T>();

            var setupAttr = _templateInstance.GetType().GetCustomAttributes(false).FirstOrDefault();
            if (setupAttr is ProtocolSetupArgument argument)
            {
                _globalOffsetInByteArray = argument.OffsetInByteArray;

                UseBigEndian = argument.UseBigEndian;

                _rangeCulture = ResolveRangeCulture(argument.RangeCulture);
            }
            else
            {
                _globalOffsetInByteArray = 0;
            }

            foreach (var info in from field in _templateInstance.GetType().GetFields()
                                 where !field.IsInitOnly && !field.IsLiteral && !field.IsStatic
                                 let attributes = field.GetCustomAttributes(typeof(ProtocolFieldAttribute), false)
                                 let attribute = (attributes.Length == 1) ? (ProtocolFieldAttribute)attributes[0] : null
                                 where attribute != null
                                 select new ConverterFieldInfo<T>(field, attribute, _rangeCulture))
            {
                if (info.Attribute.IgnoreField)
                {
                    _logger?.Log(LogLevel.Trace, $"{_moduleName}{MethodBase.GetCurrentMethod()?.Name} Field is ignored: {info.FieldName}");
                    continue;
                }

                if ((info.Attribute.StartPos > -1) && (_fieldListFixPos.ContainsKey(info.Attribute.StartPos)))
                {
                    var msg = $"Fields '{_fieldListFixPos[info.Attribute.StartPos].FieldName}' and '{info.FieldName}' have the same position in byte array definition, that's not possible";
                    _logger?.Log(LogLevel.Critical, $"{_moduleName}{MethodBase.GetCurrentMethod()?.Name} {msg}");

                    throw new ProtocolConverterException(msg);
                }

                if (info.Attribute.StartPos > -1)
                {
                    _fieldListFixPos.Add(info.Attribute.StartPos, info);

                    _logger?.Log(LogLevel.Trace, $"{_moduleName}{MethodBase.GetCurrentMethod()?.Name} Adding field {info.FieldName} at array position {info.Attribute.StartPos}");
                }

                if (info.Attribute.SequenceNo > 0)
                {
                    _fieldListSeqPos.Add(info.Attribute.SequenceNo, info);

                    _logger?.Log(LogLevel.Trace, $"{_moduleName}{MethodBase.GetCurrentMethod()?.Name} Adding field {info.FieldName} at sequence position {info.Attribute.SequenceNo}");
                }
            }

            if (_fieldListFixPos.Count == 0)
            {
                var msg = "No fields with attributes found at all";
                _logger?.Log(LogLevel.Critical, $"{_moduleName}{MethodBase.GetCurrentMethod()?.Name} {msg}");

                throw new ProtocolConverterException(msg);
            }

            // Overlap detection and the minimum length both need the size a field really occupies.
            // ExpectedFieldSize reports -1 for strings and enums and 1 for padding bytes, which made
            // the old calculation meaningless as soon as a protocol contained one of them.
            var nextPosition = 0;

            foreach (var kvp in _fieldListFixPos)
            {
                if (kvp.Key < nextPosition)
                {
                    var msg = $"Overlapping fields are not allowed, '{kvp.Value.FieldName}' starts at {kvp.Key} but the previous field ends at {nextPosition}";
                    _logger?.Log(LogLevel.Critical, $"{_moduleName}{MethodBase.GetCurrentMethod()?.Name} {msg}");

                    throw new ProtocolConverterException(msg);
                }

                var effectiveSize = kvp.Value.EffectiveFieldSize;

                // a variable length string has no size yet, it is only legal in a sequence protocol
                if (effectiveSize < 0) continue;

                nextPosition = kvp.Key + effectiveSize;

                if (nextPosition > _totalMinLength) _totalMinLength = nextPosition;
            }

            if (_fieldListSeqPos.Count == 0)
            {
                if (_fieldListFixPos.Any(kvp => kvp.Value.IsString && !kvp.Value.StrAttribute.IsFixedLengthString))
                {
                    var msg = "string fields in a 'fixed position protocol ' are not allowed, consider to add 'sequence no' infos to all fields";
                    _logger?.Log(LogLevel.Critical, $"{_moduleName}{MethodBase.GetCurrentMethod()?.Name} {msg}");
                        
                    throw new ProtocolConverterException(msg);
                }
            }


            if ((_fieldListSeqPos.Count > 0) 
                && ((_fieldListFixPos.Count != 1)
                    || ((_fieldListFixPos.Count == 1) && (_fieldListFixPos.First().Key != 0))
                    || ((_fieldListFixPos.Count == 1) && (_fieldListFixPos.First().Value.Attribute.SequenceNo != 1))))
            {
                var msg = "In variable protocols you must define one field (fixed size type) as fixed startpoint within position 0 and sequence no 1";
                _logger?.Log(LogLevel.Critical, $"{_moduleName}{MethodBase.GetCurrentMethod()?.Name} {msg}");
                
                throw new ProtocolConverterException(msg);
            }


            var expectedSequenceNumber = 1;

            var listOfFieldNames = new List<string>();
            foreach (var kvp in _fieldListSeqPos)
            {
                if (kvp.Key != expectedSequenceNumber)
                {
                    var msg = $"Missing sequence number {expectedSequenceNumber}, seq no must be ascending, integer and without gaps";
                    _logger?.Log(LogLevel.Critical, $"{_moduleName}{MethodBase.GetCurrentMethod()?.Name} {msg}");

                    throw new ProtocolConverterException(msg);
                }


                if (kvp.Value.IsString)
                {
                    if (kvp.Value.StrAttribute == null)
                    {
                        var msg = $"You must provide a ProtocolStringFieldAttribute for {kvp.Value.FieldName}";

                        _logger?.Log(LogLevel.Critical, $"{_moduleName}{MethodBase.GetCurrentMethod()?.Name} {msg}");
                        throw new ProtocolConverterException(msg);
                    }

                    if (!(string.IsNullOrWhiteSpace(kvp.Value.StrAttribute.LengthFieldName) || listOfFieldNames.Contains(kvp.Value.StrAttribute.LengthFieldName)))
                    {
                        var msg = $"LengthField {kvp.Value.StrAttribute.LengthFieldName} must be provided before usage in this field: {kvp.Value.FieldName}";
                        _logger?.Log(LogLevel.Critical, $"{_moduleName}{MethodBase.GetCurrentMethod()?.Name} {msg}");
                        
                        throw new ProtocolConverterException(msg);
                    }
                }

                listOfFieldNames.Add(kvp.Value.FieldName);

                _fieldListByName.Add(kvp.Value.FieldName, kvp.Value);

                expectedSequenceNumber++;
            }

            if (_fieldListSeqPos.Count > 0)
            {
                // A sequence protocol had no minimum length at all before, so a far too short frame
                // was only noticed when a read ran past the end of the array. Variable length strings
                // contribute nothing here, everything else is known up front.
                var minimumOfSequence = 0;

                foreach (var kvp in _fieldListSeqPos)
                {
                    var effectiveSize = kvp.Value.EffectiveFieldSize;

                    if (effectiveSize > 0) minimumOfSequence += effectiveSize;
                }

                _totalMinLength = minimumOfSequence;
            }

            // the leading bytes the converter skips belong to the frame as well
            _totalMinLength += _globalOffsetInByteArray;

            _logger?.Log(LogLevel.Trace, $"{_moduleName}{MethodBase.GetCurrentMethod()?.Name} minimum length of byte array is {_totalMinLength}");

            IsPrepared = true;
        }
    }
}

