using System;
using System.IO;

namespace DbfDataReader
{
    public class DbfValueDateTime : DbfValue<DateTime?>
    {
        public DbfValueDateTime(int length) : base(length)
        {
        }

        public override void Read(BinaryReader binaryReader)
        {
            var bytes = binaryReader.ReadBytes(8);
            if (bytes[0] == '\0')
            {
                Value = null;
            }
            else
            {
                var datePart = BitConverter.ToInt32(bytes, 0);
                var timePart = BitConverter.ToInt32(bytes, 4);
                Value = new DateTime(1, 1, 1).AddDays(datePart).Subtract(TimeSpan.FromDays(1721426))
                    .AddMilliseconds(timePart);
            }
        }
    }
}