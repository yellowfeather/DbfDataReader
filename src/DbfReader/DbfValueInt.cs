using System;
using System.Globalization;
using System.IO;

namespace DbfReader
{
    public class DbfValueInt : DbfValue<int?>
    {
        private static readonly NumberFormatInfo IntNumberFormat = new NumberFormatInfo();

        public int Length { get; }

        public DbfValueInt(int length)
        {
            Length = length;
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

                int value;
                if (int.TryParse(stringValue, NumberStyles.Integer | NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite, IntNumberFormat, out value))
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