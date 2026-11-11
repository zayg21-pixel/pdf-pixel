using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Transform;
using PdfPixel.Functions;
using PdfPixel.Objects;
using PdfPixel.Rendering;
using PdfPixel.Snap.Transparency;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Color;

/// <summary>
/// Converts colors from a source into a target color space, applying the transfer function last.
/// </summary>
internal sealed class SnapColorConverter
{
    private const int GrayFunctionIndex = 3;

    private static readonly PdfRange ComplementRange = new(1f, 0f);

    private readonly uint _targetByteCount;
    private readonly PdfColorSampler _sampler;

    public SnapColorConverter(in SnapColorConverterKey key, PdfDeviceProfiles deviceProfiles)
    {
        int targetComponentCount = key.TargetColorSpace.Components;
        IsIdentity = key.TransferFunction == null
            && ReferenceEquals(key.DeviceColorMapping.Map(key.SourceColorSpace), key.TargetColorSpace);

        IPdfColorTransform colorTransform = CreateTransform(key.SourceColorSpace, key.DeviceColorMapping, key, deviceProfiles);

        if (key.TransferFunction == null)
        {
            _sampler = new PdfColorSampler([colorTransform]);
        }
        else
        {
            _sampler = new PdfColorSampler(CreateTransferTransforms(colorTransform, key.TransferFunction, key.TargetColorSpace.GetBlendingColorModel()));
        }

        if (IsIdentity)
        {
            SourceComponentCount = targetComponentCount;
        }
        else
        {
            SourceComponentCount = _sampler.InputComponentCount;
        }

        _targetByteCount = (uint)(targetComponentCount * sizeof(float));
    }

    /// <summary>
    /// Gets the number of components <see cref="Convert"/> reads.
    /// </summary>
    public int SourceComponentCount { get; }

    /// <summary>
    /// Gets whether <see cref="Convert"/> copies the components unchanged.
    /// </summary>
    public bool IsIdentity { get; }

    /// <summary>
    /// Converts the <see cref="SourceComponentCount"/> components at <paramref name="components"/> into
    /// <paramref name="destination"/>.
    /// </summary>
    public void Convert(ref float components, ref float destination)
    {
        if (IsIdentity)
        {
            Unsafe.CopyBlockUnaligned(ref Unsafe.As<float, byte>(ref destination), ref Unsafe.As<float, byte>(ref components), _targetByteCount);
        }
        else
        {
            _sampler.Sample(ref components, ref destination);
        }
    }

    private static IPdfColorTransform[] CreateTransferTransforms(IPdfColorTransform colorTransform, PdfTransferFunction transferFunction, SnapBlendingColorModel colorModel)
    {
        PdfFunction[] functions = transferFunction.Functions;

        switch (colorModel)
        {
            case SnapBlendingColorModel.Gray:
            {
                PdfTransferFunctionColorTransform grayTransform = new([functions[GrayFunctionIndex]], 1);

                return new IPdfColorTransform[] { colorTransform, grayTransform };
            }
            case SnapBlendingColorModel.Rgb:
            {
                PdfTransferFunctionColorTransform rgbTransform = new(functions, 3);

                return new IPdfColorTransform[] { colorTransform, rgbTransform };
            }
            case SnapBlendingColorModel.Cmyk:
            {
                PdfRange[] complement = [ComplementRange, ComplementRange, ComplementRange, ComplementRange];
                PdfDecodeColorTransform complementTransform = new(complement);
                PdfTransferFunctionColorTransform cmykTransform = new(functions, 4);

                return new IPdfColorTransform[] { colorTransform, complementTransform, cmykTransform, complementTransform };
            }
            default:
            {
                throw new NotSupportedException($"No transfer function applies to the {colorModel} color model.");
            }
        }
    }

    private static IPdfColorTransform CreateTransform(PdfColorSpaceConverter colorSpace, PdfDeviceColorMapping deviceColorMapping, in SnapColorConverterKey key, PdfDeviceProfiles deviceProfiles)
    {
        PdfColorSpaceConverter mappedColorSpace = deviceColorMapping.Map(colorSpace);

        // A default color space converts with device color spaces as is, so its own device fallbacks do not map back to it.
        if (mappedColorSpace != colorSpace)
        {
            return CreateTransform(mappedColorSpace, PdfDeviceColorMapping.None, key, deviceProfiles);
        }

        switch (colorSpace)
        {
            case PdfIndexedColorSpaceConverter indexed:
            {
                return new PdfPaletteColorTransform(indexed.Palette, CreateTransform(indexed.BaseColorSpace, deviceColorMapping, key, deviceProfiles));
            }
            case PdfSeparationColorSpaceConverter separation:
            {
                return new PdfTintColorTransform(separation.Components, separation.TintTransform, CreateTransform(separation.AlternateSpace, deviceColorMapping, key, deviceProfiles));
            }
            case PdfDeviceNColorSpaceConverter deviceN:
            {
                return new PdfTintColorTransform(deviceN.Components, deviceN.TintTransform, CreateTransform(deviceN.AlternateSpace, deviceColorMapping, key, deviceProfiles));
            }
            case PdfPatternColorSpaceConverter { UnderlyingColorSpace: { } underlyingColorSpace }:
            {
                return CreateTransform(underlyingColorSpace, deviceColorMapping, key, deviceProfiles);
            }
            default:
            {
                return CreateTargetTransform(colorSpace, key, deviceProfiles);
            }
        }
    }

    private static PdfChainedColorTransform CreateTargetTransform(PdfColorSpaceConverter colorSpace, in SnapColorConverterKey key, PdfDeviceProfiles deviceProfiles)
    {
        PdfColorSpaceConverter targetColorSpace = key.TargetColorSpace;
        int targetComponentCount = targetColorSpace.Components;

        if (ReferenceEquals(colorSpace, targetColorSpace))
        {
            return new PdfChainedColorTransform(new ChainedColorTransform(), colorSpace.Components, targetComponentCount);
        }
        else
        {
            ChainedColorTransform profileConnectionTransform = colorSpace.GetProfileConnectionTransform(key.RenderingIntent, key.UseBlackPointCompensation)
                ?? deviceProfiles.GetProfileConnectionTransform(colorSpace.Components, key.RenderingIntent, key.UseBlackPointCompensation);
            ChainedColorTransform fromProfileConnectionTransform = targetColorSpace.GetFromProfileConnectionTransform(key.RenderingIntent, key.UseBlackPointCompensation)
                ?? deviceProfiles.GetFromProfileConnectionTransform(targetComponentCount, key.RenderingIntent, key.UseBlackPointCompensation);
            ChainedColorTransform toTargetTransform = new(profileConnectionTransform, fromProfileConnectionTransform);

            return new PdfChainedColorTransform(toTargetTransform, colorSpace.Components, targetComponentCount);
        }
    }
}
