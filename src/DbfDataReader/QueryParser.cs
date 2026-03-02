using System;
using System.Text.RegularExpressions;

namespace DbfDataReader
{
    internal static class QueryParser
    {
        // Match forms like:
        //   SELECT * FROM file.dbf
        //   SELECT * FROM "file with spaces.dbf"
        //   SELECT * FROM [file with spaces.dbf]
        private static readonly string Regex = @"^\s*SELECT\s*\*\s*FROM\s*(?:\""(?<FileName>[^\""]+)\""|\[(?<FileName>[^\]]+)\]|(?<FileName>[\w\.]+))\s*;?\s*$";
        private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.ExplicitCapture;
        private static readonly Regex FileNameRegex = new Regex(Regex, Options, TimeSpan.FromMilliseconds(100));

        internal static string Parse(string commandText)
        {
            if (commandText == null)
            {
                throw new ArgumentNullException(nameof(commandText));
            }

            var match = FileNameRegex.Match(commandText);
            if (!match.Success)
            {
                throw new ArgumentException($"Invalid command text: '{commandText}'. Must be in the format 'SELECT * FROM <FILENAME>'.", nameof(commandText));
            }

            return match.Groups["FileName"].Value;
        }
    }
}