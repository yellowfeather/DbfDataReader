using System.Collections.Generic;
using System.IO;

namespace DbfDataReader.Tests
{
    public static class FixtureHelpers
    {
        public static IEnumerable<string> GetFieldLines(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open))
            using (var summaryFile = new StreamReader(stream))
            {
                var line = summaryFile.ReadLine();
                while (line != null && !line.StartsWith("---"))
                {
                    line = summaryFile.ReadLine();
                }
                
                line = summaryFile.ReadLine();
                while (line != null)
                {
                    yield return line;
                    line = summaryFile.ReadLine();
                }
            }
        }

    }
}