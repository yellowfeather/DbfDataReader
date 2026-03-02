using System;
using System.Data;
using System.Data.Common;
using System.IO;

namespace DbfDataReader
{
    public class DbfDbCommand : DbCommand
    {
        public override string CommandText { get; set; }
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; }
        protected override DbTransaction DbTransaction { get; set; }

        public override void Cancel()
        {
            throw new NotImplementedException();
        }

        public override int ExecuteNonQuery()
        {
            throw new NotImplementedException();
        }

        public override object ExecuteScalar()
        {
            throw new NotImplementedException();
        }

        public override void Prepare()
        {
            throw new NotImplementedException();
        }

        protected override DbParameter CreateDbParameter()
        {
            throw new NotImplementedException();
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            var dbfDbConnection = Connection as DbfDbConnection;
            if (dbfDbConnection is null)
            {
                throw new InvalidOperationException($"{nameof(DbfDbConnection)} is not available");
            }

            var folder = dbfDbConnection.Database;
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new DirectoryNotFoundException("No folder was specified for the Dbf files.");
            }
            
            if (!Directory.Exists(folder))
            {
                throw new DirectoryNotFoundException($"The specified folder does not exist: {folder}");
            }
            
            var fileName = QueryParser.Parse(CommandText);
            var filePath = GetFilePath(folder, fileName);

            var options = dbfDbConnection.Options;
            return new DbfDataReader(filePath, options);
        }

        private static string GetFilePath(string folder, string fileName)
        {
            var filePath = Path.Combine(folder, $"{fileName}");
            if (File.Exists(filePath))
            {
                return filePath;
            }
            
            filePath = Path.ChangeExtension(filePath, ".dbf");
            if (File.Exists(filePath))
            {
                return filePath;
            }

            filePath = Path.ChangeExtension(filePath, ".DBF");
            if (File.Exists(filePath))
            {
                return filePath;
            }

            throw new FileNotFoundException(filePath);
        }
    }
}
