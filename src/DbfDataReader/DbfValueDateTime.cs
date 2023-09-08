using System;
using System.IO;

namespace DbfDataReader
{
    public class DbfValueDateTime : DbfValue<DateTime?>
    {
        public DbfValueDateTime(int start, int length) : base(start, length)
        {
        }

        public override void Read(ReadOnlySpan<byte> bytes)
        {
            if (bytes[0] == '\0')
            {
                Value = null;
            }
            else
            {
                var datePart = BitConverter.ToInt32(bytes);
                var timePart = BitConverter.ToInt32(bytes[4..]);
                Value = new DateTime(1, 1, 1).AddDays(datePart-1721426).AddMilliseconds(timePart);
            }
        }
    }
}