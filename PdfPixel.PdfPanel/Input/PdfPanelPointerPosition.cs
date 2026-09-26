using PdfPixel.Geometry;
using System;

namespace PdfPixel.PdfPanel.Input;

/// <summary>
/// A pointer position in panel coordinates together with the page it falls on.
/// </summary>
public readonly struct PdfPanelPointerPosition : IEquatable<PdfPanelPointerPosition>
{
    /// <summary>
    /// Initializes a new <see cref="PdfPanelPointerPosition"/> from a panel position and the page it falls on.
    /// </summary>
    public PdfPanelPointerPosition(in PdfPoint panelPosition, PdfPanelPagePoint? pagePoint)
    {
        PanelPosition = panelPosition;
        PagePoint = pagePoint;
    }

    /// <summary>
    /// Position in panel coordinates.
    /// </summary>
    public PdfPoint PanelPosition { get; }

    /// <summary>
    /// Page the position falls on, or <see langword="null"/> when it falls on no page.
    /// </summary>
    public PdfPanelPagePoint? PagePoint { get; }

    /// <inheritdoc/>
    public bool Equals(PdfPanelPointerPosition other)
        => PanelPosition == other.PanelPosition && PagePoint == other.PagePoint;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfPanelPointerPosition other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(PanelPosition, PagePoint);

    /// <summary>
    /// Determines whether two pointer positions have the same panel position and page.
    /// </summary>
    public static bool operator ==(in PdfPanelPointerPosition left, in PdfPanelPointerPosition right) => left.Equals(right);

    /// <summary>
    /// Determines whether two pointer positions have a different panel position or page.
    /// </summary>
    public static bool operator !=(in PdfPanelPointerPosition left, in PdfPanelPointerPosition right) => !left.Equals(right);
}
