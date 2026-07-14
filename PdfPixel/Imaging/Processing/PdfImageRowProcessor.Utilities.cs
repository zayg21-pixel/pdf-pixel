using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Structures;
using PdfPixel.Color.Transform;
using PdfPixel.Models;
using System;

namespace PdfPixel.Imaging.Processing;

internal sealed partial class PdfImageRowProcessor
{
    private static RgbaPacked[] BuildPackedPalette(PdfImageRowDecodingParameters parameters, int bitsPerComponent)
    {
        ColorTransformSampler sampler = parameters.ColorSpaceConverter.GetRgbaSampler(parameters.RenderingIntent, parameters.Context.FullTransferFunction);
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

        if (ShouldApplyDecode(parameters.Decode, converter.Components, parameters.BitsPerComponent, converter is IndexedConverter))
        {
            stages |= ProcessingStages.Decode;
        }

        if (parameters.MaskArray != null && parameters.MaskArray.Length == converter.Components * 2)
        {
            stages |= ProcessingStages.Mask;
        }

        if (!converter.IsDevice || converter.Components != 1 || converter.Components != 3)
        {
            stages |= ProcessingStages.SampleColor;
        }

        return stages;
    }
}
