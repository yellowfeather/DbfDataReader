namespace DbfDataReader
{
    /// <summary>
    ///     Class <c>DbfRecordConfiguration</c> provides a configuration for the <c>DbfRecord</c> class
    /// </summary>
    public class DbfRecordConfiguration
    {
        /// <summary>
        ///     Configuation that denotes floats from the database should be read as <c>decimal</c> in C#. 
        ///     This is to mitigate precision issues where a number in the database is labeled as a float, but is greater than the size of a float in C#
        /// </summary>
        public bool ReadFloatsAsDecimals { get; set; }

        /// <summary>
        ///     Provides a default configuration
        /// </summary>
        /// <returns>A <c>DbfRecordConfigration</c> object containing the default configuration</returns>
        public static DbfRecordConfiguration GetDefault()
        {
            return new DbfRecordConfiguration { 
                ReadFloatsAsDecimals = false 
            };
        }
    }
}
