using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace DbfDataReader
{
    public static class DbfDbConnectionStringBuilderUtil
    {
        public static bool ConvertToBoolean(object value)
        {
            Debug.Assert(null != value, "ConvertToBoolean(null)");
            if (value is string sValue)
            {
                if (StringComparer.OrdinalIgnoreCase.Equals(sValue, "true") || StringComparer.OrdinalIgnoreCase.Equals(sValue, "yes"))
                    return true;
                if (StringComparer.OrdinalIgnoreCase.Equals(sValue, "false") || StringComparer.OrdinalIgnoreCase.Equals(sValue, "no"))
                    return false;
                
                var tmp = sValue.Trim();  // Remove leading & trailing white space.
                if (StringComparer.OrdinalIgnoreCase.Equals(tmp, "true") || StringComparer.OrdinalIgnoreCase.Equals(tmp, "yes"))
                    return true;
                if (StringComparer.OrdinalIgnoreCase.Equals(tmp, "false") || StringComparer.OrdinalIgnoreCase.Equals(tmp, "no"))
                    return false;
                return bool.Parse(sValue);
            }
            
            try
            {
                return ((IConvertible)value).ToBoolean(CultureInfo.InvariantCulture);
            }
            catch (InvalidCastException e)
            {
                throw ConvertFailed(value.GetType(), typeof(Boolean), e);
            }
        }

        public static string ConvertToString(object value)
        {
            try
            {
                return ((IConvertible)value).ToString(CultureInfo.InvariantCulture);
            }
            catch (InvalidCastException e)
            {
                throw ConvertFailed(value.GetType(), typeof(string), e);
            }
        }

        #region Encoding

        private static Encoding ConvertToEncodingImpl(string keyword, string value)
        {
            try
            {
                return Encoding.GetEncoding(value);
            }
            catch (ArgumentException)
            {
                // string values must be valid
                throw InvalidConnectionOptionValue(keyword, value);
            }
        }

        public static string EncodingToString(Encoding value)
        {
            return value.EncodingName;
        }
        
        public static Encoding ConvertToEncoding(string keyword, object value)
        {
            Debug.Assert(null != value, "ConvertToEncoding(null)");
            return value switch
            {
                string sValue => ConvertToEncodingImpl(keyword, sValue),
                Encoding encoding => encoding,
                _ => throw ConvertFailed(value.GetType(), typeof(Encoding), null)
            };
        }
        
        #endregion
        
        #region StringTrimmingOption

        private const string StringTrimmingOptionNoneString = "None";
        private const string StringTrimmingOptionTrimString = "Trim";
        private const string StringTrimmingOptionTrimStartString = "TrimStart";
        private const string StringTrimmingOptionTrimEndString = "TrimEnd";

        private static bool TryConvertToStringTrimmingOption(string value, out StringTrimmingOption result)
        {
            Debug.Assert(Enum.GetNames(typeof(StringTrimmingOption)).Length == 4, "StringTrimmingOption enum has changed, update needed");
            Debug.Assert(null != value, "TryConvertToStringTrimmingOption(null,...)");

            if (StringComparer.OrdinalIgnoreCase.Equals(value, StringTrimmingOptionNoneString))
            {
                result = StringTrimmingOption.None;
                return true;
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(value, StringTrimmingOptionTrimString))
            {
                result = StringTrimmingOption.Trim;
                return true;
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(value, StringTrimmingOptionTrimStartString))
            {
                result = StringTrimmingOption.TrimStart;
                return true;
            }

            if (StringComparer.OrdinalIgnoreCase.Equals(value, StringTrimmingOptionTrimEndString))
            {
                result = StringTrimmingOption.TrimEnd;
                return true;
            }
            
            result = StringTrimmingOption.None;
            return false;
        }

        internal static bool IsValidStringTrimmingOptionValue(StringTrimmingOption value)
        {
            Debug.Assert(Enum.GetNames(typeof(StringTrimmingOption)).Length == 4, "StringTrimmingOption enum has changed, update needed");
            return value == StringTrimmingOption.None || value == StringTrimmingOption.Trim || value == StringTrimmingOption.TrimStart || value == StringTrimmingOption.TrimEnd;
        }
        
        public static string StringTrimmingOptionToString(StringTrimmingOption value)
        {
            Debug.Assert(IsValidStringTrimmingOptionValue(value));
            
            switch (value)
            {
                case StringTrimmingOption.None:
                    return StringTrimmingOptionNoneString;
                case StringTrimmingOption.Trim:
                    return  StringTrimmingOptionTrimString;
                case StringTrimmingOption.TrimStart:
                    return StringTrimmingOptionTrimStartString;
                case StringTrimmingOption.TrimEnd:
                    return StringTrimmingOptionTrimEndString;
                default:
                    throw new ArgumentOutOfRangeException(nameof(value), value, null);
            }
        }

        /// <summary>
        /// This method attempts to convert the given value to a StringTrimmingOption enum. The algorithm is:
        /// * if the value is from type string, it will be matched against StringTrimmingOption enum names only, using ordinal, case-insensitive comparer
        /// * if the value is from type StringTrimmingOption, it will be used as is
        /// * if the value is from integral type (SByte, Int16, Int32, Int64, Byte, UInt16, UInt32, or UInt64), it will be converted to enum
        /// * if the value is another enum or any other type, it will be blocked with an appropriate ArgumentException
        /// 
        /// in any case above, if the converted value is out of valid range, the method raises ArgumentOutOfRangeException.
        /// </summary>
        /// <returns>StringTrimmingOption value in the valid range</returns>
        public static StringTrimmingOption ConvertToStringTrimmingOption(string keyword, object value)
        {
            Debug.Assert(null != value, "ConvertToStringTrimmingOption(null)");
            if (value is string sValue)
            {
                // We could use Enum.TryParse<StringTrimmingOption> here, but it accepts value combinations like
                // "ReadOnly, ReadWrite" which are unwelcome here
                // Also, Enum.TryParse is 100x slower than plain StringComparer.OrdinalIgnoreCase.Equals method.

                if (TryConvertToStringTrimmingOption(sValue, out var result))
                {
                    return result;
                }

                // try again after remove leading & trailing whitespaces.
                sValue = sValue.Trim();
                if (TryConvertToStringTrimmingOption(sValue, out result))
                {
                    return result;
                }

                // string values must be valid
                throw InvalidConnectionOptionValue(keyword, sValue);
            }

            // the value is not string, try other options
            StringTrimmingOption eValue;

            if (value is StringTrimmingOption option)
            {
                // quick path for the most common case
                eValue = option;
            }
            else if (value.GetType().IsEnum)
            {
                // explicitly block scenarios in which user tries to use wrong enum types
                throw ConvertFailed(value.GetType(), typeof(StringTrimmingOption), null);
            }
            else
            {
                try
                {
                    // Enum.ToObject allows only integral and enum values (enums are blocked above), rasing ArgumentException for the rest
                    eValue = (StringTrimmingOption)Enum.ToObject(typeof(StringTrimmingOption), value);
                }
                catch (ArgumentException e)
                {
                    // to be consistent with the messages we send in case of wrong type usage, replace 
                    // the error with our exception, and keep the original one as inner one for troubleshooting
                    throw ConvertFailed(value.GetType(), typeof(StringTrimmingOption), e);
                }
            }

            // ensure value is in valid range
            if (!IsValidStringTrimmingOptionValue(eValue))
            {
                throw InvalidEnumerationValue(typeof(StringTrimmingOption), (int)eValue);
            }
                
            return eValue;
        }

        #endregion

        internal static void CheckArgumentNull<T>(T argumentValue, string argumentName) where T : class
        {
            if (null == argumentValue)
            {
                throw new ArgumentNullException(argumentName);
            }
        }
        
        private static Exception ConvertFailed(Type getType, Type type, Exception innerException)
        {
            return new ArgumentException($"Cannot convert object of type '{getType}' to object of type '{type}'", innerException);
        }

        private static ArgumentException InvalidConnectionOptionValue(string keyword, string value)
        {
            return new ArgumentException($"Invalid value '{value}' for key '{keyword}'.");
        }
        
        internal static ArgumentOutOfRangeException InvalidEnumerationValue(Type type, int value)
        {
            return new ArgumentOutOfRangeException(type.Name, $"The {type.Name} enumeration value, {value.ToString(CultureInfo.InvariantCulture)}, is not valid.");
        }

        internal static ArgumentException KeywordNotSupported(string keyword)
        {
            return new ArgumentException($"Keyword not supported: '{keyword}'.");
        }
    }
}