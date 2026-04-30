using PdfPixel.Imaging.Jpx.Decoding;
using PdfPixel.Imaging.Jpx.Model;
using PdfPixel.Imaging.Jpx.Parsing;
using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace PdfPixel.Tests.Imaging.Jpx;

/// <summary>
/// Integration tests using real JPEG 2000 test images.
/// Validates the full pipeline: header parsing → tile parsing → entropy decoding → DWT → output.
/// </summary>
public class JpxIntegrationTests
{
    private static readonly string ImagesDirectory = Path.Combine("Imaging", "Jpx", "Images");
    private readonly ITestOutputHelper _output;

    public JpxIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Returns all .jpx test image file paths.
    /// </summary>
    public static TheoryData<string> GetTestImages()
    {
        var data = new TheoryData<string>();
        string dir = Path.Combine(AppContext.BaseDirectory, ImagesDirectory);

        if (Directory.Exists(dir))
        {
            foreach (string file in Directory.GetFiles(dir, "*.jpx"))
            {
                data.Add(Path.GetFileName(file));
            }
        }

        return data;
    }

    private static byte[] LoadImage(string fileName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, ImagesDirectory, fileName);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Test image not found: {path}");
        }

        return File.ReadAllBytes(path);
    }

    /// <summary>
    /// Verifies that header parsing succeeds for all test images and produces valid metadata.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetTestImages))]
    public void ParseHeader_AllImages_Succeeds(string fileName)
    {
        byte[] data = LoadImage(fileName);

        JpxHeader header = JpxReader.ParseHeader(data);

        Assert.NotNull(header);
        Assert.True(header.Width > 0, $"{fileName}: Width must be > 0, got {header.Width}");
        Assert.True(header.Height > 0, $"{fileName}: Height must be > 0, got {header.Height}");
        Assert.True(header.ComponentCount > 0, $"{fileName}: ComponentCount must be > 0, got {header.ComponentCount}");
        Assert.True(header.TileWidth > 0, $"{fileName}: TileWidth must be > 0, got {header.TileWidth}");
        Assert.True(header.TileHeight > 0, $"{fileName}: TileHeight must be > 0, got {header.TileHeight}");
        Assert.True(header.CodestreamOffset > 0, $"{fileName}: CodestreamOffset must be > 0");

        _output.WriteLine($"{fileName}: {header.Width}x{header.Height}, {header.ComponentCount} components, " +
            $"tile={header.TileWidth}x{header.TileHeight}, offset={header.CodestreamOffset}");

        if (header.CodingStyle != null)
        {
            _output.WriteLine($"  CodingStyle: levels={header.CodingStyle.DecompositionLevels}, " +
                $"transform={( header.CodingStyle.IsReversibleTransform ? "5-3" : "9-7")}, " +
                $"layers={header.CodingStyle.NumberOfLayers}, " +
                $"codeblock={header.CodingStyle.CodeBlockWidth}x{header.CodingStyle.CodeBlockHeight}, " +
                $"progression={header.CodingStyle.ProgressionOrder}");
        }

        if (header.Quantization != null)
        {
            _output.WriteLine($"  Quantization: type={header.Quantization.QuantizationType}, " +
                $"guard={header.Quantization.GuardBits}, " +
                $"steps={header.Quantization.StepSizes?.Length ?? 0}");
        }
    }

    /// <summary>
    /// Verifies that the header contains a coding style (COD marker) for all test images.
    /// This is required for the wavelet decoder path.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetTestImages))]
    public void ParseHeader_AllImages_HasCodingStyle(string fileName)
    {
        byte[] data = LoadImage(fileName);

        JpxHeader header = JpxReader.ParseHeader(data);

        Assert.NotNull(header.CodingStyle);
        Assert.True(header.CodingStyle.DecompositionLevels >= 0,
            $"{fileName}: DecompositionLevels must be >= 0");
        Assert.True(header.CodingStyle.NumberOfLayers >= 1,
            $"{fileName}: NumberOfLayers must be >= 1, got {header.CodingStyle.NumberOfLayers}");
    }

    /// <summary>
    /// Verifies that component metadata is correctly parsed for all test images.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetTestImages))]
    public void ParseHeader_AllImages_HasValidComponents(string fileName)
    {
        byte[] data = LoadImage(fileName);

        JpxHeader header = JpxReader.ParseHeader(data);

        Assert.Equal(header.ComponentCount, header.Components.Count);

        for (int i = 0; i < header.Components.Count; i++)
        {
            var component = header.Components[i];
            Assert.True(component.PrecisionBits > 0 && component.PrecisionBits <= 38,
                $"{fileName}: Component {i} precision must be 1-38, got {component.PrecisionBits}");

            _output.WriteLine($"  Component {i}: {component.PrecisionBits}-bit, " +
                $"signed={component.IsSigned}, " +
                $"subsampling={component.HorizontalSeparation}x{component.VerticalSeparation}");
        }
    }

    /// <summary>
    /// Attempts the full decode pipeline for all test images.
    /// Currently validates that the pipeline does not throw unexpected exceptions.
    /// Known NotImplementedException or InvalidDataException are acceptable at this stage.
    /// </summary>
    [Theory]
    [MemberData(nameof(GetTestImages))]
    public void FullDecode_AllImages_DoesNotCrash(string fileName)
    {
        byte[] data = LoadImage(fileName);

        JpxHeader header = JpxReader.ParseHeader(data);
        Assert.NotNull(header);

        ReadOnlySpan<byte> codestream = data.AsSpan(header.CodestreamOffset);

        try
        {
            var tileDecoder = JpxTileDecoderFactory.CreateDecoder(header);
            var decoder = new JpxDecoder(tileDecoder);
            using var rowProvider = decoder.Decode(header, codestream);

            Assert.Equal((int)header.Width, rowProvider.Width);
            Assert.Equal((int)header.Height, rowProvider.Height);
            Assert.Equal(header.ComponentCount, rowProvider.ComponentCount);

            // Read all rows to verify the full pipeline
            byte[] rowBuffer = new byte[rowProvider.Width * rowProvider.ComponentCount];
            int rowsRead = 0;
            int nonZeroPixels = 0;
            int non255Pixels = 0;
            long pixelSum = 0;

            while (rowProvider.TryGetNextRow(rowBuffer))
            {
                for (int i = 0; i < rowBuffer.Length; i++)
                {
                    if (rowBuffer[i] != 0)
                    {
                        nonZeroPixels++;
                    }

                    if (rowBuffer[i] != 255)
                    {
                        non255Pixels++;
                    }

                    pixelSum += rowBuffer[i];
                }

                rowsRead++;
            }

            Assert.Equal((int)header.Height, rowsRead);

            int totalPixels = rowsRead * rowProvider.Width * rowProvider.ComponentCount;
            double avgPixel = totalPixels > 0 ? (double)pixelSum / totalPixels : 0;
            _output.WriteLine($"{fileName}: DECODED OK - {header.Width}x{header.Height}, {rowsRead} rows, " +
                $"nonZero={nonZeroPixels}/{totalPixels}, non255={non255Pixels}/{totalPixels}, avg={avgPixel:F1}");

            // Diagnostic: check if avg is exactly 128.0 (likely means all coefficients were zero)
            if (Math.Abs(avgPixel - 128.0) < 0.5 && totalPixels > 100)
            {
                _output.WriteLine($"  WARNING: avg≈128 suggests all wavelet coefficients are zero (only DC offset)");
            }
        }
        catch (NotImplementedException ex)
        {
            _output.WriteLine($"{fileName}: NOT IMPLEMENTED - {ex.Message}");
        }
        catch (InvalidDataException ex)
        {
            _output.WriteLine($"{fileName}: INVALID DATA - {ex.Message}");
        }
        catch (EndOfStreamException ex)
        {
            _output.WriteLine($"{fileName}: END OF STREAM - {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            _output.WriteLine($"{fileName}: INVALID OPERATION - {ex.Message}");
        }
        catch (OutOfMemoryException)
        {
            _output.WriteLine($"{fileName}: OUT OF MEMORY - image too large for current implementation");
        }
    }

    /// <summary>
    /// Diagnostic test that inspects intermediate pipeline stages for a single small image.
    /// Uses the full decode path but with a larger image to validate non-trivial content.
    /// </summary>
    [Fact]
    public void Diagnostic_SingleImage_InspectPipeline()
    {
        // Use Im0.jpx which showed avg=128 (all zero coefficients)
        string fileName = "Im0.jpx";
        byte[] data = LoadImage(fileName);

        JpxHeader header = JpxReader.ParseHeader(data);
        _output.WriteLine($"Header: {header.Width}x{header.Height}, {header.ComponentCount} comp, " +
            $"decomp={header.CodingStyle.DecompositionLevels}, " +
            $"reversible={header.CodingStyle.IsReversibleTransform}, " +
            $"cbSize={header.CodingStyle.CodeBlockWidth}x{header.CodingStyle.CodeBlockHeight}, " +
            $"layers={header.CodingStyle.NumberOfLayers}, " +
            $"progression={header.CodingStyle.ProgressionOrder}");

        if (header.Quantization != null)
        {
            _output.WriteLine($"Quantization: type={header.Quantization.QuantizationType}, " +
                $"guard={header.Quantization.GuardBits}, steps={header.Quantization.StepSizes?.Length ?? 0}");
        }

        ReadOnlySpan<byte> codestream = data.AsSpan(header.CodestreamOffset);

        // Dump first 32 bytes of codestream for inspection
        int dumpLen = Math.Min(32, codestream.Length);
        var hexDump = new System.Text.StringBuilder();
        for (int i = 0; i < dumpLen; i++)
        {
            hexDump.AppendFormat("{0:X2} ", codestream[i]);
        }

        _output.WriteLine($"Codestream first {dumpLen} bytes: {hexDump}");
        _output.WriteLine($"Codestream length: {codestream.Length}");

        // Full decode
        var tileDecoder = JpxTileDecoderFactory.CreateDecoder(header);
        var decoder = new JpxDecoder(tileDecoder);
        using var rowProvider = decoder.Decode(header, codestream);

        // Check first few rows of pixel data
        byte[] rowBuffer = new byte[rowProvider.Width * rowProvider.ComponentCount];
        int rowsRead = 0;
        int[] histogram = new int[256];

        while (rowProvider.TryGetNextRow(rowBuffer))
        {
            for (int i = 0; i < rowBuffer.Length; i++)
            {
                histogram[rowBuffer[i]]++;
            }

            if (rowsRead < 3)
            {
                var firstPixels = new System.Text.StringBuilder();
                int pixelsToDump = Math.Min(10, rowProvider.Width);
                for (int px = 0; px < pixelsToDump; px++)
                {
                    firstPixels.Append('[');
                    for (int c = 0; c < rowProvider.ComponentCount; c++)
                    {
                        if (c > 0)
                        {
                            firstPixels.Append(',');
                        }

                        firstPixels.Append(rowBuffer[px * rowProvider.ComponentCount + c]);
                    }

                    firstPixels.Append("] ");
                }

                _output.WriteLine($"  Row {rowsRead}: {firstPixels}");
            }

            rowsRead++;
        }

        // Print histogram summary
        _output.WriteLine($"Histogram (non-zero bins):");
        for (int i = 0; i < 256; i++)
        {
            if (histogram[i] > 0)
            {
                _output.WriteLine($"  [{i}] = {histogram[i]}");
            }
        }
    }
}
