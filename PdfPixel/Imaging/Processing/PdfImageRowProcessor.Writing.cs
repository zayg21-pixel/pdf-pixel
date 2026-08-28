using PdfPixel.Color.Structures;
using PdfPixel.Color.Transform;
using PdfPixel.Parsing;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Processing;

internal sealed partial class PdfImageRowProcessor
{
    /// <summary>
    /// Expands the row through the palette and merges the alpha plane over the source grid, because
    /// palette indexes cannot be averaged, then resamples the finished color to the output grid.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecodePaletteRow(int rowIndex, in ReadOnlySpan<byte> sourceRow, in ReadOnlySpan<byte> alphaRow, PdfImageRowTarget target)
    {
        if (target.ColorConverter == null)
        {
            Span<byte> outputRow = target.CurrentRow;
            ExpandPaletteToRgba(sourceRow, GetSourceStartBit(target), target.SourceWidth, outputRow);
            MergeAlphaPlane(target.SourceWidth, alphaRow, outputRow);
            ApplyMatte(outputRow, target.OutputWidth);
            target.AdvanceRow();
            return;
        }

        if (_paletteRgbaBuffer == null)
        {
            throw new InvalidOperationException("Resampled palette rows require a source-grid buffer.");
        }

        ExpandPaletteToRgba(sourceRow, GetSourceStartBit(target), target.SourceWidth, _paletteRgbaBuffer);
        MergeAlphaPlane(target.SourceWidth, alphaRow, _paletteRgbaBuffer);

        Span<byte> destRow = target.CurrentRow;

        if (!target.ColorConverter.TryConvertRow(rowIndex, _paletteRgbaBuffer, sourceStartBit: 0, destRow))
        {
            return;
        }

        ApplyMatte(destRow, target.OutputWidth);
        target.AdvanceRow();
    }

    /// <summary>
    /// Resamples at the source bit depth, converts the result through the colour space, and merges
    /// the alpha plane that was resampled alongside it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecodeTransformedRow(int rowIndex, in ReadOnlySpan<byte> sourceRow, in ReadOnlySpan<byte> alphaRow, PdfImageRowTarget target)
    {
        if (target.ColorConverter == null || _convertedRowBuffer == null)
        {
            throw new InvalidOperationException("Transformed rows require a resampler and a conversion buffer.");
        }

        bool hasResampledAlpha = (_stages & RowStages.AlphaPlane) != 0 && TryResampleAlphaRow(rowIndex, alphaRow, target);

        if (!target.ColorConverter.TryConvertRow(rowIndex, sourceRow, GetSourceStartBit(target), _convertedRowBuffer))
        {
            return;
        }

        Span<byte> destRow = target.CurrentRow;

        TransformColorToRgba(_convertedRowBuffer, destRow, target.OutputWidth);

        if (hasResampledAlpha)
        {
            MergeAlphaPlane(target.OutputWidth, _convertedAlphaBuffer, destRow);
        }

        ApplyMatte(destRow, target.OutputWidth);
        target.AdvanceRow();
    }

    /// <summary>
    /// Resamples the single gray channel and lays it out according to the alpha stage in effect.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecodeDirectGrayRow(int rowIndex, in ReadOnlySpan<byte> sourceRow, in ReadOnlySpan<byte> alphaRow, PdfImageRowTarget target)
    {
        if (target.ColorConverter == null)
        {
            throw new InvalidOperationException("Direct gray rows require a resampler.");
        }

        bool hasResampledAlpha = (_stages & RowStages.AlphaPlane) != 0 && TryResampleAlphaRow(rowIndex, alphaRow, target);

        if ((_stages & AlphaStages) == 0)
        {
            Span<byte> outputRow = target.CurrentRow;

            if (!target.ColorConverter.TryConvertRow(rowIndex, sourceRow, GetSourceStartBit(target), outputRow))
            {
                return;
            }

            ApplyMatte(outputRow, target.OutputWidth);
            target.AdvanceRow();
            return;
        }

        if (_convertedRowBuffer == null)
        {
            throw new InvalidOperationException("Gray rows carrying alpha require a conversion buffer.");
        }

        if (!target.ColorConverter.TryConvertRow(rowIndex, sourceRow, GetSourceStartBit(target), _convertedRowBuffer))
        {
            return;
        }

        Span<byte> destRow = target.CurrentRow;

        if ((_stages & RowStages.AlphaInterleaved) != 0)
        {
            ExpandInterleavedGrayToRgba(_convertedRowBuffer, destRow, target.OutputWidth);
        }
        else if (hasResampledAlpha)
        {
            ExpandGrayToRgba(_convertedRowBuffer, _convertedAlphaBuffer, destRow, target.OutputWidth);
        }
        else
        {
            ExpandGrayToOpaqueRgba(_convertedRowBuffer, destRow, target.OutputWidth);
        }

        ApplyMatte(destRow, target.OutputWidth);
        target.AdvanceRow();
    }

    /// <summary>
    /// Resamples the three colour channels and lays them out according to the alpha stage in effect.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DecodeDirectRgbRow(int rowIndex, in ReadOnlySpan<byte> sourceRow, in ReadOnlySpan<byte> alphaRow, PdfImageRowTarget target)
    {
        if (target.ColorConverter == null)
        {
            throw new InvalidOperationException("Direct RGB rows require a resampler.");
        }

        bool hasResampledAlpha = (_stages & RowStages.AlphaPlane) != 0 && TryResampleAlphaRow(rowIndex, alphaRow, target);

        // Interleaved alpha rode through the resampler as a fourth channel, so the row is already RGBA.
        if ((_stages & RowStages.AlphaInterleaved) != 0)
        {
            Span<byte> outputRow = target.CurrentRow;

            if (!target.ColorConverter.TryConvertRow(rowIndex, sourceRow, GetSourceStartBit(target), outputRow))
            {
                return;
            }

            ApplyMatte(outputRow, target.OutputWidth);
            target.AdvanceRow();
            return;
        }

        if (_convertedRowBuffer == null)
        {
            throw new InvalidOperationException("RGB rows expanding to RGBA require a conversion buffer.");
        }

        if (!target.ColorConverter.TryConvertRow(rowIndex, sourceRow, GetSourceStartBit(target), _convertedRowBuffer))
        {
            return;
        }

        Span<byte> destRow = target.CurrentRow;

        if (hasResampledAlpha)
        {
            ExpandRgbToRgba(_convertedRowBuffer, _convertedAlphaBuffer, destRow, target.OutputWidth);
        }
        else
        {
            ExpandRgbToOpaqueRgba(_convertedRowBuffer, destRow, target.OutputWidth);
        }

        ApplyMatte(destRow, target.OutputWidth);
        target.AdvanceRow();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExpandPaletteToRgba(in ReadOnlySpan<byte> sourceRow, int sourceStartBit, int pixelCount, in Span<byte> destination)
    {
        if (_indexedPalette == null)
        {
            throw new InvalidOperationException("Not initialized.");
        }

        RgbaPacked[] palette = _indexedPalette;
        ref RgbaPacked paletteRef = ref palette[0];
        var maxPaletteIndex = (uint)(palette.Length - 1);
        ref RgbaPacked destPixel = ref Unsafe.As<byte, RgbaPacked>(ref destination[0]);
        UintBitReaderFixedLength bitReader = new(sourceRow, _indexedBitsPerComponent, sourceStartBit);

        for (int x = 0; x < pixelCount; x++)
        {
            uint sample = Math.Min(bitReader.Read(), maxPaletteIndex);
            destPixel = Unsafe.Add(ref paletteRef, sample);
            destPixel = ref Unsafe.Add(ref destPixel, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TransformColorToRgba(in ReadOnlySpan<byte> normalizedRow, in Span<byte> destRow, int pixelCount)
    {
        if (_sampler == null)
        {
            throw new InvalidOperationException("Not initialized.");
        }

        ref RgbaPacked destPixel = ref Unsafe.As<byte, RgbaPacked>(ref destRow[0]);
        ref byte sourceByte = ref Unsafe.AsRef(in normalizedRow[0]);

        Span<float> componentValues = stackalloc float[_components];

        bool applyDecode = (_stages & RowStages.Decode) != 0;
        bool applyMask = (_stages & RowStages.ColorKeyMask) != 0;
        bool readInterleavedAlpha = (_stages & RowStages.AlphaInterleaved) != 0;

        for (int x = 0; x < pixelCount; x++)
        {
            bool maskMatch = applyMask;

            for (int c = 0; c < _components; c++)
            {
                byte sample = sourceByte;
                sourceByte = ref Unsafe.Add(ref sourceByte, 1);

                if (applyMask && maskMatch)
                {
                    int minCode = _maskArray[c * 2];
                    int maxCodeRange = _maskArray[(c * 2) + 1];

                    if (sample < minCode || sample > maxCodeRange)
                    {
                        maskMatch = false;
                    }
                }

                float value01 = sample * _scale;

                if (applyDecode)
                {
                    value01 = _decodeRanges[c].Denormalize(value01);
                }

                componentValues[c] = value01;
            }

            Vector4 colorVector = _sampler.Sample(componentValues);
            ColorVectorUtilities.Load01ToRgba(colorVector, ref destPixel);

            if (readInterleavedAlpha)
            {
                destPixel.A = sourceByte;
                sourceByte = ref Unsafe.Add(ref sourceByte, 1);
            }

            if (applyMask && maskMatch)
            {
                destPixel.A = 0;
            }

            destPixel = ref Unsafe.Add(ref destPixel, 1);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExpandInterleavedGrayToRgba(in ReadOnlySpan<byte> normalizedRow, in Span<byte> destRow, int pixelCount)
    {
        ref byte source = ref Unsafe.AsRef(in normalizedRow[0]);
        ref uint destPixel = ref Unsafe.As<byte, uint>(ref destRow[0]);

        for (int x = 0; x < pixelCount; x++)
        {
            int offset = x * 2;
            uint gray = Unsafe.Add(ref source, offset);
            uint alpha = Unsafe.Add(ref source, offset + 1);
            Unsafe.Add(ref destPixel, x) = gray | (gray << 8) | (gray << 16) | (alpha << 24);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExpandGrayToRgba(in ReadOnlySpan<byte> normalizedRow, in ReadOnlySpan<byte> alphaRow, in Span<byte> destRow, int pixelCount)
    {
        ref byte source = ref Unsafe.AsRef(in normalizedRow[0]);
        ref byte sourceAlpha = ref Unsafe.AsRef(in alphaRow[0]);
        ref uint destPixel = ref Unsafe.As<byte, uint>(ref destRow[0]);

        for (int x = 0; x < pixelCount; x++)
        {
            uint gray = Unsafe.Add(ref source, x);
            uint alpha = Unsafe.Add(ref sourceAlpha, x);
            Unsafe.Add(ref destPixel, x) = gray | (gray << 8) | (gray << 16) | (alpha << 24);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExpandGrayToOpaqueRgba(in ReadOnlySpan<byte> normalizedRow, in Span<byte> destRow, int pixelCount)
    {
        ref byte source = ref Unsafe.AsRef(in normalizedRow[0]);
        ref uint destPixel = ref Unsafe.As<byte, uint>(ref destRow[0]);

        for (int x = 0; x < pixelCount; x++)
        {
            uint gray = Unsafe.Add(ref source, x);
            Unsafe.Add(ref destPixel, x) = gray | (gray << 8) | (gray << 16) | 0xFF000000;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExpandRgbToRgba(in ReadOnlySpan<byte> normalizedRow, in ReadOnlySpan<byte> alphaRow, in Span<byte> destRow, int pixelCount)
    {
        ref byte source = ref Unsafe.AsRef(in normalizedRow[0]);
        ref byte sourceAlpha = ref Unsafe.AsRef(in alphaRow[0]);
        ref uint destPixel = ref Unsafe.As<byte, uint>(ref destRow[0]);

        for (int x = 0; x < pixelCount; x++)
        {
            uint rgb = Unsafe.As<byte, uint>(ref Unsafe.Add(ref source, x * 3));
            uint alpha = Unsafe.Add(ref sourceAlpha, x);
            Unsafe.Add(ref destPixel, x) = (rgb & 0x00FFFFFF) | (alpha << 24);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExpandRgbToOpaqueRgba(in ReadOnlySpan<byte> normalizedRow, in Span<byte> destRow, int pixelCount)
    {
        ref byte source = ref Unsafe.AsRef(in normalizedRow[0]);
        ref uint destPixel = ref Unsafe.As<byte, uint>(ref destRow[0]);

        for (int x = 0; x < pixelCount; x++)
        {
            uint rgb = Unsafe.As<byte, uint>(ref Unsafe.Add(ref source, x * 3));
            Unsafe.Add(ref destPixel, x) = rgb | 0xFF000000;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryResampleAlphaRow(int rowIndex, in ReadOnlySpan<byte> alphaRow, PdfImageRowTarget target)
    {
        if (target.AlphaConverter == null || _convertedAlphaBuffer == null || alphaRow.IsEmpty)
        {
            return false;
        }

        return target.AlphaConverter.TryConvertRow(rowIndex, alphaRow, sourceStartBit: 0, _convertedAlphaBuffer);
    }

    /// <summary>
    /// Takes the alpha plane into <paramref name="destRow"/> over <paramref name="pixelCount"/>
    /// pixels of whichever grid the calling pipeline merges on. Samples already cleared — a colour
    /// key match, or a stencil's blank palette entry — stay clear; nothing else carries alpha of its
    /// own here, because an interleaved source excludes a plane.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void MergeAlphaPlane(int pixelCount, in ReadOnlySpan<byte> alphaRow, in Span<byte> destRow)
    {
        if ((_stages & RowStages.AlphaPlane) == 0 || destRow.IsEmpty || alphaRow.IsEmpty)
        {
            return;
        }

        int mergedCount = Math.Min(pixelCount, alphaRow.Length);
        ref RgbaPacked destPixel = ref Unsafe.As<byte, RgbaPacked>(ref destRow[0]);
        ref byte sourceAlpha = ref Unsafe.AsRef(in alphaRow[0]);

        for (int x = 0; x < mergedCount; x++)
        {
            ref RgbaPacked pixel = ref Unsafe.Add(ref destPixel, x);

            if (pixel.A != 0)
            {
                pixel.A = Unsafe.Add(ref sourceAlpha, x);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyMatte(in Span<byte> destRow, int pixelCount)
    {
        if ((_stages & RowStages.Matte) == 0)
        {
            return;
        }

        UndoMattePreblend(destRow, pixelCount);
    }

    /// <summary>
    /// Recovers the unblended color of samples the soft mask's /Matte declares preblended,
    /// as c = m + (c' - m) / a. Samples with zero alpha take the backdrop.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UndoMattePreblend(in Span<byte> destRow, int pixelCount)
    {
        Vector4 backdrop = _backdrop;
        ref RgbaPacked pixel = ref Unsafe.As<byte, RgbaPacked>(ref destRow[0]);

        for (int x = 0; x < pixelCount; x++)
        {
            ref RgbaPacked current = ref Unsafe.Add(ref pixel, x);

            // A zero reciprocal collapses the expression to the backdrop, which is what zero alpha takes.
            float inverseAlpha = (current.A == 0) ? 0f : (255f / current.A);
            Vector4 unblended = backdrop + ((current.FromRgbaTo01() - backdrop) * inverseAlpha);

            ColorVectorUtilities.Load01ToRgb(unblended, ref current);
        }
    }
}
