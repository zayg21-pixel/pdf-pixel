using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using PdfPixel.Imaging.Jpx.Decoding;
using PdfPixel.Imaging.Jpx.Model;
using PdfPixel.Imaging.Jpx.Parsing;
using System;
using System.IO;

namespace Benchmarks;

/// <summary>
/// Benchmarks the full JPEG 2000 decode pipeline: header parsing → tile extraction →
/// entropy decoding → inverse DWT → MCT → DC level shift → row output.
/// </summary>
//[MemoryDiagnoser]
[EventPipeProfiler(BenchmarkDotNet.Diagnosers.EventPipeProfile.CpuSampling)]
[SimpleJob(RuntimeMoniker.Net10_0, launchCount: 1, warmupCount: 5, invocationCount: 30, iterationCount: 256)]
public class JpxDecodePipelineBenchmarks
{
    private byte[] _smallImage = Array.Empty<byte>();
    private byte[] _mediumImage = Array.Empty<byte>();
    private byte[] _largeImage = Array.Empty<byte>();

    [GlobalSetup]
    public void Setup()
    {
        string imagesDir = Path.Combine(AppContext.BaseDirectory, "Images");

        // Im8.jpx is small (3080 bytes decoded output)
        _smallImage = LoadIfExists(imagesDir, "Im8.jpx");

        // Im10.jpx is medium (109624 bytes decoded output)
        _mediumImage = LoadIfExists(imagesDir, "Im10.jpx");

        // Im0.jpx is large (1323252 bytes decoded output)
        _largeImage = LoadIfExists(imagesDir, "Im0.jpx");
    }

    [Benchmark]
    public int DecodeSmall()
    {
        return DecodeFullPipeline(_smallImage);
    }

    //[Benchmark]
    //public int DecodeMedium()
    //{
    //    return DecodeFullPipeline(_mediumImage);
    //}

    //[Benchmark]
    //public int DecodeLarge()
    //{
    //    return DecodeFullPipeline(_largeImage);
    //}

    private static int DecodeFullPipeline(byte[] data)
    {
        JpxHeader header = JpxReader.ParseHeader(data);
        ReadOnlySpan<byte> codestream = data.AsSpan(header.CodestreamOffset);

        var tileProvider = new JpxTileProvider(header, codestream);
        using var rowProvider = new JpxTileToRowConverter(header, tileProvider);

        byte[] rowBuffer = new byte[rowProvider.Width * rowProvider.ComponentCount];
        int rowsRead = 0;

        while (rowProvider.TryGetNextRow(rowBuffer))
        {
            rowsRead++;
        }

        return rowsRead;
    }

    private static byte[] LoadIfExists(string dir, string fileName)
    {
        string path = Path.Combine(dir, fileName);

        if (File.Exists(path))
        {
            return File.ReadAllBytes(path);
        }

        return Array.Empty<byte>();
    }
}
