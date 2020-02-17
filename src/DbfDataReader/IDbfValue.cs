using System;
using System.IO;

namespace DbfDataReader
{
    public interface IDbfValue
    {
        void Read(BinaryReader binaryReader);

        object GetValue();

        Type GetFieldType();
    }
}