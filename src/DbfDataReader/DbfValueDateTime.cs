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
#if NET48
                var datePart = BitConverter.ToInt32(bytes.Slice(0, 4).ToArray(), 0);
                var timePart = BitConverter.ToInt32(bytes.Slice(4, 4).ToArray(), 0);
#else
                var datePart = BitConverter.ToInt32(bytes);
                var timePart = BitConverter.ToInt32(bytes[4..]);
#endif
                Value = new DateTime(1, 1, 1).AddDays(datePart).Subtract(TimeSpan.FromDays(1721426))
                    .AddMilliseconds(timePart);
            }
        }
    }
}