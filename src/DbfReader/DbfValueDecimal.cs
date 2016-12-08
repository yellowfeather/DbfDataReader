using System.Globalization;
using System.IO;

namespace DbfReader
{
    public class DbfValueDecimal : DbfValue<decimal?>
    {
        private static readonly NumberFormatInfo DecimalNumberFormat = new NumberFormatInfo { NumberDecimalSeparator = "." };

        public int Length { get; }
        public int DecimalCount { get; }

        public DbfValueDecimal(int length, int decimalCount)
        {
            Length = length;
            DecimalCount = decimalCount;
        }

        public override void Read(BinaryReader binaryReader)
        {
            if (binaryReader.PeekChar() == '\0')
            {
                binaryReader.ReadBytes(Length);
                Value = null;
            }
            else
            {
                var stringValue = new string(binaryReader.ReadChars(Length));

                decimal value;
                if (decimal.TryParse(stringValue, NumberStyles.Float | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, DecimalNumberFormat, out value))
                {
                    Value = value;
                }
                else
                {
                    Value = null;
                }
            }
        }
    }
}