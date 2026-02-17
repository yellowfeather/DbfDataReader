using System;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;

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
            
            var baseFileName = QueryParser.GetFileName(CommandText);
            var fileNames = new[]
            {
                Path.Combine(folder, $"{baseFileName}.dbf"),
                Path.Combine(folder, $"{baseFileName}")
            };

            var foundFile = fileNames.FirstOrDefault(File.Exists);

            if (foundFile is null)
            {
                var builder = new StringBuilder();
                builder.AppendLine("Unable to find any of the following files:");
                for(var i = 0; i < fileNames.Length; i++)
                {
                    if (i + 1 != fileNames.Length)
                    {
                        builder.AppendLine(fileNames[i]);
                    }
                    else {
                        builder.Append(fileNames[i]);
                    }
                }

                throw new FileNotFoundException(builder.ToString());
            }

            var options = dbfDbConnection.Options;
            if (options is null)
            {
                throw new InvalidOperationException("Invalid Configuration");
            }

            return new DbfDataReader(foundFile, options);
        }
    }
}
