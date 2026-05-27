using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Run all benchmarks in the assembly, including JpgIdctTransformBench and others.
            // KeepBenchmarkFiles prevents artifacts cleanup from deleting assemblies
            // that VS needs to resolve symbols when opening .nettrace files.
            var config = DefaultConfig.Instance
                .WithOptions(ConfigOptions.KeepBenchmarkFiles);

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        }
    }
}
