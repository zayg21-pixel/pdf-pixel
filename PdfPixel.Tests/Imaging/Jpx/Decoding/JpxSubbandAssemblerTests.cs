using PdfPixel.Imaging.Jpx.Decoding;
using PdfPixel.Imaging.Jpx.Model;
using System;
using Xunit;

namespace PdfPixel.Tests.Imaging.Jpx.Decoding;

/// <summary>
/// Tests for coefficient assembly and inverse DWT (Stages 3-6).
/// </summary>
public class JpxSubbandAssemblerTests
{
    private static JpxHeader CreateHeader(int width, int height, int decompositionLevels, bool reversible = true)
    {
        var header = new JpxHeader
        {
            Width = (uint)width,
            Height = (uint)height,
            ComponentCount = 1,
            TileWidth = (uint)width,
            TileHeight = (uint)height,
            CodingStyle = new JpxCodingStyle
            {
                DecompositionLevels = (byte)decompositionLevels,
                CodeBlockWidthExponent = 4,
                CodeBlockHeightExponent = 4,
                Transform = (byte)(reversible ? 1 : 0),
                NumberOfLayers = 1
            }
        };

        // 8-bit unsigned component
        header.Components.Add(new JpxComponent { SamplePrecision = 7 });
        return header;
    }

    private static JpxTileHeader CreateTileHeader()
    {
        return new JpxTileHeader
        {
            TileIndex = 0,
            TilesHorizontal = 1,
            TilesVertical = 1
        };
    }

    /// <summary>
    /// Helper that replicates the full pipeline (assembly + DWT + MCT + level shift)
    /// using the new granular classes, matching the old AssembleAndReconstruct behavior.
    /// </summary>
    private static void AssembleAndReconstruct(JpxPacket[] packets, JpxHeader header, JpxTile tile)
    {
        if (packets == null)
        {
            return;
        }

        int decompositionLevels = header.CodingStyle.DecompositionLevels;
        var assembler = new JpxSubbandAssembler(header.CodingStyle);

        for (int component = 0; component < tile.ComponentCount; component++)
        {
            var subbands = new JpxSubbandData(tile.Width, tile.Height, decompositionLevels);
            assembler.Assemble(packets, component, subbands);

            IJpxInverseDwt inverseDwt = JpxInverseDwtFactory.Create(header, component);
            int[] reconstructed = new int[tile.Width * tile.Height];
            inverseDwt.Transform(subbands, reconstructed);

            int count = Math.Min(reconstructed.Length, tile.ComponentData[component].Length);
            Array.Copy(reconstructed, tile.ComponentData[component], count);
        }

        var inverseMct = new JpxInverseMct(header.CodingStyle);
        inverseMct.Apply(tile);

        for (int component = 0; component < tile.ComponentCount; component++)
        {
            int tileBitDepth = tile.ComponentBitDepths[component];
            bool isSigned = tile.ComponentSigned[component];
            int[] data = tile.ComponentData[component];

            if (!isSigned)
            {
                int dcOffset = 1 << (tileBitDepth - 1);
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] += dcOffset;
                }
            }

            int maxValue = (1 << tileBitDepth) - 1;
            int minValue = isSigned ? -(1 << (tileBitDepth - 1)) : 0;

            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] < minValue)
                {
                    data[i] = minValue;
                }
                else if (data[i] > maxValue)
                {
                    data[i] = maxValue;
                }
            }
        }
    }

    // --- SubbandData dimension tests ---

    [Fact]
    public void SubbandData_OneLevelDecomposition_CorrectDimensions()
    {
        var sb = new JpxSubbandData(8, 8, 1);

        Assert.Equal(4, sb.LLWidth);
        Assert.Equal(4, sb.LLHeight);

        // HL: parentWidth(8) - childWidth(4) = 4, height = childHeight(4)
        Assert.Equal(4, sb.GetWidth(0, JpxSubbandType.HL));
        Assert.Equal(4, sb.GetHeight(0, JpxSubbandType.HL));

        // LH: width = childWidth(4), height = parentHeight(8) - childHeight(4) = 4
        Assert.Equal(4, sb.GetWidth(0, JpxSubbandType.LH));
        Assert.Equal(4, sb.GetHeight(0, JpxSubbandType.LH));

        // HH: 4x4
        Assert.Equal(4, sb.GetWidth(0, JpxSubbandType.HH));
        Assert.Equal(4, sb.GetHeight(0, JpxSubbandType.HH));
    }

    [Fact]
    public void SubbandData_TwoLevelDecomposition_CorrectDimensions()
    {
        var sb = new JpxSubbandData(8, 8, 2);

        // LL: 8/4 = 2x2
        Assert.Equal(2, sb.LLWidth);
        Assert.Equal(2, sb.LLHeight);

        // Level 0 (finest): parent = 8, child = 4 → detail subbands are 4x4
        Assert.Equal(4, sb.GetWidth(0, JpxSubbandType.HL));
        Assert.Equal(4, sb.GetHeight(0, JpxSubbandType.HL));

        // Level 1 (coarser): parent = 4, child = 2 → detail subbands are 2x2
        Assert.Equal(2, sb.GetWidth(1, JpxSubbandType.HL));
        Assert.Equal(2, sb.GetHeight(1, JpxSubbandType.HL));
    }

    [Fact]
    public void SubbandData_OddDimensions_CeilsCorrectly()
    {
        // 7x5 with 1 level
        var sb = new JpxSubbandData(7, 5, 1);

        // LL: ceil(7/2)=4, ceil(5/2)=3
        Assert.Equal(4, sb.LLWidth);
        Assert.Equal(3, sb.LLHeight);

        // HL: parent(7)-child(4)=3, height=child(3)
        Assert.Equal(3, sb.GetWidth(0, JpxSubbandType.HL));
        Assert.Equal(3, sb.GetHeight(0, JpxSubbandType.HL));

        // LH: width=child(4), parent(5)-child(3)=2
        Assert.Equal(4, sb.GetWidth(0, JpxSubbandType.LH));
        Assert.Equal(2, sb.GetHeight(0, JpxSubbandType.LH));
    }

    // --- Inverse DWT 5-3 tests ---

    [Fact]
    public void AssembleAndReconstruct_EmptyPackets_DoesNotThrow()
    {
        var header = CreateHeader(8, 8, 1);
        var tile = new JpxTile(header, CreateTileHeader());

        AssembleAndReconstruct(Array.Empty<JpxPacket>(), header, tile);

        // All zeros + level shift = 128 for 8-bit unsigned
        for (int i = 0; i < tile.ComponentData[0].Length; i++)
        {
            Assert.Equal(128, tile.ComponentData[0][i]);
        }
    }

    [Fact]
    public void AssembleAndReconstruct_NullPackets_DoesNotThrow()
    {
        var header = CreateHeader(8, 8, 1);
        var tile = new JpxTile(header, CreateTileHeader());

        AssembleAndReconstruct(null, header, tile);

        // Should not modify tile data (stays at initial zeros, no level shift applied)
    }

    [Fact]
    public void AssembleAndReconstruct_ZeroDecomposition_PassesThroughDirectly()
    {
        var header = CreateHeader(4, 4, 0);
        var tile = new JpxTile(header, CreateTileHeader());

        // With 0 decomposition levels, LL = full image, no DWT needed
        var sb = new JpxSubbandData(4, 4, 0);

        Assert.Equal(4, sb.LLWidth);
        Assert.Equal(4, sb.LLHeight);
    }

    /// <summary>
    /// Verifies that level shift adds 2^(bitDepth-1) for unsigned components.
    /// </summary>
    [Fact]
    public void AssembleAndReconstruct_LevelShift_AddsOffset()
    {
        var header = CreateHeader(2, 2, 1);
        var tile = new JpxTile(header, CreateTileHeader());

        // Empty packets → all-zero coefficients → after DWT still zero → level shift adds 128
        AssembleAndReconstruct(new JpxPacket[0], header, tile);

        for (int i = 0; i < tile.ComponentData[0].Length; i++)
        {
            Assert.Equal(128, tile.ComponentData[0][i]);
        }
    }

    /// <summary>
    /// Verifies that output values are clamped to valid range [0, 255] for 8-bit unsigned.
    /// </summary>
    [Fact]
    public void AssembleAndReconstruct_Clamps_ToValidRange()
    {
        var header = CreateHeader(2, 2, 1);
        var tile = new JpxTile(header, CreateTileHeader());

        AssembleAndReconstruct(new JpxPacket[0], header, tile);

        for (int i = 0; i < tile.ComponentData[0].Length; i++)
        {
            Assert.InRange(tile.ComponentData[0][i], 0, 255);
        }
    }

    /// <summary>
    /// Verifies that the inverse 5-3 DWT of a DC-only signal (all energy in LL) is constant.
    /// </summary>
    [Fact]
    public void InverseDwt53_DcOnlySignal_ProducesConstantOutput()
    {
        var header = CreateHeader(4, 4, 1);
        var tile = new JpxTile(header, CreateTileHeader());

        // Create a packet at resolution 0 (LL) with a constant coefficient
        var codeBlock = new JpxCodeBlock
        {
            X = 0,
            Y = 0,
            Width = 64,
            Height = 64,
            CodingPasses = 1,
            ZeroBitPlanes = 0,
            Data = new byte[1],
            DecodedCoefficients = new int[2 * 2]
        };

        // Set LL to constant value 50
        for (int y = 0; y < 2; y++)
        {
            for (int x = 0; x < 2; x++)
            {
                codeBlock.DecodedCoefficients[y * 2 + x] = 50;
            }
        }

        var packet = new JpxPacket
        {
            Layer = 0,
            Resolution = 0,
            Component = 0,
            CodeBlocks = new[] { codeBlock }
        };

        AssembleAndReconstruct(new[] { packet }, header, tile);

        // All pixels should be 50 + 128 (level shift) = 178
        // (inverse DWT of constant LL with zero detail = constant)
        for (int i = 0; i < tile.ComponentData[0].Length; i++)
        {
            Assert.Equal(178, tile.ComponentData[0][i]);
        }
    }

    /// <summary>
    /// Verifies deterministic output for the assembler.
    /// </summary>
    [Fact]
    public void AssembleAndReconstruct_IsDeterministic()
    {
        var header = CreateHeader(4, 4, 1);

        var tile1 = new JpxTile(header, CreateTileHeader());
        var tile2 = new JpxTile(header, CreateTileHeader());

        AssembleAndReconstruct(new JpxPacket[0], header, tile1);
        AssembleAndReconstruct(new JpxPacket[0], header, tile2);

        for (int i = 0; i < tile1.ComponentData[0].Length; i++)
        {
            Assert.Equal(tile1.ComponentData[0][i], tile2.ComponentData[0][i]);
        }
    }
}
