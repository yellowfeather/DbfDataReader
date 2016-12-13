using System;
using System.IO;

namespace DbfDataReader
{
    public abstract class DbfValue<T> : IDbfValue
    {
        protected DbfValue(int length)
        {
            Length = length;
        }

        public int Length { get; }


        public abstract void Read(BinaryReader binaryReader);

        public override string ToString()
        {
            return Value.ToString();
        }

        public object GetValue()
        {
            return Value;
        }

        public T Value { get; protected set; }
    }
}