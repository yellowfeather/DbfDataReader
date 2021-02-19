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
#if NET48
            Value = BitConverter.ToInt32(bytes.ToArray(), 0);
#else
            Value = BitConverter.ToInt32(bytes);
#endif
        }
    }
}