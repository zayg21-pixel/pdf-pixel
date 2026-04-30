using PdfPixel.Imaging.Jpx.Decoding;
using PdfPixel.Imaging.Jpx.Model;
using PdfPixel.Imaging.Jpx.Parsing;
using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace PdfPixel.Tests.Imaging.Jpx;

/// <summary>
/// Integration snapshot tests for JPEG 2000 decoding.
/// Compares fully decoded pixel output against stored binary reference files.
/// </summary>
public class JpxSnapshotTests
{
    private static readonly string ImagesDir = Path.Combine("Imaging", "Jpx", "Images");
    private static readonly string SnapshotsDir = Path.Combine("Imaging", "Jpx", "Snapshots");
    private readonly ITestOutputHelper _output;

    public JpxSnapshotTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Returns all .jpx test image file names.
    /// </summary>
    public static TheoryData<string> GetTestImages()
    {
        var data = new TheoryData<string>();
        string dir = Path.Combine(AppContext.BaseDirectory, ImagesDir);

        if (Directory.Exists(dir))
        {
            foreach (string file in Directory.GetFiles(dir, "*.jpx"))
            {
                data.Add(Path.GetFileName(file));
            }
        }

        return data;
    }

    /// <summary>
    /// Verifies that the fully decoded pixel output matches the stored binary snapshot.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetTestImages))]
    public void Decode_MatchesSnapshot(string fileName)
    {
        string snapshotName = Path.GetFileNameWithoutExtension(fileName) + ".bin";
        string snapshotPath = Path.Combine(AppContext.BaseDirectory, SnapshotsDir, snapshotName);

        Assert.True(File.Exists(snapshotPath),
            $"Snapshot file not found: {snapshotPath}. Run GenerateSnapshots to create reference data.");

        byte[] actual = DecodeImage(fileName);
        byte[] expected = File.ReadAllBytes(snapshotPath);

        Assert.Equal(expected.Length, actual.Length);

        int mismatches = 0;
        int firstMismatchIndex = -1;

        for (int i = 0; i < expected.Length; i++)
        {
            if (expected[i] != actual[i])
            {
                if (firstMismatchIndex < 0)
                {
                    firstMismatchIndex = i;
                }

                mismatches++;
            }
        }

        if (mismatches > 0)
        {
            _output.WriteLine($"{fileName}: {mismatches}/{expected.Length} bytes differ " +
                $"({100.0 * mismatches / expected.Length:F2}%), first at index {firstMismatchIndex} " +
                $"(expected={expected[firstMismatchIndex]}, actual={actual[firstMismatchIndex]})");
        }

        Assert.Equal(0, mismatches);
    }

    /// <summary>
    /// Generates snapshot .bin files for all test images.
    /// Run this explicitly when the decoder output is known-good.
    /// Skipped by default to avoid accidentally overwriting reference data.
    /// </summary>
    [Fact(Skip = "Run manually to regenerate snapshot files.")]
    public void GenerateSnapshots()
    {
        string outputDir = Path.Combine(AppContext.BaseDirectory, SnapshotsDir);
        Directory.CreateDirectory(outputDir);

        string imagesPath = Path.Combine(AppContext.BaseDirectory, ImagesDir);

        foreach (string file in Directory.GetFiles(imagesPath, "*.jpx"))
        {
            string fileName = Path.GetFileName(file);
            string snapshotName = Path.GetFileNameWithoutExtension(fileName) + ".bin";
            string snapshotPath = Path.Combine(outputDir, snapshotName);

            try
            {
                byte[] decoded = DecodeImage(fileName);
                File.WriteAllBytes(snapshotPath, decoded);
                _output.WriteLine($"Generated: {snapshotName} ({decoded.Length} bytes)");
            }
            catch (Exception ex)
            {
                _output.WriteLine($"FAILED: {fileName} - {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static byte[] DecodeImage(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, ImagesDir, fileName);
        byte[] data = File.ReadAllBytes(path);

        JpxHeader header = JpxReader.ParseHeader(data);
        ReadOnlySpan<byte> codestream = data.AsSpan(header.CodestreamOffset);

        var tileDecoder = JpxTileDecoderFactory.CreateDecoder(header);
        var decoder = new JpxDecoder(tileDecoder);
        using var rowProvider = decoder.Decode(header, codestream);

        int rowSize = rowProvider.Width * rowProvider.ComponentCount;
        byte[] result = new byte[rowSize * rowProvider.Height];
        byte[] rowBuffer = new byte[rowSize];
        int offset = 0;

        while (rowProvider.TryGetNextRow(rowBuffer))
        {
            Buffer.BlockCopy(rowBuffer, 0, result, offset, rowSize);
            offset += rowSize;
        }

        return result;
    }
}
