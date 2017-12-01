using System.IO;

namespace DbfDataReader
{
    public class DbfValueMemo : DbfValueString
    {
        private readonly DbfMemo _memo;

        public DbfValueMemo(int length, DbfMemo memo)
            : base(length)
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
                var value = new string(binaryReader.ReadChars(Length));
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