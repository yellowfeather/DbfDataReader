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
            UseIndexes = true;
        }

        public bool SkipDeletedRecords { get; set; }
        public Encoding Encoding { get; set; }
        public StringTrimmingOption StringTrimming { get; set; }
        public bool ReadFloatsAsDecimals { get; set; }

        // when true, queries use a sidecar .cdx compound index automatically where one
        // can serve the WHERE or ORDER BY clause
        public bool UseIndexes { get; set; }
    }

    public enum StringTrimmingOption
    {
        None,
        Trim,
        TrimStart,
        TrimEnd,
    }
}