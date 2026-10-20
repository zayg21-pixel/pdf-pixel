using PdfPixel.Geometry;
using PdfPixel.Geometry.PathOperations;

namespace PdfPixel.Annotations.Model;

/// <summary>
/// Parsed annotation border effect (BE dictionary): the effect type and its intensity.
/// </summary>
public readonly struct PdfAnnotationBorderEffect
{
    private const float CloudRadiusPerIntensity = 3.5f;
    private const float CloudRadiusPerLineWidth = 0.5f;
    private const float CloudSpacingPerRadius = 1.8f;
    private const float CloudOverlapDegrees = 30f;

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

    /// <summary>
    /// Distance the effect extends beyond the outline it is applied to, for a border of <paramref name="lineWidth"/>.
    /// </summary>
    internal float GetOutset(float lineWidth)
    {
        switch (Type)
        {
            case PdfAnnotationBorderEffectType.Cloudy when Intensity > 0f:
                return (Intensity * CloudRadiusPerIntensity) + (lineWidth * CloudRadiusPerLineWidth);

            default:
                return 0f;
        }
    }

    /// <summary>
    /// Returns <paramref name="path"/> scalloped for a cloudy effect with a positive <see cref="Intensity"/>,
    /// otherwise <paramref name="path"/> itself, for a border of <paramref name="lineWidth"/>.
    /// </summary>
    internal PdfPath Apply(PdfPath path, float lineWidth)
    {
        switch (Type)
        {
            case PdfAnnotationBorderEffectType.Cloudy when Intensity > 0f:
            {
                float radius = GetOutset(lineWidth);
                return PdfPathEffects.ApplyCloudEffect(path, radius, radius * CloudSpacingPerRadius, CloudOverlapDegrees);
            }
            default:
                return path;
        }
    }
}
