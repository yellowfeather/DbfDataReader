using System.Text;

namespace DbfDataReader.Cdx
{
    public class CdxKeyEntry
    {
        private readonly Encoding _encoding;
        private string _key;

        internal CdxKeyEntry(byte[] keyBytes, int recordNumber, Encoding encoding)
        {
            KeyBytes = keyBytes;
            RecordNumber = recordNumber;
            _encoding = encoding;
        }

        // the key value with its trailing index padding removed
        public byte[] KeyBytes { get; }

        // one-based DBF record number, as stored in the index
        public int RecordNumber { get; }

        // zero-based record index for use with DbfTable.Seek and DbfDataReader.Seek
        public int RecordIndex => RecordNumber - 1;

        public string Key => _key ??= _encoding.GetString(KeyBytes, 0, KeyBytes.Length);

        public override string ToString()
        {
            return Key;
        }
    }
}
