using System;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Text;

namespace DbfDataReader
{
    public class DbfDbConnection : DbConnection
    {
        private string _database = string.Empty;
        private ConnectionState _state  = ConnectionState.Closed;

        public DbfDbConnection()
        {
            DataSource = string.Empty;
            ServerVersion = string.Empty;
        }

        public DbfDbConnection(string dataSource, string serverVersion)
        {
            DataSource = dataSource;
            ServerVersion = serverVersion;
        }

        public DbfDataReaderOptions Options { get; private set; } = new DbfDataReaderOptions();

        public override string ConnectionString { get; set; }
        public override string DataSource { get; }
        public override string ServerVersion { get; }
        
        public override ConnectionState State => _state;
        public override string Database => _database;
        
        public override void ChangeDatabase(string databaseName)
        {
            if (!Directory.Exists(databaseName))
            {
                throw new DirectoryNotFoundException(databaseName);
            }
            
            _database = databaseName;
        }

        public override void Close()
        {
            _state = ConnectionState.Closed;
        }

        public override void Open()
        {
            var builder = new DbfDbConnectionStringBuilder(ConnectionString);

            ChangeDatabase(builder.Folder);

            var options = new DbfDataReaderOptions();
            
            if (builder.Encoding is { } encoding) 
            {
                options.Encoding = Encoding.GetEncoding(encoding);
            }

            if (builder.ReadFloatsAsDecimals is var readFloatsAsDecimals)
            {
                options.ReadFloatsAsDecimals = readFloatsAsDecimals;
            }

            if (builder.SkipDeletedRecords is var skipDeletedRecords) 
            {
                options.SkipDeletedRecords = skipDeletedRecords;
            }

            if (builder.StringTrimming is var stringTrimming)
            {
                options.StringTrimming = stringTrimming;
            }

            Options = options;
            _state = ConnectionState.Open;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            throw new NotImplementedException();
        }

        protected override DbCommand CreateDbCommand()
        {
            return new DbfDbCommand
            {
                Connection = this,
            };
        }
    }
}
