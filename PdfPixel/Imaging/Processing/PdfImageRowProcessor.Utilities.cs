using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Structures;
using PdfPixel.Color.Transform;
using PdfPixel.Models;
using System;
using System.Numerics;

namespace PdfPixel.Imaging.Processing;

internal sealed partial class PdfImageRowProcessor
{
    private static RgbaPacked[] BuildPackedPalette(PdfImageRowDecodingParameters parameters, int bitsPerComponent)
    {
        ColorTransformSampler sampler = parameters.ColorSpaceConverter.GetRgbaSampler(parameters.RenderingIntent, parameters.Context.TransferFunction);
        int paletteSize = 1 << bitsPerComponent;
        var palette = new RgbaPacked[paletteSize];
        Span<float> comps = stackalloc float[1];
        int maxCode = paletteSize - 1;

        for (int code = 0; code < paletteSize; code++)
        {
            comps[0] = (maxCode == 0) ? 0f : (float)code / maxCode;
            ColorVectorUtilities.Load01ToRgba(sampler.Sample(comps), ref palette[code]);
        }

        return palette;
    }

    /// <summary>
    /// Builds the two-entry palette of a stencil mask, mapping each sample straight to the fill
    /// color at full or zero coverage. A Decode of [1 0] swaps which sample value paints.
    /// </summary>
    private static RgbaPacked[] BuildStencilMaskPalette(PdfImageRowDecodingParameters parameters)
    {
        PdfColor fillColor = parameters.Context.FillPaint.Color;
        Vector4 fillVector = new(fillColor.Red, fillColor.Green, fillColor.Blue, 0f);
        RgbaPacked painted = default;
        ColorVectorUtilities.Load01ToRgba(fillVector, ref painted);
        RgbaPacked clear = painted;
        clear.A = 0;

        PdfRange[]? decode = parameters.Decode;
        bool paintsOnZero = decode == null || decode.Length < 1 || decode[0].Min < decode[0].Max;

        return paintsOnZero
            ? new[] { painted, clear }
            : new[] { clear, painted };
    }

    /// <summary>
    /// Rebuilds the palette so that a raw sample read from the row selects its final pixel directly, with the
    /// /Decode mapping and the colour key mask already applied. The result is indexed by sample value and so
    /// spans the whole sample domain rather than the source palette's entry count. Returns the source palette
    /// unchanged when neither stage applies.
    /// </summary>
    /// <param name="palette">The palette built from the image's colour space.</param>
    private RgbaPacked[] FoldDecodeAndMaskIntoPalette(RgbaPacked[] palette)
    {
        bool applyDecode = (_stages & RowStages.Decode) != 0;
        bool applyMask = (_stages & RowStages.ColorKeyMask) != 0;

        if (!applyDecode && !applyMask)
        {
            return palette;
        }

        float decodeMin = 0f;
        float decodeScale = 0f;

        if (applyDecode)
        {
            PdfRange decodeRange = _decodeRanges[0];
            decodeMin = decodeRange.Min;
            decodeScale = decodeRange.Range * _scale;
        }

        uint maskMinCode = 0;
        uint maskMaxCode = 0;

        if (applyMask)
        {
            maskMinCode = (uint)_maskArray[0];
            maskMaxCode = (uint)_maskArray[1];
        }

        var maxPaletteIndex = (uint)(palette.Length - 1);
        int sampleCount = 1 << _indexedBitsPerComponent;
        var foldedPalette = new RgbaPacked[sampleCount];

        for (int sample = 0; sample < sampleCount; sample++)
        {
            var paletteIndex = (uint)sample;

            if (applyDecode)
            {
                paletteIndex = (uint)Math.Max(0f, decodeMin + (paletteIndex * decodeScale));
            }

            paletteIndex = Math.Min(paletteIndex, maxPaletteIndex);

            RgbaPacked pixel = palette[paletteIndex];

            if (applyMask && paletteIndex >= maskMinCode && paletteIndex <= maskMaxCode)
            {
                pixel.A = 0;
            }

            foldedPalette[sample] = pixel;
        }

        return foldedPalette;
    }

    private static bool ShouldApplyDecode(PdfRange[]? decode, int componentCount, int bitsPerComponent, bool indexed)
    {
        if (decode == null || decode.Length != componentCount)
        {
            return false;
        }

        float defaultMax = indexed ? (1 << bitsPerComponent) - 1 : 1f;

        for (int i = 0; i < componentCount; i++)
        {
            if (decode[i].Min != 0f || decode[i].Max != defaultMax)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Collects the stages a row has to run. Matte is added later by the constructor, which is where
    /// the backdrop it needs is resolved. A stencil mask carries its /Decode in its own palette, so it
    /// reports none of the colour stages.
    /// </summary>
    private static RowStages GetRowStages(PdfImageRowDecodingParameters parameters)
    {
        var stages = RowStages.None;

        if (parameters.IsAlphaInterleaved)
        {
            stages |= RowStages.AlphaInterleaved;
        }
        else if (parameters.HasAlphaPlane)
        {
            stages |= RowStages.AlphaPlane;
        }

        if (parameters.HasImageMask)
        {
            return stages;
        }

        PdfColorSpaceConverter converter = parameters.ColorSpaceConverter;

        if (ShouldApplyDecode(parameters.Decode, converter.Components, parameters.BitsPerComponent, converter is PdfIndexedColorSpaceConverter))
        {
            stages |= RowStages.Decode;
        }

        if (parameters.MaskArray != null && parameters.MaskArray.Length == converter.Components * 2)
        {
            stages |= RowStages.ColorKeyMask;
        }

        return stages;
    }

    /// <summary>
    /// Whether the samples have to reach a colour space converter at all, which is what rules out the
    /// direct routes even when no other stage applies.
    /// </summary>
    private static bool RequiresColorTransform(PdfImageRowDecodingParameters parameters)
    {
        if (parameters.HasImageMask)
        {
            return false;
        }

        PdfColorSpaceConverter converter = parameters.ColorSpaceConverter;

        return !converter.IsDevice
            || (converter.Components != 1 && converter.Components != 3)
            || parameters.Context.TransferFunction != null;
    }
}
