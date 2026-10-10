using PdfPixel.Models;

namespace PdfPixel.Navigation.Model;

/// <summary>
/// Structure destination whose target is a structure element ID, as in the structure destination of a
/// remote go-to action.
/// </summary>
public sealed class PdfStructureIdDestination : PdfExplicitDestination
{
    /// <summary>
    /// Initializes a structure destination targeting a structure element ID.
    /// </summary>
    /// <param name="elementId">ID (/ID) of the target structure element.</param>
    /// <param name="fitType">How the target page is displayed.</param>
    /// <param name="zoom">Zoom factor for the XYZ fit type; null retains the current zoom.</param>
    /// <param name="left">Left coordinate, or null when not given.</param>
    /// <param name="bottom">Bottom coordinate, or null when not given.</param>
    /// <param name="right">Right coordinate, or null when not given.</param>
    /// <param name="top">Top coordinate, or null when not given.</param>
    public PdfStructureIdDestination(in PdfString elementId,PdfDestinationFitType fitType, float? zoom, float? left, float? bottom, float? right, float? top)
        : base(fitType, zoom, left, bottom, right, top)
    {
        ElementId = elementId;
    }

    internal PdfStructureIdDestination(PdfArray destinationArray, in PdfString elementId)
        : base(destinationArray)
    {
        ElementId = elementId;
    }

    /// <summary>
    /// ID (/ID) of the target structure element.
    /// </summary>
    public PdfString ElementId { get; }

    /// <summary>
    /// Returns a string representation of this destination.
    /// </summary>
    public override string ToString() => $"Destination: structure element {ElementId}, {FitType}";
}
