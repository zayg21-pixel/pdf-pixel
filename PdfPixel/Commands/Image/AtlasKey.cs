using SkiaSharp;
using System;

namespace PdfPixel.Commands.Image;

/// <summary>
/// Groups atlas images by their pixel format — color type and alpha type together.
/// </summary>
internal readonly struct AtlasKey : IEquatable<AtlasKey>
{
    public AtlasKey(SKColorType colorType, SKAlphaType alphaType)
    {
        ColorType = colorType;
        AlphaType = alphaType;
    }

    public SKColorType ColorType { get; }
    public SKAlphaType AlphaType { get; }

    public bool Equals(AtlasKey other) => ColorType == other.ColorType && AlphaType == other.AlphaType;
    public override bool Equals(object? obj) => obj is AtlasKey other && Equals(other);
    public override int GetHashCode() => ((int)ColorType * 397) ^ (int)AlphaType;
}
