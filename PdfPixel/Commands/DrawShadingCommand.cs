using PdfPixel.Color.Paint;
using PdfPixel.Shading;
using System;

namespace PdfPixel.Commands;

/// <summary>
/// Draws the rendering primitives built for a PDF shading, with the paint in effect where the
/// shading is used.
/// </summary>
public sealed class DrawShadingCommand : PdfCommand
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DrawShadingCommand"/> class.
    /// </summary>
    /// <param name="content">Rendering primitives built for the shading.</param>
    /// <param name="paint">Paint captured where the shading is used, in the color space the colors of <paramref name="content"/> are given in.</param>
    public DrawShadingCommand(PdfShadingContent content, PdfPaint paint)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        Paint = paint ?? throw new ArgumentNullException(nameof(paint));
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
    public override PdfCommandKind Kind => PdfCommandKind.DrawShading;

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawShadingCommand)} {Content.ShadingType}";
}
