using System.Text;

namespace DbfDataReader
{
    public class DbfDataReaderOptions
    {
        public DbfDataReaderOptions()
        {
            SkipDeletedRecords = false;
            Encoding = EncodingProvider.GetEncoding(1252);
        }

        public bool SkipDeletedRecords { get; set; }
        public Encoding Encoding { get; set; }
    }
}