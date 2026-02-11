using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
using System;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a parsed PDF destination.
/// </summary>
/// <remarks>
/// A destination defines a particular view of a document, consisting of:
/// - A page reference or page index
/// - A fit type (how to display the page)
/// - Optional parameters for the fit type
/// </remarks>
public class PdfDestination
{
    private readonly PdfDocument _document;
    private readonly PdfReference _pageReference;
    private readonly int _pageIndex;
    private readonly float? _left;
    private readonly float? _bottom;
    private readonly float? _right;
    private readonly float? _top;
    private PdfPage _cachedPage;

    private PdfDestination(
        PdfDocument document,
        PdfReference pageReference, 
        int pageIndex, 
        PdfDestinationFitType fitType,
        float? left,
        float? bottom,
        float? right,
        float? top,
        float? zoom)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _pageReference = pageReference;
        _pageIndex = pageIndex;
        FitType = fitType;
        _left = left;
        _bottom = bottom;
        _right = right;
        _top = top;
        Zoom = zoom;
    }

    /// <summary>
    /// Gets the destination page resolved from the owning <see cref="PdfDocument"/>.
    /// </summary>
    /// <remarks>
    /// A PDF destination can refer to a page either by an indirect reference or by a numeric page index.
    /// This method resolves the page once and caches it for subsequent calls.
    /// </remarks>
    /// <returns>The resolved <see cref="PdfPage"/>, or null if it cannot be resolved.</returns>
    /// <returns>The resolved <see cref="PdfPage"/>, or null if it cannot be resolved when the page is not found.</returns>
    public PdfPage GetPdfPage()
    {
        if (_cachedPage != null)
        {
            return _cachedPage;
        }

        if (_pageIndex >= 0)
        {
            if (_pageIndex >= _document.Pages.Count)
            {
                return null;
            }

            _cachedPage = _document.Pages[_pageIndex];
            return _cachedPage;
        }

        if (!_pageReference.IsValid)
        {
            return null;
        }

        for (int pageIndex = 0; pageIndex < _document.Pages.Count; pageIndex++)
        {
            var page = _document.Pages[pageIndex];
            if (page.PageObject.Reference.Equals(_pageReference))
            {
                _cachedPage = page;
                break;
            }
        }

        if (_cachedPage == null)
        {
            return null;
        }

        return _cachedPage;
    }

    /// <summary>
    /// Gets the fit type that determines how the page should be displayed.
    /// </summary>
    public PdfDestinationFitType FitType { get; }

    /// <summary>
    /// Gets the zoom factor for XYZ fit type (null = retain current zoom).
    /// </summary>
    public float? Zoom { get; }

    /// <summary>
    /// Gets the target location rectangle in PDF coordinates for this destination.
    /// Returns null if the destination does not specify a location (e.g., Fit, FitB types) or if the page cannot be resolved.
    /// The rectangle dimensions depend on the fit type:
    /// - XYZ, FitH, FitV, FitBH, FitBV: returns a rectangle with specified coordinates and zero size
    /// - FitR: returns the actual rectangle to fit
    /// - Fit, FitB: returns null
    /// </summary>
    /// <returns>A rectangle in PDF coordinates, or null if no location is specified.</returns>
    public SKRect? GetTargetLocation()
    {
        PdfPage page = GetPdfPage();
        if (page == null)
        {
            return null;
        }

        switch (FitType)
        {
            case PdfDestinationFitType.XYZ:
                if (_left.HasValue || _top.HasValue)
                {
                    float left = _left ?? 0f;
                    float top = _top ?? page.CropBox.Height;
                    return SKRect.Create(left, top, 0, 0);
                }
                return null;

            case PdfDestinationFitType.FitH:
            case PdfDestinationFitType.FitBH:
                if (_top.HasValue)
                {
                    return SKRect.Create(0, _top.Value, 0, 0);
                }
                return null;

            case PdfDestinationFitType.FitV:
            case PdfDestinationFitType.FitBV:
                if (_left.HasValue)
                {
                    return SKRect.Create(_left.Value, 0, 0, 0);
                }
                return null;

            case PdfDestinationFitType.FitR:
                if (_left.HasValue && _bottom.HasValue && _right.HasValue && _top.HasValue)
                {
                    return new SKRect(_left.Value, _bottom.Value, _right.Value, _top.Value).Standardized;
                }
                return null;

            case PdfDestinationFitType.Fit:
            case PdfDestinationFitType.FitB:
            default:
                return null;
        }
    }

    /// <summary>
    /// Parses a destination from a PDF value.
    /// </summary>
    /// <param name="destination">The destination value (can be name, string, or array).</param>
    /// <param name="document">The PDF document for resolving named destinations.</param>
    /// <returns>A parsed destination, or null if the destination is invalid.</returns>
    public static PdfDestination Parse(IPdfValue destination, PdfDocument document)
    {
        if (destination == null)
        {
            return null;
        }

        if (destination is PdfArray destArray)
        {
            return ParseExplicitDestination(destArray, document);
        }

        if (document != null && destination.Type == PdfValueType.String)
        {
            var destName = destination.AsString();
            if (document.NamedDestinations != null)
            {
                var resolvedDest = document.NamedDestinations.GetArray(destName);
                if (resolvedDest != null)
                {
                    return ParseExplicitDestination(resolvedDest, document);
                }
            }
        }

        return null;
    }

    private static PdfDestination ParseExplicitDestination(PdfArray destArray, PdfDocument document)
    {
        if (destArray == null || destArray.Count == 0)
        {
            return null;
        }

        var pageValue = destArray.GetObject(0);
        PdfReference pageReference;
        int pageIndex = -1;

        if (pageValue != null && pageValue.Reference.IsValid)
        {
            pageReference = pageValue.Reference;
        }
        else
        {
            pageIndex = destArray.GetIntegerOrDefault(0);
            pageReference = default;
        }

        PdfDestinationFitType fitType = PdfDestinationFitType.Unknown;
        if (destArray.Count > 1)
        {
            var fitName = destArray.GetName(1);
            fitType = fitName.AsEnum<PdfDestinationFitType>();
        }

        float? left = null;
        float? bottom = null;
        float? right = null;
        float? top = null;
        float? zoom = null;

        switch (fitType)
        {
            case PdfDestinationFitType.XYZ:
                if (destArray.Count >= 5)
                {
                    left = destArray.GetFloat(2);
                    top = destArray.GetFloat(3);
                    zoom = destArray.GetFloat(4);
                    if (zoom.HasValue && zoom.Value == 0)
                    {
                        zoom = null;
                    }
                }
                break;

            case PdfDestinationFitType.FitH:
            case PdfDestinationFitType.FitBH:
                if (destArray.Count >= 3)
                {
                    top = destArray.GetFloat(2);
                }
                break;

            case PdfDestinationFitType.FitV:
            case PdfDestinationFitType.FitBV:
                if (destArray.Count >= 3)
                {
                    left = destArray.GetFloat(2);
                }
                break;

            case PdfDestinationFitType.FitR:
                if (destArray.Count >= 6)
                {
                    left = destArray.GetFloat(2);
                    bottom = destArray.GetFloat(3);
                    right = destArray.GetFloat(4);
                    top = destArray.GetFloat(5);
                }
                break;
        }

        return new PdfDestination(document, pageReference, pageIndex, fitType, left, bottom, right, top, zoom);
    }

    /// <summary>
    /// Returns a string representation of this destination.
    /// </summary>
    public override string ToString()
    {
        if (_pageIndex >= 0)
        {
            return $"Destination: Page {_pageIndex + 1}, {FitType}";
        }

        return $"Destination: Page Reference, {FitType}";
    }
}

/// <summary>
/// Defines how a destination page should be displayed.
/// </summary>
[PdfEnum]
public enum PdfDestinationFitType
{
    /// <summary>
    /// Unknown or unsupported fit type.
    /// </summary>
    [PdfEnumDefaultValue]
    Unknown = 0,

    /// <summary>
    /// Display the page with explicit coordinates and zoom.
    /// </summary>
    [PdfEnumValue("XYZ")]
    XYZ,

    /// <summary>
    /// Fit the page in the window.
    /// </summary>
    [PdfEnumValue("Fit")]
    Fit,

    /// <summary>
    /// Fit the page width in the window.
    /// </summary>
    [PdfEnumValue("FitH")]
    FitH,

    /// <summary>
    /// Fit the page height in the window.
    /// </summary>
    [PdfEnumValue("FitV")]
    FitV,

    /// <summary>
    /// Fit a rectangle in the window.
    /// </summary>
    [PdfEnumValue("FitR")]
    FitR,

    /// <summary>
    /// Fit the page's bounding box in the window.
    /// </summary>
    [PdfEnumValue("FitB")]
    FitB,

    /// <summary>
    /// Fit the page's bounding box width in the window.
    /// </summary>
    [PdfEnumValue("FitBH")]
    FitBH,

    /// <summary>
    /// Fit the page's bounding box height in the window.
    /// </summary>
    [PdfEnumValue("FitBV")]
    FitBV
}
