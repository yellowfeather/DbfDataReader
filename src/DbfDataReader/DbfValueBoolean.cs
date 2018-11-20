using System.IO;

namespace DbfDataReader
{
    public class DbfValueBoolean : DbfValue<bool?>
    {
        public DbfValueBoolean(int length) : base(length)
        {
        }

        public override void Read(BinaryReader binaryReader)
        {
            var value = binaryReader.ReadChar();
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