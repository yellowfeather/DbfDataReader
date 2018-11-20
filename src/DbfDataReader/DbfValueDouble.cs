using System;
using System.Globalization;
using System.IO;

namespace DbfDataReader
{
    public class DbfValueDouble : DbfValue<double?>
    {
        private static readonly NumberFormatInfo _doubleNumberFormat = new NumberFormatInfo
            {NumberDecimalSeparator = "."};

        [Obsolete("This constructor should no longer be used. Use DbfValueDouble(System.Int32, System.Int32) instead.")]
        public DbfValueDouble(int length) : this(length, 0)
        {
        }

        public DbfValueDouble(int length, int decimalCount) : base(length)
        {
            DecimalCount = decimalCount;
        }

        public int DecimalCount { get; }

        public override void Read(BinaryReader binaryReader)
        {
            var bytes = binaryReader.ReadBytes(Length);
            Value = BitConverter.ToDouble(bytes, 0);
        }

        public override string ToString()
        {
            var format = DecimalCount != 0
                ? $"0.{new string('0', DecimalCount)}"
                : null;

            return Value?.ToString(format, NumberFormatInfo.CurrentInfo) ?? string.Empty;
        }
    }
}