using System;
using System.IO;

namespace DbfDataReader
{
    public class DbfValueNull : IDbfValue
    {
        public DbfValueNull(int length)
        {
            Length = length;
        }

        public int Length { get; }

        public object GetValue()
        {
            return null;
        }

        public T GetValue<T>()
        {
            throw new NotImplementedException();
        }

        public void Read(BinaryReader binaryReader)
        {
            // binaryReader.ReadBytes(Length);
            binaryReader.ReadByte();
        }

        public override string ToString()
        {
            return string.Empty;
        }
    }
}