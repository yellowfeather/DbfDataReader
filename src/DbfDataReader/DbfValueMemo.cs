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
#if NET48
                var startBlock = BitConverter.ToUInt32(bytes.ToArray(), 0);
#else
                var startBlock = BitConverter.ToUInt32(bytes);
#endif
                Value = _memo.Get(startBlock);
            }
            else
            {
#if NET48
                var value = Encoding.GetString(bytes.ToArray());
#else
                var value = Encoding.GetString(bytes);
#endif
                if (string.IsNullOrWhiteSpace(value))
                {
                    Value = string.Empty;
                }
                else
                {
                    var startBlock = long.Parse(value);
                    Value = _memo?.Get(startBlock);
                }
            }
        }
    }
}