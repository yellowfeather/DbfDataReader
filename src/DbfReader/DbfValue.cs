using System.IO;

namespace DbfReader
{
    public abstract class DbfValue<T> : IDbfValue
    {
        public abstract void Read(BinaryReader binaryReader);

        public override string ToString()
        {
            return Value.ToString();
        }

        public T Value { get; protected set; }
    }
}