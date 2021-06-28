using System;

namespace DbfDataReader
{
    public abstract class DbfValue<T> : IDbfValue
    {
        protected DbfValue(int offset, int length)
        {
            Start = offset;
            Length = length;
        }

        public int Start { get; }

        public int Length { get; }

        public T Value { get; protected set; }

        public abstract void Read(ReadOnlySpan<byte> bytes);

        public object GetValue()
        {
            return Value;
        }

        public override string ToString()
        {
            return Value.ToString();
        }

        public Type GetFieldType()
        {
            return typeof(T);
        }
    }
}