using System;

namespace DbfDataReader
{
    public class DbfValueBoolean : DbfValue<bool?>
    {
        public DbfValueBoolean(int start, int length) : base(start, length)
        {
        }

        public override void Read(ReadOnlySpan<byte> bytes)
        {
            var value = bytes[0];
            if (value == 'Y' || value == 'y' || value == 'T' || value == 't')
                Value = true;
            else if (value == 'N' || value == 'n' || value == 'F' || value == 'f')
                Value = false;
            else
                Value = null;
        }

        public override string ToString()
        {
            return !Value.HasValue ? string.Empty : Value.Value ? "T" : "F";
        }
    }
}