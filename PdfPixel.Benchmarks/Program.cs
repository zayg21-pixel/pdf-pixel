using BenchmarkDotNet.Running;

namespace Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
#if DEBUG
            var t = new UintBitReaderVsDirectReadBenchmarks();
            t.Setup();
            t.Verivy();
            return;
#endif

            //// Run all benchmarks in the assembly, including JpgIdctTransformBench and others.
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
