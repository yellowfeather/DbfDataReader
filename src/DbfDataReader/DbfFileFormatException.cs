using System;

namespace DbfDataReader
{
    public class DbfFileFormatException : Exception
    {
        public DbfFileFormatException()
        {
        }

        public DbfFileFormatException(string message)
            : base(message)
        {
        }

        public DbfFileFormatException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
