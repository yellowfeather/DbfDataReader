using System;
using System.Text;

namespace DbfDataReader
{
    public class DbfValueMemo : DbfValueString
    {
        private readonly DbfMemo _memo;

        public DbfValueMemo(int start, int length, DbfMemo memo, Encoding encoding)
            : base(start, length, encoding)
        {
            _memo = memo;
        }

        public override void Read(ReadOnlySpan<byte> bytes)
        {
            if (Length == 4)
            {
                var startBlock = BitConverter.ToUInt32(bytes);
                Value = _memo?.Get(startBlock);
            }
            else
            {
                var value = Encoding.GetString(bytes);

                if (string.IsNullOrWhiteSpace(value))
                {
                    Value = string.Empty;
                }
                else if (long.TryParse(value, out long startBlock))
                {
                    Value = _memo?.Get(startBlock);
                }
                else
                {
                    Value = string.Empty;
                }
            }
        }
    }
}
