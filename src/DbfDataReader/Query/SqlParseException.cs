using System;

namespace DbfDataReader.Query
{
    // derives from ArgumentException so callers that caught the previous parser's
    // ArgumentException for invalid command text keep working
    public class SqlParseException : ArgumentException
    {
        public SqlParseException(string message, int position)
            : base(message)
        {
            Position = position;
        }

        // zero-based character position within the command text
        public int Position { get; }
    }
}
