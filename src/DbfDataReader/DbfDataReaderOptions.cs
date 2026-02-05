using System.Text;

namespace DbfDataReader
{
    public class DbfDataReaderOptions
    {
        public DbfDataReaderOptions()
        {
            SkipDeletedRecords = false;
            Encoding = null;
            StringTrimming = StringTrimmingOption.Trim;
            ReadFloatsAsDecimals = false;
        }

        public bool SkipDeletedRecords { get; set; }
        public Encoding Encoding { get; set; }
        public StringTrimmingOption StringTrimming { get; set; }
        public bool ReadFloatsAsDecimals { get; set; }
    }

    public enum StringTrimmingOption
    {
        None,
        Trim,
        TrimStart,
        TrimEnd,
    }
}