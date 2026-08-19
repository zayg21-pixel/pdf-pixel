using PdfPixel.Geometry;
using PdfPixel.Models;
using System;

namespace PdfPixel.Commands.Model;

/// <summary>
/// Represents a rectangular clip operation.
/// </summary>
public sealed class ClipRectangleCommand : PdfCommand
{
    /// <summary>
    /// Initializes the command with the given rectangle and clip operation.
    /// </summary>
    public ClipRectangleCommand(in PdfRectangle rect, PdfClipOperation operation)
        : this(rect, operation, PdfClipRectangleSource.Rectangle)
    {
    }

    /// <summary>
    /// Initializes the command with the given clip operation and rectangle source.
    /// </summary>
    /// <param name="rect">Rectangle to clip to, required by <see cref="PdfClipRectangleSource.Rectangle"/>.</param>
    /// <param name="operation">Clip operation to apply.</param>
    /// <param name="source">Where the command takes its rectangle from.</param>
    public ClipRectangleCommand(PdfRectangle? rect, PdfClipOperation operation, PdfClipRectangleSource source)
    {
        if (source == PdfClipRectangleSource.Rectangle && rect == null)
        {
            throw new ArgumentNullException(nameof(rect), $"{PdfClipRectangleSource.Rectangle} requires a rectangle.");
        }

        Rect = rect;
        Operation = operation;
        Source = source;
    }

    /// <summary>
    /// Gets the rectangle this command clips to, set when <see cref="Source"/> is
    /// <see cref="PdfClipRectangleSource.Rectangle"/>.
    /// </summary>
    public PdfRectangle? Rect { get; }

    /// <summary>
    /// Gets the clip operation this command applies.
    /// </summary>
    public PdfClipOperation Operation { get; }

    /// <summary>
    /// Gets where this command takes its rectangle from.
    /// </summary>
    public PdfClipRectangleSource Source { get; }

    /// <inheritdoc />
    public override PdfCommandFeatures Features
        => (Source == PdfClipRectangleSource.Region) ? PdfCommandFeatures.Region : PdfCommandFeatures.None;

    /// <inheritdoc />
    public override PdfCommandKind Kind => PdfCommandKind.ClipRectangle;
}
