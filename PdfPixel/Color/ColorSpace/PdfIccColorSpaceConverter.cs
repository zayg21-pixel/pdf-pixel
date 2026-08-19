using PdfPixel.Color.Icc;
using PdfPixel.Color.Icc.Model;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using System;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// The ICCBased color space: color values are components whose meaning is given by an embedded
/// ICC profile.
/// </summary>
public sealed class PdfIccColorSpaceConverter : PdfColorSpaceConverter
{
    private readonly bool _useDefault;
    private readonly PdfColorSpaceConverter _default;
    private readonly IccProfileTransform? _iccTransform;
    private readonly bool _isStandardRgbOrGray;

    /// <summary>
    /// Initializes the space from its component count, alternate color space, and parsed ICC profile.
    /// </summary>
    public PdfIccColorSpaceConverter(int n, PdfColorSpaceConverter? alternate, IccProfile? profile)
    {
        Profile = profile;
        N = n;

        _isStandardRgbOrGray = profile != null && (IccProfileAnalyzer.IsStandardSrgb(profile) || IccProfileAnalyzer.IsStandardGray(profile));
        if (profile == null || profile.ChannelsCount != n || _isStandardRgbOrGray)
        {
            _useDefault = true;
        }
        else
        {
            _iccTransform = new IccProfileTransform(profile);
        }

        _default = alternate
            ?? n switch
            {
                1 => PdfDeviceGrayColorSpaceConverter.Instance,
                3 => PdfDeviceRgbColorSpaceConverter.Instance,
                4 => PdfDeviceCmykColorSpaceConverter.Instance,
                _ => PdfDeviceRgbColorSpaceConverter.Instance
            };
    }

    /// <summary>
    /// Initializes the space from its component count, alternate color space, and raw ICC profile bytes.
    /// </summary>
    public PdfIccColorSpaceConverter(int n, PdfColorSpaceConverter? alternate, byte[]? iccProfileBytes)
        : this(n, alternate, GetProfile(iccProfileBytes))
    {
    }

    /// <summary>
    /// Gets the embedded ICC profile, or null when the space carries none or it could not be parsed.
    /// </summary>
    public IccProfile? Profile { get; }

    /// <inheritdoc />
    public override int Components => _default.Components;

    /// <inheritdoc />
    public override bool IsDevice => _isStandardRgbOrGray;

    /// <summary>
    /// Gets the number of components the space declares.
    /// </summary>
    public int N { get; }

    /// <inheritdoc />
    public override IColorTransform? NormalizeTransform => _default.NormalizeTransform;

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
    protected override ColorTransformSampler GetRgbaSamplerCore(PdfRenderingIntent intent, TransferFunctionTransform? postTransform, bool normalize)
    {
        if (_useDefault || _iccTransform == null)
        {
            return _default.GetRgbaSampler(intent, postTransform, normalize);
        }

        IColorTransform iccPipeline = _iccTransform.GetIntentTransform(intent.ToIccRenderingIntent());
        IColorTransform? normalizeTransform = normalize ? _default.NormalizeTransform : null;
        ChainedColorTransform chained = new(normalizeTransform, iccPipeline, postTransform);
        return new ColorTransformSampler(chained);
    }
}
