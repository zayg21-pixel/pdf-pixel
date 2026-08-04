using System;
using System.IO;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Reads glyph existence, widths, unicode mapping, outlines, and metrics directly from a font
/// program. Implemented once per font kind (Type1, CFF, CFF2, SFNT), each wrapping that kind's own
/// parsed data model.
/// </summary>
public interface IPdfTypeface : IDisposable
{
    /// <summary>
    /// Gets the font metrics and style information for this typeface.
    /// </summary>
    PdfFontMetrics Metrics { get; }

    /// <summary>
    /// Gets a readable stream over the final font bytes for this typeface. Each call returns an
    /// independent stream over the same underlying data - the caller disposes it, and doing so never
    /// affects any other stream returned from this method.
    /// </summary>
    Stream GetFontStream();

    /// <summary>
    /// Determines whether the given glyph id exists in this typeface.
    /// </summary>
    bool IsGidExists(ushort gid);

    /// <summary>
    /// Gets the advance width of the given glyph id, in em-relative units (already divided by
    /// <see cref="PdfFontMetrics.UnitsPerEm"/>; 1.0 = one em).
    /// </summary>
    /// <returns>The advance width, or <see langword="null"/> if <paramref name="gid"/> does not exist in this typeface.</returns>
    float? GetWidth(ushort gid);

    /// <summary>
    /// Gets the outline of the given glyph id, in em-relative units (1.0 = one em), encoded in the
    /// format <c>PdfPixel.Geometry.PdfPath</c>'s constructor accepts directly (see <see cref="PdfFontPathBuilder"/>).
    /// </summary>
    ReadOnlyMemory<byte> GetPath(ushort gid);

    /// <summary>
    /// Gets the glyph id this typeface's own cmap maps the given Unicode string to, or null if none
    /// is known. Meaningful only for a fallback/substitution typeface (always TrueType in practice,
    /// so the only kind with a cmap available for this) used to find a glyph for a character an
    /// embedded font doesn't cover itself.
    /// </summary>
    ushort? GetGid(string unicode);
}
