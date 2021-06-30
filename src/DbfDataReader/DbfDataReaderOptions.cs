using System.Text;

namespace DbfDataReader
{
    public class DbfDataReaderOptions
    {
        public DbfDataReaderOptions()
        {
            SkipDeletedRecords = false;
            Encoding = null;
        }

        public bool SkipDeletedRecords { get; set; }
        public Encoding Encoding { get; set; }
    }
}