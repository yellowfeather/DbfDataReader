using System.IO;

namespace DbfReader
{
    public interface IDbfValue
    {
        void Read(BinaryReader binaryReader);
    }
}