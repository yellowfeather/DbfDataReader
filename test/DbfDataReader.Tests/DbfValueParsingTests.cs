using System;
using System.Data.SqlTypes;
using System.Globalization;
using System.Linq;
using System.Text;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    public class DbfValueParsingTests
    {
        static DbfValueParsingTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        [Theory]
        [InlineData(StringTrimmingOption.None)]
        [InlineData(StringTrimmingOption.Trim)]
        [InlineData(StringTrimmingOption.TrimStart)]
        [InlineData(StringTrimmingOption.TrimEnd)]
        public void String_values_trim_exactly_like_the_string_based_implementation(StringTrimmingOption stringTrimming)
        {
            var inputs = new[]
            {
                "abc  ", "  abc", "  abc  ", "     ", "a b c ", "abc\0\0", "abc \0",
                "a\0b", " \0abc", "x", "µöü  ", "héllo  "
            };

            var encodings = new[]
            {
                Encoding.GetEncoding(1252), // single byte: trimmed as bytes
                Encoding.UTF8,              // multi byte, ASCII compatible: trimmed as bytes
                Encoding.Unicode            // not ASCII compatible: decoded then trimmed
            };

            foreach (var encoding in encodings)
            {
                foreach (var input in inputs)
                {
                    var bytes = encoding.GetBytes(input);
                    var dbfValue = new DbfValueString(0, bytes.Length, encoding, stringTrimming);

                    dbfValue.Read(bytes);

                    dbfValue.Value.ShouldBe(LegacyTrim(input, stringTrimming),
                        $"input '{input}' with encoding {encoding.WebName}");
                }
            }
        }

        [Fact]
        public void String_values_are_null_when_the_first_byte_is_nul()
        {
            var dbfValue = new DbfValueString(0, 4, Encoding.GetEncoding(1252));

            dbfValue.Read(new byte[] { 0x00, (byte) 'a', (byte) 'b', (byte) 'c' });

            dbfValue.Value.ShouldBeNull();
            dbfValue.IsNull.ShouldBeTrue();
        }

        [Theory]
        [InlineData("   12.34", "12.34")]
        [InlineData("-0.5", "-0.5")]
        [InlineData(".5", "0.5")]
        [InlineData("12.", "12")]
        [InlineData("1.2E+2", "120")]
        [InlineData("        ", null)]
        [InlineData("******", null)]
        [InlineData("12x", null)]
        [InlineData("1,234", null)]
        public void Decimal_values_parse_from_the_record_buffer(string text, string expected)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            var dbfValue = new DbfValueDecimal(0, bytes.Length, 2);

            dbfValue.Read(bytes);

            if (expected is null)
            {
                dbfValue.Value.ShouldBeNull();
                dbfValue.IsNull.ShouldBeTrue();
            }
            else
            {
                dbfValue.Value.ShouldBe(decimal.Parse(expected, CultureInfo.InvariantCulture));
                dbfValue.IsNull.ShouldBeFalse();
            }
        }

        [Theory]
        [InlineData("  42  ", 42)]
        [InlineData("-7", -7)]
        [InlineData("3.5", null)]
        [InlineData("99999999999", null)]
        [InlineData("   ", null)]
        [InlineData("*", null)]
        public void Int_values_parse_from_the_record_buffer(string text, int? expected)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            var dbfValue = new DbfValueInt(0, bytes.Length);

            dbfValue.Read(bytes);

            dbfValue.Value.ShouldBe(expected);
        }

        [Theory]
        [InlineData("  9999999999", 9999999999L)]
        [InlineData("-42", -42L)]
        [InlineData("abc", null)]
        [InlineData("          ", null)]
        public void Int64_values_parse_from_the_record_buffer(string text, long? expected)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            var dbfValue = new DbfValueInt64(0, bytes.Length);

            dbfValue.Read(bytes);

            dbfValue.Value.ShouldBe(expected);
        }

        [Theory]
        [InlineData(" 1.25", 1.25f)]
        [InlineData("-.5", -0.5f)]
        [InlineData("1E3", 1000f)]
        [InlineData("     ", null)]
        [InlineData("**.**", null)]
        public void Float_values_parse_from_the_record_buffer(string text, float? expected)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            var dbfValue = new DbfValueFloat(0, bytes.Length, 2);

            dbfValue.Read(bytes);

            dbfValue.Value.ShouldBe(expected);
        }

        [Fact]
        public void Numeric_bytes_above_ascii_fail_to_parse_like_ascii_decoding_did()
        {
            // Encoding.ASCII decodes 0xB5 as '?', so the legacy path never parsed
            // such content; the span path must reject it the same way
            var dbfValue = new DbfValueDecimal(0, 2, 2);

            dbfValue.Read(new byte[] { 0xB5, (byte) '1' });

            dbfValue.Value.ShouldBeNull();
        }

        [Theory]
        [InlineData("20260706", "2026-07-06")]
        [InlineData("        ", null)]
        [InlineData("00000000", null)]
        [InlineData("20261340", null)]
        [InlineData("ABCDEFGH", null)]
        [InlineData("2026\0\0\0\0", null)]
        public void Date_values_parse_from_the_record_buffer(string text, string expected)
        {
            var bytes = Encoding.ASCII.GetBytes(text);
            var dbfValue = new DbfValueDate(0, bytes.Length);

            dbfValue.Read(bytes);

            if (expected is null)
            {
                dbfValue.Value.ShouldBeNull();
            }
            else
            {
                dbfValue.Value.ShouldBe(DateTime.ParseExact(expected, "yyyy-MM-dd", CultureInfo.InvariantCulture));
            }
        }

        [Fact]
        public void DbfValueNull_is_always_null()
        {
            IDbfValue dbfValue = new DbfValueNull(0, 1);

            dbfValue.IsNull.ShouldBeTrue();
            dbfValue.GetValue().ShouldBeNull();
        }

        [Theory]
        [InlineData("../../../../fixtures/dbase_03.dbf")]
        [InlineData("../../../../fixtures/dbase_30.dbf")]
        [InlineData("../../../../fixtures/dbase_8b.dbf")]
        public void Typed_getters_and_IsDBNull_match_boxed_values_for_every_cell(string fixturePath)
        {
            var options = new DbfDataReaderOptions { SkipDeletedRecords = false };
            using var reader = new DbfDataReader(fixturePath, options);

            while (reader.Read())
            {
                for (var ordinal = 0; ordinal < reader.FieldCount; ordinal++)
                {
                    var boxed = reader.GetValue(ordinal);
                    reader.IsDBNull(ordinal).ShouldBe(boxed == null);

                    var typeCode = GetUnderlyingTypeCode(reader.GetFieldType(ordinal));
                    if (boxed != null)
                    {
                        ReadTypedField(reader, ordinal, typeCode).ShouldBe(boxed);
                    }
                    else if (typeCode == TypeCode.String)
                    {
                        reader.GetString(ordinal).ShouldBeNull();
                    }
                    else if (StructGetterTypeCodes.Contains(typeCode))
                    {
                        Should.Throw<SqlNullValueException>(() => ReadTypedField(reader, ordinal, typeCode));
                    }
                }
            }
        }

        private static readonly TypeCode[] StructGetterTypeCodes =
        {
            TypeCode.Boolean, TypeCode.Int32, TypeCode.Int64, TypeCode.Decimal,
            TypeCode.DateTime, TypeCode.Single, TypeCode.Double
        };

        // GetFieldType reports nullable types (int?, decimal?, ...); unwrap them so
        // value columns route to the typed getters instead of TypeCode.Object
        private static TypeCode GetUnderlyingTypeCode(Type fieldType)
        {
            if (fieldType is null) return TypeCode.Empty;
            return Type.GetTypeCode(Nullable.GetUnderlyingType(fieldType) ?? fieldType);
        }

        private static object ReadTypedField(DbfDataReader reader, int ordinal, TypeCode typeCode)
        {
            return typeCode switch
            {
                TypeCode.Boolean => reader.GetBoolean(ordinal),
                TypeCode.Int32 => reader.GetInt32(ordinal),
                TypeCode.Int64 => reader.GetInt64(ordinal),
                TypeCode.Decimal => reader.GetDecimal(ordinal),
                TypeCode.DateTime => reader.GetDateTime(ordinal),
                TypeCode.Single => reader.GetFloat(ordinal),
                TypeCode.Double => reader.GetDouble(ordinal),
                TypeCode.String => reader.GetString(ordinal),
                _ => reader.GetValue(ordinal)
            };
        }

        private static string LegacyTrim(string value, StringTrimmingOption stringTrimming)
        {
            var trimmedNull = value.Trim('\0');

            return stringTrimming switch
            {
                StringTrimmingOption.None => trimmedNull,
                StringTrimmingOption.Trim => trimmedNull.Trim(' '),
                StringTrimmingOption.TrimStart => trimmedNull.TrimStart(' '),
                StringTrimmingOption.TrimEnd => trimmedNull.TrimEnd(' '),
                _ => trimmedNull.Trim(' ')
            };
        }
    }
}
