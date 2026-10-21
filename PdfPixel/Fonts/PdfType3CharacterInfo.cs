using PdfPixel.Geometry;
using System;

namespace PdfPixel.Fonts;

/// <summary>
/// Holds the character procedure and metrics of a Type 3 character. The metrics come from the d0/d1
/// operator of its CharProc stream; the glyph itself is recorded for each use, in the graphics state it
/// inherits from the text-showing operator.
/// </summary>
public sealed class PdfType3CharacterInfo
{
    /// <summary>
    /// Gets a singleton instance representing an undefined character (no character procedure/metrics available).
    /// </summary>
    public static PdfType3CharacterInfo Undefined { get; } = new(ReadOnlyMemory<byte>.Empty, 0, null, PdfSize.Empty);

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfType3CharacterInfo"/> class.
    /// </summary>
    /// <param name="content">Decoded CharProc content stream; empty when not defined.</param>
    /// <param name="objectNumber">Object number of the CharProc stream, guarding against recursive glyphs.</param>
    /// <param name="boundingBox">Bounding box parsed by d1 (llx, lly, urx, ury). Null when not provided.</param>
    /// <param name="advancement">Glyph advancement vector parsed by d0/d1 (wx, wy). If not provided, use <see cref="PdfSize.Empty"/>.</param>
    internal PdfType3CharacterInfo(in ReadOnlyMemory<byte> content, uint objectNumber, PdfRectangle? boundingBox, in PdfSize advancement)
    {
        Content = content;
        ObjectNumber = objectNumber;
        BoundingBox = boundingBox;
        Advancement = advancement;
    }

    /// <summary>
    /// True when the character procedure was found and has content.
    /// </summary>
    public bool IsDefined => !Content.IsEmpty;

    /// <summary>
    /// Gets the character bounding box from d1 if provided; otherwise null.
    /// </summary>
    public PdfRectangle? BoundingBox { get; }

    /// <summary>
    /// Gets the glyph advancement vector (wx, wy) from d0/d1. Defaults to <see cref="PdfSize.Empty"/> when not provided.
    /// </summary>
    public PdfSize Advancement { get; }

    /// <summary>
    /// Gets a value indicating whether the character is colored. Defined as BoundingBox being absent.
    /// </summary>
    public bool IsColored => !BoundingBox.HasValue;

    /// <summary>
    /// Gets the decoded CharProc content stream.
    /// </summary>
    internal ReadOnlyMemory<byte> Content { get; }

    /// <summary>
    /// Gets the object number of the CharProc stream.
    /// </summary>
    internal uint ObjectNumber { get; }
}
