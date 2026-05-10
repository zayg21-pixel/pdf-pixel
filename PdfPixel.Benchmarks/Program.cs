using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using Microsoft.Extensions.Logging;
using PdfPixel.Imaging.Jbig2.Decoding;
using PdfPixel.Imaging.Jbig2.Model;
using System;
using System.IO;

namespace Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            DecodeFile("bitmap-symbol-context-reuse.jb2");
            return;
            //#if DEBUG
            //            var t = new UintBitReaderVsDirectReadBenchmarks();
            //            t.Setup();
            //            t.Verivy();
            //            return;
            //#endif

            //var a = new JpxDecodePipelineBenchmarks();

            //a.Setup();

            //for (int i = 0; i < 50000; i++)
            //{
            //    a.DecodeSmall();
            //}

            //return;

            // Run all benchmarks in the assembly, including JpgIdctTransformBench and others.
            // KeepBenchmarkFiles prevents artifacts cleanup from deleting assemblies
            // that VS needs to resolve symbols when opening .nettrace files.
            var config = DefaultConfig.Instance
                .WithOptions(ConfigOptions.KeepBenchmarkFiles);

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        }

        private static Jbig2Bitmap DecodeFile(string fileName)
        {
            string filePath = Path.Combine(fileName);

            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"JBIG2 test file not found: {filePath}");
            }

            byte[] data = File.ReadAllBytes(filePath);
            var lf = new LoggerFactory();
            var decoder = new Jbig2PageDecoder(lf.CreateLogger<Jbig2PageDecoder>());

            // For standalone .jb2 files, pass as page data with no globals
            return decoder.Decode(data, 0, 0);
        }
    }
}
