using System;

namespace DbfDataReader
{
    public interface IDbfValue
    {
        int Start { get; }

        int Length { get; }

        void Read(ReadOnlySpan<byte> bytes);

        object GetValue();

        Type GetFieldType();
    }
}