using System;
using System.Reflection;

namespace DbfDataReader.Benchmarks
{
    // BenchmarkDotNet rebuilds this project in a generated child project, which does
    // NOT inherit -p:DbfDataReaderVersion from the command line (only the environment
    // variable form reaches it - see the README). Printing the version the benchmark
    // process actually loaded makes a silently wrong package impossible to miss.
    internal static class BenchmarkVersion
    {
        public static void Print()
        {
            var assembly = typeof(DbfDataReader).Assembly;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ?? assembly.GetName().Version?.ToString();
            Console.WriteLine($"// DbfDataReader under benchmark: {version}");
        }
    }
}
