using PdfPixel.Models;

namespace PdfPixel.Navigation.Model;

/// <summary>
/// Structure destination (/SD, PDF 2.0) whose target is a structure element of the document it belongs to;
/// the page is the one holding the element's first content.
/// </summary>
public sealed class PdfStructureDestination : PdfExplicitDestination
{
    /// <summary>
    /// Initializes a structure destination targeting a structure element.
    /// </summary>
    /// <param name="elementReference">Reference of the target structure element.</param>
    /// <param name="fitType">How the target page is displayed.</param>
    /// <param name="zoom">Zoom factor for the XYZ fit type; null retains the current zoom.</param>
    /// <param name="left">Left coordinate, or null when not given.</param>
    /// <param name="bottom">Bottom coordinate, or null when not given.</param>
    /// <param name="right">Right coordinate, or null when not given.</param>
    /// <param name="top">Top coordinate, or null when not given.</param>
    public PdfStructureDestination(in PdfReference elementReference,PdfDestinationFitType fitType, float? zoom, float? left, float? bottom, float? right, float? top)
        : base(fitType, zoom, left, bottom, right, top)
    {
        ElementReference = elementReference;
    }

    internal PdfStructureDestination(PdfArray destinationArray, in PdfReference elementReference)
        : base(destinationArray)
    {
        ElementReference = elementReference;
    }

    /// <summary>
    /// Reference of the target structure element.
    /// </summary>
    public PdfReference ElementReference { get; }

    /// <summary>
    /// Returns a string representation of this destination.
    /// </summary>
    public override string ToString() => $"Destination: structure element {ElementReference}, {FitType}";
}
