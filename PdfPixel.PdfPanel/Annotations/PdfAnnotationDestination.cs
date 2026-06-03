using PdfPixel.Annotations.Models;
using SkiaSharp;

namespace PdfPixel.PdfPanel.Annotations;

/// <summary>
/// Serialization-friendly destination extracted from a PDF navigation target.
/// Holds the resolved page number, fit type, optional scroll location, and optional zoom.
/// </summary>
public class PdfAnnotationDestination
{
    /// <summary>
    /// 1-based page number of the navigation target.
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// How the destination page should be fit into the viewport.
    /// Determines the zoom calculation applied during navigation.
    /// </summary>
    public PdfDestinationFitType FitType { get; set; }

    /// <summary>
    /// Target scroll location in PDF coordinates, pre-computed at annotation extraction time.
    /// Null for <see cref="PdfDestinationFitType.Fit"/> and <see cref="PdfDestinationFitType.FitB"/>
    /// (those scroll to the page top via page number).
    /// For <see cref="PdfDestinationFitType.FitR"/> contains the full rectangle to fit.
    /// </summary>
    public SKRect? TargetLocation { get; set; }

    /// <summary>
    /// Explicit zoom for <see cref="PdfDestinationFitType.XYZ"/>. Null retains the current zoom.
    /// Not used for fit types whose zoom is computed from viewport and page dimensions at navigation time.
    /// </summary>
    public float? Zoom { get; set; }
}
