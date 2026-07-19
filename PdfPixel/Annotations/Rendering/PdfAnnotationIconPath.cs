using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Geometry;

namespace PdfPixel.Annotations.Rendering;

/// <summary>
/// A single path element within an icon definition.
/// </summary>
internal sealed class PdfAnnotationIconPath
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfAnnotationIconPath"/> class.
    /// </summary>
    public PdfAnnotationIconPath(
        PdfPath path,
        PdfAnnotationIconColorType fillColorType,
        in PdfColor fillColor,
        PdfAnnotationIconColorType strokeColorType,
        in PdfColor strokeColor,
        float strokeWidth,
        PdfStrokeCap strokeCap,
        PdfStrokeJoin strokeJoin,
        float fillOpacity,
        float strokeOpacity)
    {
        Path = path;
        FillColorType = fillColorType;
        FillColor = fillColor;
        StrokeColorType = strokeColorType;
        StrokeColor = strokeColor;
        StrokeWidth = strokeWidth;
        StrokeCap = strokeCap;
        StrokeJoin = strokeJoin;
        FillOpacity = fillOpacity;
        StrokeOpacity = strokeOpacity;
    }

    /// <summary>
    /// Gets the parsed SVG path geometry.
    /// </summary>
    public PdfPath Path { get; }

    /// <summary>
    /// Gets how the fill color is sourced for this path.
    /// <see cref="PdfAnnotationIconColorType.None"/> means no fill pass is applied.
    /// </summary>
    public PdfAnnotationIconColorType FillColorType { get; }

    /// <summary>
    /// Gets the fill color used when <see cref="FillColorType"/> is <see cref="PdfAnnotationIconColorType.Override"/>.
    /// </summary>
    public PdfColor FillColor { get; }

    /// <summary>
    /// Gets how the stroke color is sourced for this path.
    /// <see cref="PdfAnnotationIconColorType.None"/> means no stroke pass is applied.
    /// </summary>
    public PdfAnnotationIconColorType StrokeColorType { get; }

    /// <summary>
    /// Gets the stroke color used when <see cref="StrokeColorType"/> is <see cref="PdfAnnotationIconColorType.Override"/>.
    /// </summary>
    public PdfColor StrokeColor { get; }

    /// <summary>
    /// Gets the stroke width in icon coordinate space.
    /// </summary>
    public float StrokeWidth { get; }

    /// <summary>
    /// Gets the stroke cap style applied at path endpoints.
    /// </summary>
    public PdfStrokeCap StrokeCap { get; }

    /// <summary>
    /// Gets the stroke join style applied at path corners.
    /// </summary>
    public PdfStrokeJoin StrokeJoin { get; }

    /// <summary>
    /// Gets the opacity applied to the fill, in the range [0, 1].
    /// </summary>
    public float FillOpacity { get; }

    /// <summary>
    /// Gets the opacity applied to the stroke, in the range [0, 1].
    /// </summary>
    public float StrokeOpacity { get; }
}
