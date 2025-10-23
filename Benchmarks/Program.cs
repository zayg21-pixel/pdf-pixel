using BenchmarkDotNet.Running;

namespace Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var b = new JpegDecodeBenchmarks();
            b.Setup();

            for (int i = 0; i < 1000; i++)
            {
                var result = b.BaselineStreamDecode();
                result.Dispose();
            }


            // Run all benchmarks in the assembly, including JpgIdctTransformBench and others.
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
