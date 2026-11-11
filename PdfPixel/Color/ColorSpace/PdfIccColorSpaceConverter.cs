using PdfPixel.Color.Icc;
using PdfPixel.Color.Transform;
using PdfPixel.Objects;
using System;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The ICCBased color space: color values are components whose meaning is given by an embedded
/// ICC profile.
/// </summary>
public sealed class PdfIccColorSpaceConverter : PdfColorSpaceConverter
{
    private readonly bool _useDefault;
    private readonly IccProfileTransform? _iccTransform;
    private readonly bool _isStandardRgbOrGray;

    internal PdfIccColorSpaceConverter(int n, PdfColorSpaceConverter? alternate, IccProfile? profile)
    {
        Profile = profile;

        _isStandardRgbOrGray = profile != null && (IccProfileAnalyzer.IsStandardSrgb(profile) || IccProfileAnalyzer.IsStandardGray(profile));
        if (profile == null || profile.ChannelsCount != n || _isStandardRgbOrGray)
        {
            _useDefault = true;
        }
        else
        {
            _iccTransform = new IccProfileTransform(profile);
        }

        Alternate = alternate
            ?? n switch
            {
                1 => PdfDeviceGrayColorSpaceConverter.Instance,
                3 => PdfDeviceRgbColorSpaceConverter.Instance,
                4 => PdfDeviceCmykColorSpaceConverter.Instance,
                _ => PdfDeviceRgbColorSpaceConverter.Instance
            };
    }

    internal PdfIccColorSpaceConverter(int n, PdfColorSpaceConverter? alternate, byte[]? iccProfileBytes)
        : this(n, alternate, GetProfile(iccProfileBytes))
    {
    }

    /// <summary>
    /// Initializes an ICCBased color space with the components of <paramref name="profile"/>.
    /// </summary>
    /// <exception cref="ArgumentException">The profile has neither 1, 3 nor 4 channels.</exception>
    public PdfIccColorSpaceConverter(IccProfile profile)
        : this(GetChannelCount(profile), null, profile)
    {
    }

    /// <summary>
    /// Gets the embedded ICC profile, or null when the space carries none or it could not be parsed.
    /// </summary>
    public IccProfile? Profile { get; }

    /// <inheritdoc />
    public override int Components => Alternate.Components;

    /// <inheritdoc />
    public override bool IsDevice => _isStandardRgbOrGray;

    /// <summary>
    /// Gets the alternate color space (/Alternate), or the device space with the same number of components when absent.
    /// </summary>
    public PdfColorSpaceConverter Alternate { get; }

    /// <inheritdoc />
    public override IColorTransform? NormalizeTransform => Alternate.NormalizeTransform;

    /// <inheritdoc />
    public override IColorTransform? DenormalizeTransform => Alternate.DenormalizeTransform;

    /// <inheritdoc />
    public override PdfRange[]? ComponentRanges => Alternate.ComponentRanges;

    private static int GetChannelCount(IccProfile profile)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        int channelCount = profile.ChannelsCount;

        if (channelCount != 1 && channelCount != 3 && channelCount != 4)
        {
            throw new ArgumentException("An ICCBased color space has 1, 3 or 4 components.", nameof(profile));
        }

        return channelCount;
    }

    private static IccProfile? GetProfile(byte[]? bytes)
    {
        if (bytes == null)
        {
            return null;
        }

#pragma warning disable CA1031
        try
        {
            return IccProfile.Parse(bytes);
        }
        catch (Exception)
        {
#if DEBUG
            throw;
#else
            return null;
#endif
        }
#pragma warning restore RCS1075
    }

    /// <inheritdoc />
    public override ChainedColorTransform? GetProfileConnectionTransform(PdfRenderingIntent renderingIntent, bool useBlackPointCompensation)
    {
        if (_useDefault || _iccTransform == null)
        {
            return Alternate.GetProfileConnectionTransform(renderingIntent, useBlackPointCompensation);
        }
        else
        {
            ChainedColorTransform iccPipeline = _iccTransform.GetIntentToXyzTransform(renderingIntent.ToIccRenderingIntent(), useBlackPointCompensation);
            return new ChainedColorTransform(Alternate.NormalizeTransform, iccPipeline);
        }
    }

    /// <inheritdoc />
    public override ChainedColorTransform? GetFromProfileConnectionTransform(PdfRenderingIntent renderingIntent, bool useBlackPointCompensation)
    {
        if (_useDefault || _iccTransform == null)
        {
            return Alternate.GetFromProfileConnectionTransform(renderingIntent, useBlackPointCompensation);
        }
        else
        {
            ChainedColorTransform? iccPipeline = _iccTransform.GetXyzToDeviceTransform(renderingIntent.ToIccRenderingIntent(), useBlackPointCompensation);

            if (iccPipeline != null)
            {
                return new ChainedColorTransform(iccPipeline, Alternate.DenormalizeTransform);
            }
            else
            {
                return Alternate.GetFromProfileConnectionTransform(renderingIntent, useBlackPointCompensation);
            }
        }
    }
}
