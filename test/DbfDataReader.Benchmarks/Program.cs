using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Running;

namespace DbfDataReader.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var config = DefaultConfig.Instance.AddDiagnoser(MemoryDiagnoser.Default);
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly)
                .Run(args, config);
        }
    }
}
