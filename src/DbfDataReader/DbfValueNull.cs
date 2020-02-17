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

        public void Read(BinaryReader binaryReader)
        {
            binaryReader.ReadBytes(Length);
        }

        public T GetValue<T>()
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return string.Empty;
        }

        public Type GetFieldType()
        {
            return null;
        }
    }
}