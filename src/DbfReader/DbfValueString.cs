using System.IO;

namespace DbfReader
{
    public class DbfValueString : DbfValue<string>
    {
        public DbfValueString(int length) : base(length)
        {
        }

        public override void Read(BinaryReader binaryReader)
        {
            var chars = binaryReader.ReadChars(Length);
            if (chars[0] == '\0') {
                Value = null; 
            }
            else {
                var value = new string(chars);
                Value = value.TrimEnd('\0', ' ');
            }
        }
    }
}