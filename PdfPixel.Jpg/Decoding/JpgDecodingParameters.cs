using System;

using PdfPixel.Jpg.Model;

namespace PdfPixel.Jpg.Decoding;

/// <summary>
/// <para>
/// Immutable container for JPEG decoding parameters derived from the header.
/// Extracted once to avoid recomputing sizing and sampling invariants during decoding.
/// </para>
/// <para>
/// MCU geometry follows the JPEG spec (T.81, §A.2):
///   - For single-component images the scan is always non-interleaved; each MCU contains
///     exactly one data unit regardless of the component's H/V sampling factors.
///     Effective HMax = VMax = 1, McuWidth = McuHeight = 8.
///   - For multi-component images the scan is interleaved; MCU dimensions are derived from
///     the maximum H/V sampling factors across all components.
/// </para>
/// <para>
/// ComponentBlocksH/V store the effective per-component block counts per MCU dimension
/// (1 for single-component images, actual H/V for multi-component). All decoders must use
/// these values rather than reading sampling factors directly from the header.
/// </para>
/// </summary>
public sealed class JpgDecodingParameters
{
    /// <summary>
    /// Derives all decoding geometry from <paramref name="header"/>.
    /// </summary>
    /// <param name="header">Parsed JPEG header.</param>
    public JpgDecodingParameters(JpgHeader header)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        if (header.ComponentCount <= 0 || header.Components == null || header.Components.Count != header.ComponentCount)
        {
            throw new ArgumentException("Invalid header components.", nameof(header));
        }

        int hMax = 1;
        int vMax = 1;

        // Single-component images are always non-interleaved: one block per MCU regardless of H/V.
        bool singleComponent = header.ComponentCount == 1;
        if (!singleComponent)
        {
            for (int i = 0; i < header.Components.Count; i++)
            {
                JpgComponent c = header.Components[i];
                if (c.HorizontalSamplingFactor > hMax)
                {
                    hMax = c.HorizontalSamplingFactor;
                }

                if (c.VerticalSamplingFactor > vMax)
                {
                    vMax = c.VerticalSamplingFactor;
                }
            }
        }

        HMax = hMax;
        VMax = vMax;
        McuWidth = 8 * hMax;
        McuHeight = 8 * vMax;
        McuColumns = (header.Width + McuWidth - 1) / McuWidth;
        McuRows = (header.Height + McuHeight - 1) / McuHeight;
        UpsampledBlocksPerMcu = hMax * vMax;
        OutputStride = checked(header.Width * header.ComponentCount);

        ComponentBlocksH = new int[header.ComponentCount];
        ComponentBlocksV = new int[header.ComponentCount];
        BlocksPerMcu = new int[header.ComponentCount];
        TotalBlocksPerBand = new int[header.ComponentCount];
        var upsamplingNeeded = false;

        for (int ci = 0; ci < header.ComponentCount; ci++)
        {
            int h = singleComponent ? 1 : header.Components[ci].HorizontalSamplingFactor;
            int v = singleComponent ? 1 : header.Components[ci].VerticalSamplingFactor;
            ComponentBlocksH[ci] = h;
            ComponentBlocksV[ci] = v;
            int blocks = h * v;
            BlocksPerMcu[ci] = blocks;
            TotalBlocksPerBand[ci] = McuColumns * blocks;
            if (h != hMax || v != vMax)
            {
                upsamplingNeeded = true;
            }
        }

        NeedsUpsampling = upsamplingNeeded;
    }

    /// <summary>
    /// Maximum horizontal sampling factor across all components.
    /// </summary>
    public int HMax { get; }

    /// <summary>
    /// Maximum vertical sampling factor across all components.
    /// </summary>
    public int VMax { get; }

    /// <summary>
    /// MCU width in pixels (8 × <see cref="HMax"/>).
    /// </summary>
    public int McuWidth { get; }

    /// <summary>
    /// MCU height in pixels (8 × <see cref="VMax"/>).
    /// </summary>
    public int McuHeight { get; }

    /// <summary>
    /// Number of MCU columns needed to cover the full image width.
    /// </summary>
    public int McuColumns { get; }

    /// <summary>
    /// Number of MCU rows needed to cover the full image height.
    /// </summary>
    public int McuRows { get; }

    /// <summary>
    /// Byte stride of one decoded output row (Width × ComponentCount).
    /// </summary>
    public int OutputStride { get; }

    /// <summary>
    /// Number of full-resolution 8×8 blocks per MCU after upsampling (<see cref="HMax"/> × <see cref="VMax"/>).
    /// </summary>
    public int UpsampledBlocksPerMcu { get; }

    /// <summary>
    /// Per-component horizontal 8×8 block count per MCU (1 for single-component images, H sampling factor otherwise).
    /// </summary>
    public int[] ComponentBlocksH { get; }

    /// <summary>
    /// Per-component vertical 8×8 block count per MCU (1 for single-component images, V sampling factor otherwise).
    /// </summary>
    public int[] ComponentBlocksV { get; }

    /// <summary>
    /// Per-component number of 8×8 blocks per MCU (<c>ComponentBlocksH[i] × ComponentBlocksV[i]</c>).
    /// </summary>
    public int[] BlocksPerMcu { get; }

    /// <summary>
    /// Per-component total 8×8 blocks in one MCU-height band (<c>McuColumns × BlocksPerMcu[i]</c>).
    /// </summary>
    public int[] TotalBlocksPerBand { get; }

    /// <summary>
    /// True when at least one component has a smaller sampling factor than <see cref="HMax"/>/<see cref="VMax"/> and must be upsampled.
    /// </summary>
    public bool NeedsUpsampling { get; }
}
