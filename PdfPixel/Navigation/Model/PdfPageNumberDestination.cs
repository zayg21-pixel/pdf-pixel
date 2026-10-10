using PdfPixel.Models;

namespace PdfPixel.Navigation.Model;

/// <summary>
/// Explicit destination whose target is a page number, as in the destination of a remote go-to action.
/// </summary>
public sealed class PdfPageNumberDestination : PdfExplicitDestination
{
    /// <summary>
    /// Initializes an explicit destination targeting a page number.
    /// </summary>
    /// <param name="pageNumber">Number of the target page, the first page being 0.</param>
    /// <param name="fitType">How the target page is displayed.</param>
    /// <param name="zoom">Zoom factor for the XYZ fit type; null retains the current zoom.</param>
    /// <param name="left">Left coordinate, or null when not given.</param>
    /// <param name="bottom">Bottom coordinate, or null when not given.</param>
    /// <param name="right">Right coordinate, or null when not given.</param>
    /// <param name="top">Top coordinate, or null when not given.</param>
    public PdfPageNumberDestination(int pageNumber, PdfDestinationFitType fitType, float? zoom, float? left, float? bottom, float? right, float? top)
        : base(fitType, zoom, left, bottom, right, top)
    {
        PageNumber = pageNumber;
    }

    internal PdfPageNumberDestination(PdfArray destinationArray, int pageNumber)
        : base(destinationArray)
    {
        PageNumber = pageNumber;
    }

    /// <summary>
    /// Number of the target page, the first page being 0.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Returns a string representation of this destination.
    /// </summary>
    public override string ToString() => $"Destination: page number {PageNumber}, {FitType}";
}
