namespace PdfPixel.Color.Paint;

/// <summary>
/// What a mask paint (<see cref="PdfPaint.MaskParameters"/>) masks content with. The content beneath keeps only as much of
/// itself as what the paint composites holds: a layer the paint is used for, or a stencil image drawn with it over the whole
/// area it masks.
/// </summary>
public enum PdfMaskPaintKind
{
    /// <summary>
    /// A soft mask (/SMask of the graphics state): the layer holds the rendered mask group.
    /// </summary>
    SoftMask,

    /// <summary>
    /// The shape of a path, text or stencil image that a pattern fills or strokes: the layer holds the shape drawn opaque,
    /// so the pattern painted beneath shows only within it.
    /// </summary>
    Shape
}
