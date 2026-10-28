using PdfPixel.Color.Paint;

namespace PdfPixel.Annotations;

/// <summary>
/// Parsed annotation border style (BS dictionary, or legacy Border array).
/// </summary>
public sealed class PdfAnnotationBorderStyle
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfAnnotationBorderStyle"/> class.
    /// </summary>
    public PdfAnnotationBorderStyle(
        PdfAnnotationBorderStyleType styleType,
        PdfStrokeStyle strokeStyle,
        float horizontalCornerRadius,
        float verticalCornerRadius)
    {
        StyleType = styleType;
        StrokeStyle = strokeStyle;
        HorizontalCornerRadius = horizontalCornerRadius;
        VerticalCornerRadius = verticalCornerRadius;
    }

    /// <summary>
    /// Gets the border style type.
    /// </summary>
    public PdfAnnotationBorderStyleType StyleType { get; }

    /// <summary>
    /// Gets the stroke style (width, dash pattern) used to render this border.
    /// </summary>
    public PdfStrokeStyle StrokeStyle { get; }

    /// <summary>
    /// Gets the horizontal radius of the border's rounded corners, 0 for square corners.
    /// </summary>
    public float HorizontalCornerRadius { get; }

    /// <summary>
    /// Gets the vertical radius of the border's rounded corners, 0 for square corners.
    /// </summary>
    public float VerticalCornerRadius { get; }
}
