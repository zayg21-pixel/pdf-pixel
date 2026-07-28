using System;
using System.IO;
using BenchmarkDotNet.Attributes;
using PdfPixel.Jpx.Decoding;
using PdfPixel.Jpx.Model;
using PdfPixel.Jpx.Parsing;

namespace Benchmarks;

/// <summary>
/// Measures raw JPX codec throughput for the image sizes found in bug1749563.pdf.
/// Run from repo root: dotnet run -c Release --project PdfPixel.Benchmarks -- --filter *JpxDecode*
/// </summary>
[MemoryDiagnoser]
public class JpxDecodeBenchmark
{
    // Extracted from bug1749563.pdf page 1 via `pdfimages -all` (checked into TestData/).
    // Override via environment variables if the files live elsewhere.
    private static readonly string LargeJp2Path = Environment.GetEnvironmentVariable("LARGE_JP2")
        ?? Path.Combine(AppContext.BaseDirectory, "TestData", "img-000.jp2");

    private static readonly string SmallJp2Path = Environment.GetEnvironmentVariable("SMALL_JP2")
        ?? Path.Combine(AppContext.BaseDirectory, "TestData", "img-011.jp2");

    private byte[] _largeJp2 = Array.Empty<byte>();
    private byte[] _smallJp2 = Array.Empty<byte>();

    private byte[] _rowBuffer = Array.Empty<byte>();

    [GlobalSetup]
    public void Setup()
    {
        _largeJp2 = File.ReadAllBytes(LargeJp2Path);
        _smallJp2 = File.ReadAllBytes(SmallJp2Path);

        // Pre-size the row buffer using the large image dimensions
        JpxHeader header = JpxReader.ParseHeader(_largeJp2);
        JpxTileProvider provider = new(header, _largeJp2.AsSpan(header.CodestreamOffset));
        JpxTileToRowConverter converter = new(header, provider);
        int rowBytes = (converter.Width * converter.ComponentCount * converter.BitsPerComponent + 7) / 8;
        _rowBuffer = new byte[rowBytes];

        Console.WriteLine($"Large image: {converter.Width}x{converter.Height} {converter.BitsPerComponent}bpc, {converter.ComponentCount} components, row={rowBytes}B");
    }

    /// <summary>
    /// Decodes the large background image (the slow case, ~178KB compressed, 1190x1683 RGB).
    /// </summary>
    [Benchmark]
    public int DecodeLargeImage()
    {
        JpxHeader header = JpxReader.ParseHeader(_largeJp2);
        JpxTileProvider provider = new(header, _largeJp2.AsSpan(header.CodestreamOffset));
        JpxTileToRowConverter converter = new(header, provider);

        int rowBytes = (converter.Width * converter.ComponentCount * converter.BitsPerComponent + 7) / 8;
        byte[] rowBuf = _rowBuffer.Length >= rowBytes ? _rowBuffer : new byte[rowBytes];
        Span<byte> row = rowBuf.AsSpan(0, rowBytes);

        int rows = 0;
        while (converter.TryGetNextRow(row))
        {
            rows++;
        }

        return rows;
    }

    /// <summary>
    /// Decodes a small overlay image (~15KB compressed, 1190x1683 RGB, mostly transparent).
    /// </summary>
    [Benchmark]
    public int DecodeSmallImage()
    {
        JpxHeader header = JpxReader.ParseHeader(_smallJp2);
        JpxTileProvider provider = new(header, _smallJp2.AsSpan(header.CodestreamOffset));
        JpxTileToRowConverter converter = new(header, provider);

        int rowBytes = (converter.Width * converter.ComponentCount * converter.BitsPerComponent + 7) / 8;
        byte[] rowBufSmall = _rowBuffer.Length >= rowBytes ? _rowBuffer : new byte[rowBytes];
        Span<byte> row = rowBufSmall.AsSpan(0, rowBytes);

        int rows = 0;
        while (converter.TryGetNextRow(row))
        {
            rows++;
        }

        return rows;
    }
}
