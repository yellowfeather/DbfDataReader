using System.IO;
using System.Text;

namespace DbfDataReader
{
    public class DbfValueMemo : DbfValueString
    {
        private readonly DbfMemo _memo;

        public DbfValueMemo(int length, DbfMemo memo, Encoding encoding)
            : base(length, encoding)
        {
            _memo = memo;
        }

        public override void Read(BinaryReader binaryReader)
        {
            if (Length == 4)
            {
                var startBlock = binaryReader.ReadUInt32();
                Value = _memo.Get(startBlock);
            }
            else
            {
                var value = binaryReader.ReadString(Length, CurrentEncoding);
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