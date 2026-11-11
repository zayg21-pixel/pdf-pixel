using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Transform;
using System;
using System.Collections.Generic;

namespace PdfPixel.Color;

/// <summary>
/// The CIE-based color spaces device gray, RGB and CMYK values mean when they are converted, as sources and as
/// targets alike.
/// </summary>
public sealed class PdfDeviceProfiles
{
    private static readonly PdfIccColorSpaceConverter BuiltInCmyk = new(ProfileResources.GetCmykProfile());

    /// <summary>
    /// Initializes the device profiles; a <see langword="null"/> profile keeps the built-in one for its component
    /// count: sRGB gray, sRGB, or the bundled CMYK profile.
    /// </summary>
    /// <param name="gray">The CIE-based color space of one component DeviceGray values are in.</param>
    /// <param name="rgb">The CIE-based color space of three components DeviceRGB values are in.</param>
    /// <param name="cmyk">The CIE-based color space of four components DeviceCMYK values are in.</param>
    /// <exception cref="ArgumentException">A profile is not CIE-based or has the wrong number of components.</exception>
    public PdfDeviceProfiles(PdfColorSpaceConverter? gray, PdfColorSpaceConverter? rgb, PdfColorSpaceConverter? cmyk)
    {
        ValidateProfile(gray, 1, nameof(gray));
        ValidateProfile(rgb, 3, nameof(rgb));
        ValidateProfile(cmyk, 4, nameof(cmyk));

        Gray = gray ?? PdfStandardGrayColorSpaceConverter.Instance;
        Rgb = rgb ?? PdfStandardRgbColorSpaceConverter.Instance;
        Cmyk = cmyk ?? BuiltInCmyk;
    }

    /// <summary>
    /// Gets the built-in device profiles: sRGB gray, sRGB and the bundled CMYK profile.
    /// </summary>
    public static PdfDeviceProfiles Default { get; } = new(null, null, null);

    /// <summary>
    /// Gets the color space DeviceGray values are in.
    /// </summary>
    public PdfColorSpaceConverter Gray { get; }

    /// <summary>
    /// Gets the color space DeviceRGB values are in.
    /// </summary>
    public PdfColorSpaceConverter Rgb { get; }

    /// <summary>
    /// Gets the color space DeviceCMYK values are in.
    /// </summary>
    public PdfColorSpaceConverter Cmyk { get; }

    /// <summary>
    /// Returns the device profiles of the output intents in effect for <paramref name="page"/>: the first intent of
    /// each component count with a profile, the built-in profile for counts no intent covers.
    /// </summary>
    public static PdfDeviceProfiles FromPage(IPdfPage page)
    {
        if (page == null)
        {
            throw new ArgumentNullException(nameof(page));
        }

        return FromOutputIntents(page.OutputIntents);
    }

    /// <summary>
    /// Returns the device profiles of the document's output intents, as <see cref="FromPage"/> does, ignoring the
    /// output intents of its pages.
    /// </summary>
    public static PdfDeviceProfiles FromDocument(IPdfDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        return FromOutputIntents(document.OutputIntents);
    }

    /// <summary>
    /// Returns the transform taking device values of <paramref name="componentCount"/> components to the profile
    /// connection space.
    /// </summary>
    public ChainedColorTransform GetProfileConnectionTransform(int componentCount, PdfRenderingIntent renderingIntent, bool useBlackPointCompensation)
    {
        return GetProfile(this, componentCount).GetProfileConnectionTransform(renderingIntent, useBlackPointCompensation)
            ?? GetProfile(Default, componentCount).GetProfileConnectionTransform(renderingIntent, useBlackPointCompensation)
            ?? throw new InvalidOperationException($"The built-in device profile of {componentCount} components has no conversion to the profile connection space.");
    }

    /// <summary>
    /// Returns the transform taking the profile connection space to device values of <paramref name="componentCount"/>
    /// components.
    /// </summary>
    public ChainedColorTransform GetFromProfileConnectionTransform(int componentCount, PdfRenderingIntent renderingIntent, bool useBlackPointCompensation)
    {
        return GetProfile(this, componentCount).GetFromProfileConnectionTransform(renderingIntent, useBlackPointCompensation)
            ?? GetProfile(Default, componentCount).GetFromProfileConnectionTransform(renderingIntent, useBlackPointCompensation)
            ?? throw new InvalidOperationException($"The built-in device profile of {componentCount} components has no conversion from the profile connection space.");
    }

    private static PdfDeviceProfiles FromOutputIntents(IReadOnlyList<PdfOutputIntent>? outputIntents)
    {
        PdfColorSpaceConverter? gray = null;
        PdfColorSpaceConverter? rgb = null;
        PdfColorSpaceConverter? cmyk = null;

        if (outputIntents != null)
        {
            foreach (PdfOutputIntent outputIntent in outputIntents)
            {
                PdfIccColorSpaceConverter? profileColorSpace = outputIntent.DestinationOutputColorSpace;

                if (profileColorSpace == null)
                {
                    continue;
                }

                if (profileColorSpace.Components == 1 && gray == null)
                {
                    gray = profileColorSpace;
                }
                else if (profileColorSpace.Components == 3 && rgb == null)
                {
                    rgb = profileColorSpace;
                }
                else if (profileColorSpace.Components == 4 && cmyk == null)
                {
                    cmyk = profileColorSpace;
                }
            }
        }

        return new PdfDeviceProfiles(gray, rgb, cmyk);
    }

    private static PdfColorSpaceConverter GetProfile(PdfDeviceProfiles profiles, int componentCount)
    {
        switch (componentCount)
        {
            case 1:
            {
                return profiles.Gray;
            }
            case 4:
            {
                return profiles.Cmyk;
            }
            default:
            {
                return profiles.Rgb;
            }
        }
    }

    private static void ValidateProfile(PdfColorSpaceConverter? profile, int componentCount, string parameterName)
    {
        if (profile == null)
        {
            return;
        }

        bool isCieBased = profile is PdfCalibratedColorSpaceConverter
            || profile is PdfLabColorSpaceConverter
            || profile is PdfIccColorSpaceConverter;

        if (!isCieBased)
        {
            throw new ArgumentException("A device profile must be a CalGray, CalRGB, Lab or ICCBased color space.", parameterName);
        }

        if (profile.Components != componentCount)
        {
            throw new ArgumentException($"A device profile for this color space must have {componentCount} components.", parameterName);
        }
    }
}
