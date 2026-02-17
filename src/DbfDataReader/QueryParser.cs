using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace DbfDataReader
{
    internal static class QueryParser
    {

        internal static string GetFileName(string commandText)
        {
            var match = FileNameRegex.Match(commandText);
            if (!match.Success)
            {
                throw new ArgumentException("The command text must be in the format 'SELECT * FROM <FILENAME>'.", nameof(commandText));
            }
            
            var ret = new[] 
            {
                match.Groups["Value1"].Value,
                match.Groups["Value2"].Value,
                match.Groups["Value3"].Value,
            }.FirstOrDefault(x => !string.IsNullOrEmpty(x)) ?? string.Empty;

            return ret;
        }

        private const RegexOptions Options = RegexOptions.None
                                             | RegexOptions.ExplicitCapture
                                             | RegexOptions.IgnoreCase
                                             | RegexOptions.IgnorePatternWhitespace
                                             | RegexOptions.Compiled;

        private const string Space = @"(\s|\r|\n)";

        private static readonly string Regex = $@"
                {Space}*
                SELECT
                {Space}*
                \*
                {Space}*
                FROM
                (
                    ({Space}+ (?<Value1> (\w|\.)+))
                        |
                    ({Space}* \""(?<Value2>.*)\"")
                        |
                    ({Space}* \[(?<Value3>.*)\])
                )

                {Space}*
                (;)?
                {Space}*
        ";

        private static readonly Regex FileNameRegex = new Regex(Regex, Options);
    }
}