namespace PdfPixel.Annotations.Models;

/// <summary>
/// Parsed annotation border effect (BE dictionary): the effect type and its intensity.
/// </summary>
public readonly struct PdfAnnotationBorderEffect
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfAnnotationBorderEffect"/> struct.
    /// </summary>
    public PdfAnnotationBorderEffect(PdfAnnotationBorderEffectType type, float intensity)
    {
        Type = type;
        Intensity = intensity;
    }

    /// <summary>
    /// The border effect type. Default <see cref="PdfAnnotationBorderEffectType.Solid"/> (no effect).
    /// </summary>
    public PdfAnnotationBorderEffectType Type { get; }

    /// <summary>
    /// Effect intensity in the range 0–2 (BE dictionary I entry). Only meaningful when
    /// <see cref="Type"/> is <see cref="PdfAnnotationBorderEffectType.Cloudy"/>.
    /// </summary>
    public float Intensity { get; }
}
