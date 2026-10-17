using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// Color spaces that device color values are interpreted in: the default color spaces of the resources
/// a device color space was selected with, or the document's output intent. A device color space without
/// a mapped color space is converted as is.
/// </summary>
internal sealed class PdfDeviceColorMapping : IEquatable<PdfDeviceColorMapping>
{
    /// <summary>
    /// Mapping that converts every device color space as is.
    /// </summary>
    public static readonly PdfDeviceColorMapping None = new(null, null, null);

    public PdfDeviceColorMapping(PdfColorSpaceConverter? gray, PdfColorSpaceConverter? rgb, PdfColorSpaceConverter? cmyk)
    {
        Gray = gray;
        Rgb = rgb;
        Cmyk = cmyk;
    }

    /// <summary>
    /// Color space DeviceGray values are interpreted in, or null when they are converted as is.
    /// </summary>
    public PdfColorSpaceConverter? Gray { get; }

    /// <summary>
    /// Color space DeviceRGB values are interpreted in, or null when they are converted as is.
    /// </summary>
    public PdfColorSpaceConverter? Rgb { get; }

    /// <summary>
    /// Color space DeviceCMYK values are interpreted in, or null when they are converted as is.
    /// </summary>
    public PdfColorSpaceConverter? Cmyk { get; }

    /// <summary>
    /// Returns the color space values of <paramref name="converter"/> are interpreted in: the mapped color space
    /// for a device color space that has one, otherwise <paramref name="converter"/> itself.
    /// </summary>
    public PdfColorSpaceConverter Map(PdfColorSpaceConverter converter)
    {
        PdfColorSpaceConverter? mapped = converter switch
        {
            PdfDeviceGrayColorSpaceConverter => Gray,
            PdfDeviceRgbColorSpaceConverter => Rgb,
            PdfDeviceCmykColorSpaceConverter => Cmyk,
            _ => null
        };

        return mapped ?? converter;
    }

    /// <inheritdoc/>
    public bool Equals(PdfDeviceColorMapping? other)
    {
        return other != null
            && ReferenceEquals(Gray, other.Gray)
            && ReferenceEquals(Rgb, other.Rgb)
            && ReferenceEquals(Cmyk, other.Cmyk);
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as PdfDeviceColorMapping);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(Gray), RuntimeHelpers.GetHashCode(Rgb), RuntimeHelpers.GetHashCode(Cmyk));
}
