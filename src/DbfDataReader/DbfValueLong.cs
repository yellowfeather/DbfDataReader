using System;

namespace DbfDataReader
{
    public class DbfValueLong : DbfValue<long?>
    {
        public DbfValueLong(int start, int length) : base(start, length)
        {
        }

        public override void Read(ReadOnlySpan<byte> bytes)
        {
            Value = BitConverter.ToInt32(bytes);
        }
    }
}