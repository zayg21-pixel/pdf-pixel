using PdfPixel.Color.Paint;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Parsed annotation border style (BS dictionary, or legacy Border array).
/// </summary>
public sealed class PdfAnnotationBorderStyle
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfAnnotationBorderStyle"/> class.
    /// </summary>
    public PdfAnnotationBorderStyle(PdfAnnotationBorderStyleType styleType, PdfStrokeStyle strokeStyle)
    {
        StyleType = styleType;
        StrokeStyle = strokeStyle;
    }

    /// <summary>
    /// Gets the border style type.
    /// </summary>
    public PdfAnnotationBorderStyleType StyleType { get; }

    /// <summary>
    /// Gets the stroke style (width, dash pattern) used to render this border.
    /// </summary>
    public PdfStrokeStyle StrokeStyle { get; }
}
