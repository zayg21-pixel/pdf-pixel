using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using System;

namespace PdfPixel.Commands;

/// <summary>
/// Paints a transparency group as one element with no geometry of its own.
/// </summary>
public sealed class DrawGroupCommand : PdfCommand, IPaintCommand
{
    /// <summary>
    /// Initializes the command with the group and the paint it is composited with.
    /// </summary>
    /// <param name="source">The group.</param>
    /// <param name="paint">The paint the group is composited with.</param>
    public DrawGroupCommand(PdfGroupSource source, PdfPaint paint)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Paint = paint ?? throw new ArgumentNullException(nameof(paint));
    }

    /// <summary>
    /// Gets the group.
    /// </summary>
    public PdfGroupSource Source { get; }

    /// <inheritdoc />
    public PdfPaint Paint { get; }

    /// <inheritdoc />
    public PdfRectangle? Bounds => Source.Bounds;

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.DrawGroup;

    /// <inheritdoc />
    public override string ToString() => $"{nameof(DrawGroupCommand)} {Source} {PdfCommandFormatting.FormatPaint(Paint)}";
}
