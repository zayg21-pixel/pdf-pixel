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
            if (Array.IndexOf(args, "--raw") >= 0)
            {
                RunRaw();
                return;
            }

            int dumpIndex = Array.IndexOf(args, "--dump-ppm");
            if (dumpIndex >= 0)
            {
                int descaleFactor = (dumpIndex + 3 < args.Length) ? int.Parse(args[dumpIndex + 3]) : 1;
                DumpPpm(args[dumpIndex + 1], args[dumpIndex + 2], descaleFactor);
                return;
            }

            // Run all benchmarks in the assembly, including JpgIdctTransformBench and others.
            // KeepBenchmarkFiles prevents artifacts cleanup from deleting assemblies
            // that VS needs to resolve symbols when opening .nettrace files.
            var config = DefaultConfig.Instance
                .WithOptions(ConfigOptions.KeepBenchmarkFiles);

            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        }

        // Decodes a .jp2 and writes the samples as a binary PPM, so the output can be diffed
        // per-pixel against an independent decoder:
        //   pdfimages -j -f 1 -l 1 <pdf> <prefix>     produces the reference .ppm
        //   dotnet run -c Release --project PdfPixel.Benchmarks -- --dump-ppm <jp2> <ppm>
        private static void DumpPpm(string jp2Path, string ppmPath, int descaleFactor)
        {
            byte[] data = File.ReadAllBytes(jp2Path);
            JpxHeader header = JpxReader.ParseHeader(data);
            JpxDecodingParameters decodingParameters = new(descaleFactor);
            JpxTileProvider provider = new(header, data.AsSpan(header.CodestreamOffset), decodingParameters);
            JpxTileToRowConverter converter = new(header, provider, decodingParameters);

            int rowBytes = ((converter.Width * converter.ComponentCount * converter.BitsPerComponent) + 7) / 8;
            byte[] rowBuffer = new byte[rowBytes];

            using FileStream output = File.Create(ppmPath);
            using StreamWriter headerWriter = new(output, System.Text.Encoding.ASCII, 64, true);
            headerWriter.Write($"P6\n{converter.Width} {converter.Height}\n255\n");
            headerWriter.Flush();

            while (converter.TryGetNextRow(rowBuffer))
            {
                output.Write(rowBuffer, 0, rowBytes);
            }

            Console.WriteLine($"Wrote {ppmPath}: {converter.Width}x{converter.Height} {converter.ComponentCount} components");
        }

        // Runs each decode once directly, without BenchmarkDotNet's process isolation and
        // iteration harness, reporting wall time and allocation for a single decode. Useful
        // for a quick before/after read and for attaching a profiler to a short-lived run.
        private static void RunRaw()
        {
            JpxDecodeBenchmark benchmark = new();
            benchmark.Setup();

            Console.WriteLine("=== DecodeLargeImage ===");
            long largeAllocStart = GC.GetAllocatedBytesForCurrentThread();
            Stopwatch largeSw = Stopwatch.StartNew();
            benchmark.DecodeLargeImage();
            largeSw.Stop();
            long largeAlloc = GC.GetAllocatedBytesForCurrentThread() - largeAllocStart;
            Console.WriteLine($"Total: {largeSw.Elapsed.TotalMilliseconds:F2}ms, allocated {largeAlloc / 1024.0 / 1024.0:F2}MB");

            Console.WriteLine("=== DecodeSmallImage ===");
            long smallAllocStart = GC.GetAllocatedBytesForCurrentThread();
            Stopwatch smallSw = Stopwatch.StartNew();
            benchmark.DecodeSmallImage();
            smallSw.Stop();
            long smallAlloc = GC.GetAllocatedBytesForCurrentThread() - smallAllocStart;
            Console.WriteLine($"Total: {smallSw.Elapsed.TotalMilliseconds:F2}ms, allocated {smallAlloc / 1024.0 / 1024.0:F2}MB");
        }
    }
}
