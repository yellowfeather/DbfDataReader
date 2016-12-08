using System;
using System.IO;

namespace DbfReader
{
    public class DbfValueBoolean : DbfValue<bool?>
    {
        public override void Read(BinaryReader binaryReader)
        {
            Value = binaryReader.ReadBoolean();
        }
    }
}