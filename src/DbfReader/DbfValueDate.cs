using System;
using System.Globalization;
using System.IO;

namespace DbfReader
{
    public class DbfValueDate : IDbfValue
    {
        public void Read(BinaryReader binaryReader)
        {
            var value = new string(binaryReader.ReadChars(8));

            if (!string.IsNullOrWhiteSpace(value))
            {
                Value = DateTime.ParseExact(value, "yyyyMMdd", null, DateTimeStyles.AllowLeadingWhite | DateTimeStyles.AllowTrailingWhite);
            }
        }

        public DateTime Value { get; private set; }
    }
}