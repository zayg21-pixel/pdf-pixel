using PdfPixel.Geometry;
using System;

namespace PdfPixel.PdfPanel.Input;

/// <summary>
/// A position mapped to a page's coordinate space.
/// </summary>
public readonly struct PdfPanelPagePoint : IEquatable<PdfPanelPagePoint>
{
    /// <summary>
    /// Initializes a new <see cref="PdfPanelPagePoint"/> from a page number and a position on that page.
    /// </summary>
    public PdfPanelPagePoint(int pageNumber, in PdfPoint position)
    {
        PageNumber = pageNumber;
        Position = position;
    }

    /// <summary>
    /// 1-based page number.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Position in page coordinates (top-left origin, Y-down, unrotated).
    /// </summary>
    public PdfPoint Position { get; }

    /// <inheritdoc/>
    public bool Equals(PdfPanelPagePoint other) => PageNumber == other.PageNumber && Position == other.Position;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfPanelPagePoint other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(PageNumber, Position);

    /// <summary>
    /// Determines whether two page points have the same page number and position.
    /// </summary>
    public static bool operator ==(in PdfPanelPagePoint left, in PdfPanelPagePoint right) => left.Equals(right);

    /// <summary>
    /// Determines whether two page points have a different page number or position.
    /// </summary>
    public static bool operator !=(in PdfPanelPagePoint left, in PdfPanelPagePoint right) => !left.Equals(right);
}
