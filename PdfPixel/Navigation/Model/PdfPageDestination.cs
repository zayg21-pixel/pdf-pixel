using PdfPixel.Models;

namespace PdfPixel.Navigation.Model;

/// <summary>
/// Explicit destination whose target is a page object of the document it belongs to.
/// </summary>
public sealed class PdfPageDestination : PdfExplicitDestination
{
    /// <summary>
    /// Initializes an explicit destination targeting a page object.
    /// </summary>
    /// <param name="pageReference">Reference of the target page object.</param>
    /// <param name="fitType">How the target page is displayed.</param>
    /// <param name="zoom">Zoom factor for the XYZ fit type; null retains the current zoom.</param>
    /// <param name="left">Left coordinate, or null when not given.</param>
    /// <param name="bottom">Bottom coordinate, or null when not given.</param>
    /// <param name="right">Right coordinate, or null when not given.</param>
    /// <param name="top">Top coordinate, or null when not given.</param>
    public PdfPageDestination(in PdfReference pageReference,PdfDestinationFitType fitType, float? zoom, float? left, float? bottom, float? right, float? top)
        : base(fitType, zoom, left, bottom, right, top)
    {
        PageReference = pageReference;
    }

    internal PdfPageDestination(PdfArray destinationArray, in PdfReference pageReference)
        : base(destinationArray)
    {
        PageReference = pageReference;
    }

    /// <summary>
    /// Reference of the target page object.
    /// </summary>
    public PdfReference PageReference { get; }

    /// <summary>
    /// Returns a string representation of this destination.
    /// </summary>
    public override string ToString() => $"Destination: page {PageReference}, {FitType}";
}
