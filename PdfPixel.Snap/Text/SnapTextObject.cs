using PdfPixel.Geometry;
using PdfPixel.Snap.Transparency;
using System;

namespace PdfPixel.Snap.Text;

/// <summary>
/// An open text object, with the state it began in, its knockout group under text knockout (ISO 32000-2 9.3.8) and the
/// clip its glyphs add (9.3.6); it owns the knockout group and the block of its result.
/// </summary>
internal sealed class SnapTextObject : IDisposable
{
    public SnapTextObject(SnapLayer parent, in PdfIntegerRectangle limits, int saveCount, bool knockout)
    {
        Parent = parent;
        Limits = limits;
        SaveCount = saveCount;
        Knockout = knockout;
    }

    public SnapLayer Parent { get; }

    /// <summary>
    /// Gets the draw limits when the text object began.
    /// </summary>
    public PdfIntegerRectangle Limits { get; }

    /// <summary>
    /// Gets the number of saves when the text object began.
    /// </summary>
    public int SaveCount { get; }

    /// <summary>
    /// Gets whether the text object's glyphs knock each other out.
    /// </summary>
    public bool Knockout { get; }

    /// <summary>
    /// Gets or sets the knockout group, <see langword="null"/> until one is open.
    /// </summary>
    public SnapLayer? Group { get; set; }

    /// <summary>
    /// Gets or sets the block the knockout group's result is in, rented from the scratch buffer,
    /// <see langword="null"/> until the group is open.
    /// </summary>
    public SnapMemoryBlock? GroupBlock { get; set; }

    /// <summary>
    /// Gets or sets the outlines, in device space, the glyphs add to the clip when the text object ends,
    /// <see langword="null"/> until a glyph adds any.
    /// </summary>
    public PdfPathBuilder? ClipOutlines { get; set; }

    /// <inheritdoc />
    public void Dispose()
    {
        Group?.Dispose();
        GroupBlock?.Dispose();
    }
}
