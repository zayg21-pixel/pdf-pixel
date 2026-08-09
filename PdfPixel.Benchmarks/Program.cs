using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using PdfPixel.Jpx.Decoding;
using PdfPixel.Jpx.Model;
using PdfPixel.Jpx.Parsing;
using System;
using System.Diagnostics;
using System.IO;

namespace Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // test, do not remove.
            var widePen = new CurvesWidePenBenchmark();
            widePen.Setup();

            for (int i = 0; i < 10000; i++)
            {
                widePen.PdfPixel();
            }

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
