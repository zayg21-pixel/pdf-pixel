using PdfPixel.Geometry;
using System;

namespace PdfPixel.PdfPanel.Input;

/// <summary>
/// A pointer position in viewport coordinates together with the page it falls on.
/// </summary>
public readonly struct PdfPanelPointerPosition : IEquatable<PdfPanelPointerPosition>
{
    /// <summary>
    /// Initializes a new <see cref="PdfPanelPointerPosition"/> from a viewport position and the page it falls on.
    /// </summary>
    public PdfPanelPointerPosition(in PdfPoint viewportPosition, PdfPanelPagePoint? pagePoint)
    {
        ViewportPosition = viewportPosition;
        PagePoint = pagePoint;
    }

    /// <summary>
    /// Position in viewport coordinates.
    /// </summary>
    public PdfPoint ViewportPosition { get; }

    /// <summary>
    /// Page the position falls on, or <see langword="null"/> when it falls on no page.
    /// </summary>
    public PdfPanelPagePoint? PagePoint { get; }

    /// <inheritdoc/>
    public bool Equals(PdfPanelPointerPosition other)
        => ViewportPosition == other.ViewportPosition && PagePoint == other.PagePoint;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfPanelPointerPosition other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(ViewportPosition, PagePoint);

    /// <summary>
    /// Determines whether two pointer positions have the same viewport position and page.
    /// </summary>
    public static bool operator ==(in PdfPanelPointerPosition left, in PdfPanelPointerPosition right) => left.Equals(right);

    /// <summary>
    /// Determines whether two pointer positions have a different viewport position or page.
    /// </summary>
    public static bool operator !=(in PdfPanelPointerPosition left, in PdfPanelPointerPosition right) => !left.Equals(right);
}
