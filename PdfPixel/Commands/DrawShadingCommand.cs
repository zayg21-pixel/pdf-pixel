using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Shading;
using System;

namespace PdfPixel.Commands;

/// <summary>
/// Draws the rendering primitives built for a PDF shading, with the paint in effect where the
/// shading is used.
/// </summary>
public sealed class DrawShadingCommand : PdfCommand, IPaintCommand
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DrawShadingCommand"/> class.
    /// </summary>
    /// <param name="content">Rendering primitives built for the shading.</param>
    /// <param name="paint">Paint captured where the shading is used, in the color space the colors of <paramref name="content"/> are given in.</param>
    /// <param name="boundingBox">The bounding box of the shading, or <see langword="null"/> when it declares none.</param>
    public DrawShadingCommand(PdfShadingContent content, PdfPaint paint, PdfRectangle? boundingBox)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        Paint = paint ?? throw new ArgumentNullException(nameof(paint));
        Bounds = PaintBounds.Intersect(content.GetBounds(), boundingBox);
    }

    /// <summary>
    /// Rendering primitives this command draws.
    /// </summary>
    public PdfShadingContent Content { get; }

    /// <summary>
    /// Paint the shading is drawn with.
    /// </summary>
    public PdfPaint Paint { get; }

    /// <inheritdoc />
    public PdfRectangle? Bounds { get; }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.DrawShading;

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawShadingCommand)} {Content.ShadingType}";
}
