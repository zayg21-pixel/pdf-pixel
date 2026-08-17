using PdfPixel.Shading.Model;
using System;

namespace PdfPixel.Commands;

/// <summary>
/// Draws the rendering primitives built for a PDF shading, at the fill alpha in effect where the
/// shading is used.
/// </summary>
public sealed class DrawShadingCommand : PdfCommand
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DrawShadingCommand"/> class.
    /// </summary>
    /// <param name="content">Rendering primitives built for the shading.</param>
    /// <param name="fillAlpha">Fill alpha captured where the shading is used.</param>
    public DrawShadingCommand(PdfShadingContent content, float fillAlpha)
    {
        // TODO: [HIGH] let it be here, ch14, last page, extremely slow rendering
        Content = content ?? throw new ArgumentNullException(nameof(content));
        FillAlpha = fillAlpha;
    }

    /// <summary>
    /// Rendering primitives this command draws.
    /// </summary>
    public PdfShadingContent Content { get; }

    /// <summary>
    /// Shading fill alpha.
    /// </summary>
    public float FillAlpha { get; }

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.DrawShading;

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawShadingCommand)} {Content.ShadingType}";
}
