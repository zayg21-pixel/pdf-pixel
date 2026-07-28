using System;
using System.IO;
using PdfPixel.Jpx.Decoding;
using PdfPixel.Jpx.Model;
using PdfPixel.Jpx.Parsing;
using Xunit;
using Xunit.Abstractions;

namespace PdfPixel.Tests.Imaging.Jpx;

/// <summary>
/// Pins the exact decoded output of a real JPX image so refactoring of the decoding
/// pipeline can be proven not to change a single sample. The expected values were
/// captured from the decoder and cross-checked against the image rendering correctly.
/// </summary>
public class JpxDecodeRegressionTests
{
    private const string LargeJp2Path = "jpx/img-000.jp2";

    private readonly ITestOutputHelper _output;

    public JpxDecodeRegressionTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void DecodeLargeImage_ProducesExpectedSamples()
    {
        DecodedImageDigest digest = DecodeToDigest(LargeJp2Path);

        _output.WriteLine($"{digest.Width}x{digest.Height} components={digest.ComponentCount} bpc={digest.BitsPerComponent}");
        _output.WriteLine($"rows={digest.RowCount} checksum=0x{digest.Checksum:X16} sampleSum={digest.SampleSum}");

        Assert.Equal(1190, digest.Width);
        Assert.Equal(1683, digest.Height);
        Assert.Equal(3, digest.ComponentCount);
        Assert.Equal(8, digest.BitsPerComponent);
        Assert.Equal(1683, digest.RowCount);

        // Exact decoded-sample fingerprint. A change here means the pipeline produces
        // different pixels, not merely different performance. Cross-checked against
        // pdfimages' independent decode of the same image: every sample matches within +/-1
        // (0.5% within +/-2).
        Assert.Equal(0x5D5CB046C1F5FD48UL, digest.Checksum);
        Assert.Equal(242138567L, digest.SampleSum);
    }

    /// <summary>
    /// Reduced-resolution decoding reconstructs only the coarser DWT levels. This pins its
    /// output too, so the pipeline cannot change what a half-resolution decode produces —
    /// the path every image on a page-sized render actually takes.
    /// </summary>
    [Fact]
    public void DecodeLargeImageAtHalfResolution_ProducesExpectedSamples()
    {
        DecodedImageDigest digest = DecodeToDigest(LargeJp2Path, new JpxDecodingParameters(2));

        _output.WriteLine($"{digest.Width}x{digest.Height} components={digest.ComponentCount} bpc={digest.BitsPerComponent}");
        _output.WriteLine($"rows={digest.RowCount} checksum=0x{digest.Checksum:X16} sampleSum={digest.SampleSum}");

        Assert.Equal(595, digest.Width);
        Assert.Equal(842, digest.Height);
        Assert.Equal(842, digest.RowCount);

        Assert.Equal(0x5A9A37C6C80F2732UL, digest.Checksum);
        Assert.Equal(60566851L, digest.SampleSum);
    }

    private static DecodedImageDigest DecodeToDigest(string path, JpxDecodingParameters decodingParameters = default)
    {
        byte[] data = File.ReadAllBytes(path);
        JpxHeader header = JpxReader.ParseHeader(data);
        JpxTileProvider provider = new(header, data.AsSpan(header.CodestreamOffset), decodingParameters);
        JpxTileToRowConverter converter = new(header, provider, decodingParameters);

        int rowBytes = ((converter.Width * converter.ComponentCount * converter.BitsPerComponent) + 7) / 8;
        byte[] rowBuffer = new byte[rowBytes];

        // FNV-1a over every decoded row, plus a plain sum as a second, independent signal.
        ulong checksum = 14695981039346656037UL;
        long sampleSum = 0;
        int rowCount = 0;

        while (converter.TryGetNextRow(rowBuffer))
        {
            for (int i = 0; i < rowBytes; i++)
            {
                byte value = rowBuffer[i];
                checksum = (checksum ^ value) * 1099511628211UL;
                sampleSum += value;
            }

            rowCount++;
        }

        return new DecodedImageDigest(
            converter.Width,
            converter.Height,
            converter.ComponentCount,
            converter.BitsPerComponent,
            rowCount,
            checksum,
            sampleSum);
    }

    private readonly record struct DecodedImageDigest(
        int Width,
        int Height,
        int ComponentCount,
        int BitsPerComponent,
        int RowCount,
        ulong Checksum,
        long SampleSum);
}
