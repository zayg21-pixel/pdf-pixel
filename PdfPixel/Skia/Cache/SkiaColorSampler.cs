using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Icc;
using PdfPixel.Color.Paint;
using PdfPixel.Color.Transform;
using PdfPixel.Commands.Cache;
using SkiaSharp;
using System;
using System.Numerics;

namespace PdfPixel.Skia.Cache;

/// <summary>
/// Converts colors given in the color space of a <see cref="PdfPaint"/>, with its color conversion, to Skia colors.
/// </summary>
internal sealed class SkiaColorSampler : ICommandCacheItem
{
    private const int RgbComponentCount = 3;

    private static readonly IccProfileTransform CmykTransform = new(ProfileResources.GetCmykProfile());
    private static readonly PdfRgbaColor TransparentColor = new(0f, 0f, 0f, 0f);
    private static readonly FunctionColorTransform GrayToRgbTransform = new(x => new Vector4(x.X, x.X, x.X, 1f));

    public SkiaColorSampler(PdfPaint paint)
    {
        if (paint == null)
        {
            throw new ArgumentNullException(nameof(paint));
        }

        IPdfColorTransform colorTransform = CreateTransform(paint.ColorSpace, paint.DeviceColorMapping, paint);

        if (paint.TransferFunction == null)
        {
            Sampler = new PdfColorSampler([colorTransform]);
        }
        else
        {
            Sampler = new PdfColorSampler([colorTransform, new PdfTransferFunctionColorTransform(paint.TransferFunction.Functions, RgbComponentCount)]);
        }
    }

    /// <summary>
    /// Gets the sampler converting the paint's colors to RGB.
    /// </summary>
    public PdfColorSampler Sampler { get; }

    /// <summary>
    /// Converts the color of <paramref name="paint"/>, with its alpha.
    /// </summary>
    public SKColor ToSkColor(PdfPaint paint) => ToRgbaColor(paint).ToSkiaColor();

    /// <summary>
    /// Converts <paramref name="color"/> to an opaque Skia color.
    /// </summary>
    public SKColor ToSkColor(in PdfColor color) => ToRgbaColor(color, 1f).ToSkiaColor();

    /// <summary>
    /// Converts the color of <paramref name="paint"/> to RGB, with its alpha.
    /// </summary>
    public PdfRgbaColor ToRgbaColor(PdfPaint paint)
    {
        // Pattern color space without a pattern paints nothing (ISO 32000-2, Table 73).
        if (paint.Pattern == null && paint.ColorSpace is PdfPatternColorSpaceConverter)
        {
            return TransparentColor;
        }

        return ToRgbaColor(paint.Color, paint.Alpha);
    }

    private PdfRgbaColor ToRgbaColor(in PdfColor color, float alpha)
    {
        ReadOnlySpan<float> sampledColor = Sampler.Sample(color.Components.Span);

        return new PdfRgbaColor(sampledColor[0], sampledColor[1], sampledColor[2], alpha);
    }

    private static IPdfColorTransform CreateTransform(PdfColorSpaceConverter colorSpace, PdfDeviceColorMapping deviceColorMapping, PdfPaint paint)
    {
        PdfColorSpaceConverter mappedColorSpace = deviceColorMapping.Map(colorSpace);

        // A default color space converts with device color spaces as is, so its own device fallbacks do not map back to it.
        if (mappedColorSpace != colorSpace)
        {
            return CreateTransform(mappedColorSpace, PdfDeviceColorMapping.None, paint);
        }

        switch (colorSpace)
        {
            case PdfIndexedColorSpaceConverter indexed:
            {
                return new PdfPaletteColorTransform(indexed.Palette, CreateTransform(indexed.BaseColorSpace, deviceColorMapping, paint));
            }
            case PdfSeparationColorSpaceConverter separation:
            {
                return new PdfTintColorTransform(separation.Components, separation.TintTransform, CreateTransform(separation.AlternateSpace, deviceColorMapping, paint));
            }
            case PdfDeviceNColorSpaceConverter deviceN:
            {
                return new PdfTintColorTransform(deviceN.Components, deviceN.TintTransform, CreateTransform(deviceN.AlternateSpace, deviceColorMapping, paint));
            }
            case PdfPatternColorSpaceConverter { UnderlyingColorSpace: { } underlyingColorSpace }:
            {
                return CreateTransform(underlyingColorSpace, deviceColorMapping, paint);
            }
            default:
            {
                return CreateRgbTransform(colorSpace, paint);
            }
        }
    }

    private static PdfChainedColorTransform CreateRgbTransform(PdfColorSpaceConverter colorSpace, PdfPaint paint)
    {
        ChainedColorTransform? profileConnectionTransform = colorSpace.GetProfileConnectionTransform(paint.RenderingIntent, paint.UseBlackPointCompensation);
        ChainedColorTransform toRgbTransform;

        if (profileConnectionTransform != null)
        {
            toRgbTransform = new ChainedColorTransform(profileConnectionTransform, IccTransforms.XyzD50ToSrgbTransform);
        }
        else
        {
            toRgbTransform = new ChainedColorTransform(CreateDeviceTransform(colorSpace.Components, paint));
        }

        return new PdfChainedColorTransform(toRgbTransform, colorSpace.Components, RgbComponentCount);
    }

    private static IColorTransform? CreateDeviceTransform(int componentCount, PdfPaint paint)
    {
        switch (componentCount)
        {
            case 0:
            case 3:
            {
                return null;
            }
            case 1:
            {
                return GrayToRgbTransform;
            }
            case 4:
            {
                return CmykTransform.GetIntentTransform(paint.RenderingIntent.ToIccRenderingIntent(), paint.UseBlackPointCompensation);
            }
            default:
            {
                throw new NotSupportedException($"A device color space with {componentCount} components has no conversion to RGB.");
            }
        }
    }

    public void Dispose()
    {
    }
}
