namespace PdfPixel.Shading.Decoding;

/// <summary>
/// Holds the minimum value and pre-multiplied scale for a color component.
/// </summary>
internal readonly struct ColorMinAndScale
{
    public readonly float Min;
    public readonly float Scale;

    public ColorMinAndScale(float min, float scale)
    {
        Min = min;
        Scale = scale;
    }
}
