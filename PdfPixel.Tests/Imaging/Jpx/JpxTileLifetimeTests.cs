using System;
using System.IO;
using PdfPixel.Jpx.Decoding;
using PdfPixel.Jpx.Model;
using PdfPixel.Jpx.Parsing;
using Xunit;
using Xunit.Abstractions;

namespace PdfPixel.Tests.Imaging.Jpx;

/// <summary>
/// Guards the decoding pipeline's buffer reuse: decoding an image must not allocate
/// per-tile working buffers. Every stage decodes into storage owned by the tile decoder
/// or by the row converter, so the cost of decoding scales with the image's tile-row
/// width rather than with its total tile count.
/// </summary>
public class JpxTileLifetimeTests
{
    private const string LargeJp2Path = "jpx/img-000.jp2";

    // The image is 1190x1683 in 256x256 tiles: 5 columns by 7 rows, 35 tiles of 3 components.
    // Reusing one tile per column decodes it in under 7MB; allocating working buffers per
    // tile instead cost over 90MB.
    private const long MaximumAllocatedBytes = 12L * 1024 * 1024;

    private readonly ITestOutputHelper _output;

    public JpxTileLifetimeTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void DecodeImage_ReusesTileBuffers_AllocationStaysBounded()
    {
        byte[] data = File.ReadAllBytes(LargeJp2Path);

        // Decode once first so one-time costs (buffer growth, JIT) are not measured.
        DecodeAllRows(data);

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        DecodeAllRows(data);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

        _output.WriteLine($"Allocated {allocated / 1024.0 / 1024.0:F2} MB decoding the image.");

        Assert.True(
            allocated < MaximumAllocatedBytes,
            $"Decoding allocated {allocated / 1024.0 / 1024.0:F2} MB, expected below {MaximumAllocatedBytes / 1024.0 / 1024.0:F2} MB. "
                + "A per-tile buffer allocation has most likely been reintroduced.");
    }

    private static int DecodeAllRows(byte[] data)
    {
        JpxHeader header = JpxReader.ParseHeader(data);
        JpxTileProvider provider = new(header, data.AsSpan(header.CodestreamOffset));
        JpxTileToRowConverter converter = new(header, provider);

        int rowBytes = ((converter.Width * converter.ComponentCount * converter.BitsPerComponent) + 7) / 8;
        byte[] rowBuffer = new byte[rowBytes];

        int rows = 0;
        while (converter.TryGetNextRow(rowBuffer))
        {
            rows++;
        }

        return rows;
    }
}
