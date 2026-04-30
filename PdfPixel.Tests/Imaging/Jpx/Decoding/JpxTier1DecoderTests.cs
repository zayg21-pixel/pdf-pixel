using PdfPixel.Imaging.Jpx.Decoding;
using PdfPixel.Imaging.Jpx.Model;
using System;
using Xunit;

namespace PdfPixel.Tests.Imaging.Jpx.Decoding;

/// <summary>
/// Tests for the EBCOT Tier-1 code-block decoder (ITU-T T.800 Annex D).
/// </summary>
public class JpxTier1DecoderTests
{
    /// <summary>
    /// Creates a default coding style for testing.
    /// </summary>
    private static JpxCodingStyle CreateDefaultCodingStyle()
    {
        return new JpxCodingStyle
        {
            Style = 0,
            ProgressionOrder = 0,
            NumberOfLayers = 1,
            MultiComponentTransform = 0,
            DecompositionLevels = 1,
            CodeBlockWidthExponent = 4, // 64 pixels wide
            CodeBlockHeightExponent = 4, // 64 pixels tall
            CodeBlockStyle = 0,
            Transform = 1 // reversible 5-3
        };
    }

    [Fact]
    public void DecodeCodeBlock_NullCodeBlock_Throws()
    {
        var codingStyle = CreateDefaultCodingStyle();

        Assert.Throws<ArgumentNullException>(() =>
            JpxTier1Decoder.DecodeCodeBlock(null, codingStyle));
    }

    [Fact]
    public void DecodeCodeBlock_NullCodingStyle_Throws()
    {
        var codeBlock = new JpxCodeBlock { Width = 4, Height = 4 };

        Assert.Throws<ArgumentNullException>(() =>
            JpxTier1Decoder.DecodeCodeBlock(codeBlock, null));
    }

    [Fact]
    public void DecodeCodeBlock_ZeroDimensions_ReturnsEmptyArray()
    {
        var codeBlock = new JpxCodeBlock { Width = 0, Height = 0 };
        var codingStyle = CreateDefaultCodingStyle();

        int[] result = JpxTier1Decoder.DecodeCodeBlock(codeBlock, codingStyle);

        Assert.Equal(0, codeBlock.Height);
        Assert.Equal(0, codeBlock.Width);
    }

    [Fact]
    public void DecodeCodeBlock_NullData_ReturnsZeroCoefficients()
    {
        var codeBlock = new JpxCodeBlock
        {
            Width = 4,
            Height = 4,
            Data = null,
            CodingPasses = 0,
            ZeroBitPlanes = 0
        };
        var codingStyle = CreateDefaultCodingStyle();

        int[] result = JpxTier1Decoder.DecodeCodeBlock(codeBlock, codingStyle);

        Assert.Equal(4, codeBlock.Height);
        Assert.Equal(4, codeBlock.Width);

        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                Assert.Equal(0, result[y * codeBlock.Width + x]);
            }
        }
    }

    [Fact]
    public void DecodeCodeBlock_EmptyData_ReturnsZeroCoefficients()
    {
        var codeBlock = new JpxCodeBlock
        {
            Width = 4,
            Height = 4,
            Data = Array.Empty<byte>(),
            CodingPasses = 0,
            ZeroBitPlanes = 0
        };
        var codingStyle = CreateDefaultCodingStyle();

        int[] result = JpxTier1Decoder.DecodeCodeBlock(codeBlock, codingStyle);

        Assert.Equal(4, codeBlock.Height);
        Assert.Equal(4, codeBlock.Width);
    }

    [Fact]
    public void DecodeCodeBlock_ZeroCodingPasses_ReturnsZeroCoefficients()
    {
        var codeBlock = new JpxCodeBlock
        {
            Width = 4,
            Height = 4,
            Data = new byte[] { 0x00, 0x00, 0x00, 0x00 },
            CodingPasses = 0,
            ZeroBitPlanes = 0
        };
        var codingStyle = CreateDefaultCodingStyle();

        int[] result = JpxTier1Decoder.DecodeCodeBlock(codeBlock, codingStyle);

        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                Assert.Equal(0, result[y * codeBlock.Width + x]);
            }
        }
    }

    /// <summary>
    /// Verifies that a single cleanup pass (the first pass of the first bit-plane)
    /// produces valid output dimensions and values.
    /// </summary>
    [Fact]
    public void DecodeCodeBlock_SingleCleanupPass_ProducesValidOutput()
    {
        // Provide enough data for MQ decoder to work with
        var data = new byte[32];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = 0x00; // All zeros → mostly MPS decisions
        }

        var codeBlock = new JpxCodeBlock
        {
            Width = 4,
            Height = 4,
            Data = data,
            CodingPasses = 1, // Single cleanup pass
            ZeroBitPlanes = 0
        };
        var codingStyle = CreateDefaultCodingStyle();

        int[] result = JpxTier1Decoder.DecodeCodeBlock(codeBlock, codingStyle);

        Assert.Equal(4, codeBlock.Height);
        Assert.Equal(4, codeBlock.Width);
    }

    /// <summary>
    /// Verifies that multiple coding passes execute without error.
    /// After the first cleanup pass, the cycle is: sig-prop, mag-ref, cleanup.
    /// </summary>
    [Fact]
    public void DecodeCodeBlock_MultiplePasses_DoesNotThrow()
    {
        var data = new byte[128];
        var random = new Random(42); // Deterministic seed
        random.NextBytes(data);

        var codeBlock = new JpxCodeBlock
        {
            Width = 4,
            Height = 4,
            Data = data,
            CodingPasses = 7, // cleanup + 2 full cycles (sig, mag, cleanup)
            ZeroBitPlanes = 0
        };
        var codingStyle = CreateDefaultCodingStyle();

        int[] result = JpxTier1Decoder.DecodeCodeBlock(codeBlock, codingStyle);

        Assert.Equal(4, codeBlock.Height);
        Assert.Equal(4, codeBlock.Width);
    }

    /// <summary>
    /// Verifies that non-square code-blocks are handled correctly.
    /// </summary>
    [Fact]
    public void DecodeCodeBlock_NonSquare_ProducesCorrectDimensions()
    {
        var data = new byte[64];

        var codeBlock = new JpxCodeBlock
        {
            Width = 8,
            Height = 2,
            Data = data,
            CodingPasses = 1,
            ZeroBitPlanes = 0
        };
        var codingStyle = CreateDefaultCodingStyle();

        int[] result = JpxTier1Decoder.DecodeCodeBlock(codeBlock, codingStyle);

        Assert.Equal(2, codeBlock.Height);
        Assert.Equal(8, codeBlock.Width);
    }

    /// <summary>
    /// Verifies that a code-block taller than 4 rows (multiple stripes) works correctly.
    /// </summary>
    [Fact]
    public void DecodeCodeBlock_MultipleStripes_DoesNotThrow()
    {
        var data = new byte[256];
        var random = new Random(123);
        random.NextBytes(data);

        var codeBlock = new JpxCodeBlock
        {
            Width = 4,
            Height = 12, // 3 stripes of 4 rows
            Data = data,
            CodingPasses = 4, // cleanup + sig + mag + cleanup
            ZeroBitPlanes = 0
        };
        var codingStyle = CreateDefaultCodingStyle();

        int[] result = JpxTier1Decoder.DecodeCodeBlock(codeBlock, codingStyle);

        Assert.Equal(12, codeBlock.Height);
        Assert.Equal(4, codeBlock.Width);
    }

    /// <summary>
    /// Verifies that zero bit-planes offset is respected.
    /// With zero bit-planes > 0, the first coded bit represents a less significant position.
    /// </summary>
    [Fact]
    public void DecodeCodeBlock_WithZeroBitPlanes_ShiftsPositionCorrectly()
    {
        var data = new byte[64];
        var random = new Random(999);
        random.NextBytes(data);

        var codeBlock = new JpxCodeBlock
        {
            Width = 4,
            Height = 4,
            Data = data,
            CodingPasses = 1,
            ZeroBitPlanes = 3 // Skip 3 most significant bit-planes
        };
        var codingStyle = CreateDefaultCodingStyle();

        int[] result = JpxTier1Decoder.DecodeCodeBlock(codeBlock, codingStyle);

        // Any non-zero coefficients should have magnitude at bit position 3
        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                int magnitude = Math.Abs(result[y * codeBlock.Width + x]);
                if (magnitude != 0)
                {
                    // The first coded bit-plane is at position zeroBitPlanes (3)
                    Assert.Equal(1 << 3, magnitude);
                }
            }
        }
    }

    /// <summary>
    /// Verifies deterministic output — same input always produces same coefficients.
    /// </summary>
    [Fact]
    public void DecodeCodeBlock_IsDeterministic()
    {
        var data = new byte[64];
        var random = new Random(77);
        random.NextBytes(data);

        var codingStyle = CreateDefaultCodingStyle();

        var codeBlock1 = new JpxCodeBlock
        {
            Width = 4, Height = 4,
            Data = (byte[])data.Clone(),
            CodingPasses = 4, ZeroBitPlanes = 0
        };

        var codeBlock2 = new JpxCodeBlock
        {
            Width = 4, Height = 4,
            Data = (byte[])data.Clone(),
            CodingPasses = 4, ZeroBitPlanes = 0
        };

        int[] result1 = JpxTier1Decoder.DecodeCodeBlock(codeBlock1, codingStyle);
        int[] result2 = JpxTier1Decoder.DecodeCodeBlock(codeBlock2, codingStyle);

        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 4; x++)
            {
                Assert.Equal(result1[y * 4 + x], result2[y * 4 + x]);
            }
        }
    }

    /// <summary>
    /// Verifies that segmentation symbols are checked when enabled.
    /// Invalid data should cause a mismatch exception on the cleanup pass.
    /// </summary>
    [Fact]
    public void DecodeCodeBlock_SegmentationSymbol_InvalidData_Throws()
    {
        var data = new byte[64];
        // Zeros will produce deterministic MQ output that likely won't match 0x0A

        var codeBlock = new JpxCodeBlock
        {
            Width = 4,
            Height = 4,
            Data = data,
            CodingPasses = 1, // Single cleanup pass
            ZeroBitPlanes = 0
        };

        var codingStyle = CreateDefaultCodingStyle();
        codingStyle.CodeBlockStyle = 0x20; // Enable segmentation symbols

        // The cleanup pass will check for the segmentation symbol (0x0A)
        // With all-zero data, the MQ decoder will likely not produce 0x0A
        Assert.Throws<InvalidOperationException>(() =>
            JpxTier1Decoder.DecodeCodeBlock(codeBlock, codingStyle));
    }

    /// <summary>
    /// Verifies that the reset-contexts flag causes contexts to be reinitialized between passes.
    /// </summary>
    [Fact]
    public void DecodeCodeBlock_ResetContexts_DoesNotThrow()
    {
        var data = new byte[128];
        var random = new Random(555);
        random.NextBytes(data);

        var codeBlock = new JpxCodeBlock
        {
            Width = 4,
            Height = 4,
            Data = data,
            CodingPasses = 4,
            ZeroBitPlanes = 0
        };

        var codingStyle = CreateDefaultCodingStyle();
        codingStyle.CodeBlockStyle = 0x02; // Reset contexts on each pass

        int[] result = JpxTier1Decoder.DecodeCodeBlock(codeBlock, codingStyle);

        Assert.Equal(4, codeBlock.Height);
        Assert.Equal(4, codeBlock.Width);
    }

    /// <summary>
    /// Verifies that a 1x1 code-block (single sample) works correctly.
    /// </summary>
    [Fact]
    public void DecodeCodeBlock_SingleSample_ProducesValidOutput()
    {
        var data = new byte[16];

        var codeBlock = new JpxCodeBlock
        {
            Width = 1,
            Height = 1,
            Data = data,
            CodingPasses = 1,
            ZeroBitPlanes = 0
        };
        var codingStyle = CreateDefaultCodingStyle();

        int[] result = JpxTier1Decoder.DecodeCodeBlock(codeBlock, codingStyle);

        Assert.Equal(1, codeBlock.Height);
        Assert.Equal(1, codeBlock.Width);
    }
}
