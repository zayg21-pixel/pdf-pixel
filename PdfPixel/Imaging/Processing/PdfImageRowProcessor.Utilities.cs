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
            palette[code] = sampler.Sample(comps).From01ToRgba();
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
        RgbaPacked painted = new Vector4(fillColor.Red, fillColor.Green, fillColor.Blue, 0f).From01ToRgba();
        RgbaPacked clear = painted;
        clear.A = 0;

        PdfRange[]? decode = parameters.Decode;
        bool paintsOnZero = decode == null || decode.Length < 1 || decode[0].Min < decode[0].Max;

        return paintsOnZero
            ? new[] { painted, clear }
            : new[] { clear, painted };
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

    private static ProcessingStages GetProcessingStages(PdfImageRowDecodingParameters parameters)
    {
        PdfColorSpaceConverter converter = parameters.ColorSpaceConverter;

        if (converter == null || parameters.HasImageMask)
        {
            return ProcessingStages.None;
        }

        var stages = ProcessingStages.None;

        if (ShouldApplyDecode(parameters.Decode, converter.Components, parameters.BitsPerComponent, converter is PdfIndexedColorSpaceConverter))
        {
            stages |= ProcessingStages.Decode;
        }

        if (parameters.MaskArray != null && parameters.MaskArray.Length == converter.Components * 2)
        {
            stages |= ProcessingStages.Mask;
        }

        if (!converter.IsDevice
            || (converter.Components != 1 && converter.Components != 3)
            || parameters.Context.TransferFunction != null)
        {
            stages |= ProcessingStages.SampleColor;
        }

        return stages;
    }
}
