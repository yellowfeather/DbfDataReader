using System;

namespace DbfDataReader
{
    // DateTime values are encoded as two 32 bits numbers. The high word is
    // the date, encoded as the number of days since the beginning of the
    // Julian period (Jan 1, 4713BC), and the low word is the time, encoded
    // as the number of milliseconds since midnight.
    public class DbfValueDateTime : DbfValue<DateTime?>
    {
        public DbfValueDateTime(int start, int length) : base(start, length)
        {
        }

        public override void Read(ReadOnlySpan<byte> bytes)
        {
            var datePart = BitConverter.ToInt32(bytes);
            var timePart = BitConverter.ToInt32(bytes[4..]);

            if (datePart == 0 && timePart == 0)
            {
                Value = null;
                return;
            }

            const int numberOfDaysSinceBeginJulianPeriod = 1721426;
            
            Value = new DateTime(1, 1, 1)
                .AddDays(datePart - numberOfDaysSinceBeginJulianPeriod)
                .AddMilliseconds(timePart);
        }
    }
}