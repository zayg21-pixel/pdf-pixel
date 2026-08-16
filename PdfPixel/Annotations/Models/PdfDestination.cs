using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;
using System;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// A particular view of a document: the page a link leads to, how that page is displayed, and where on
/// it the view lands. Parsed off the destination array, which it does not hold on to.
/// </summary>
public sealed class PdfDestination
{
    private readonly IPdfDocumentInternal _document;
    private readonly PdfReference _pageReference;
    private readonly int? _declaredPageIndex;
    private readonly float? _left;
    private readonly float? _bottom;
    private readonly float? _right;
    private readonly float? _top;

    internal PdfDestination(PdfArray destinationArray)
    {
        _document = destinationArray.Document;

        PdfReference? pageReference = destinationArray.GetReference(0);

        if (pageReference?.IsValid == true)
        {
            _pageReference = pageReference.Value;
        }
        else
        {
            _declaredPageIndex = destinationArray.GetInteger(0);
        }

        if (destinationArray.Count > 1)
        {
            FitType = destinationArray.GetNameOrDefault(1).AsEnum<PdfDestinationFitType>();
        }

        switch (FitType)
        {
            case PdfDestinationFitType.XYZ:
                {
                    if (destinationArray.Count >= 5)
                    {
                        _left = destinationArray.GetFloat(2);
                        _top = destinationArray.GetFloat(3);
                        Zoom = destinationArray.GetFloat(4);

                        if (Zoom == 0)
                        {
                            Zoom = null;
                        }
                    }

                    break;
                }
            case PdfDestinationFitType.FitH:
            case PdfDestinationFitType.FitBH:
                {
                    if (destinationArray.Count >= 3)
                    {
                        _top = destinationArray.GetFloat(2);
                    }

                    break;
                }
            case PdfDestinationFitType.FitV:
            case PdfDestinationFitType.FitBV:
                {
                    if (destinationArray.Count >= 3)
                    {
                        _left = destinationArray.GetFloat(2);
                    }

                    break;
                }
            case PdfDestinationFitType.FitR:
                {
                    if (destinationArray.Count >= 6)
                    {
                        _left = destinationArray.GetFloat(2);
                        _bottom = destinationArray.GetFloat(3);
                        _right = destinationArray.GetFloat(4);
                        _top = destinationArray.GetFloat(5);
                    }

                    break;
                }
        }
    }

    /// <summary>
    /// How the target page is displayed.
    /// </summary>
    public PdfDestinationFitType FitType { get; }

    /// <summary>
    /// Zoom factor for the XYZ fit type. Null retains the current zoom.
    /// </summary>
    public float? Zoom { get; }

    /// <summary>
    /// Gets the target page, or null when the page is not one of this document's.
    /// </summary>
    public IPdfPage? GetPdfPage()
    {
        if (_pageReference.IsValid)
        {
            return _document.Destinations.GetPage(_pageReference);
        }

        if (_declaredPageIndex >= 0 && _declaredPageIndex < _document.Pages.Count)
        {
            return _document.Pages[_declaredPageIndex.Value];
        }

        return null;
    }

    /// <summary>
    /// Target location on the page in PDF coordinates: a zero-size point for the scrolling fit types,
    /// the rectangle to fit for FitR, null for Fit and FitB.
    /// </summary>
    public PdfRectangle? TargetLocation
    {
        get
        {
            IPdfPage? page = GetPdfPage();

            if (page == null)
            {
                return null;
            }

            switch (FitType)
            {
                case PdfDestinationFitType.XYZ:
                    {
                        float targetLeft = _left ?? 0f;
                        float targetTop = _top ?? page.CropBox.Height;
                        return new PdfRectangle(targetLeft, targetTop, targetLeft, targetTop);
                    }
                case PdfDestinationFitType.FitH:
                case PdfDestinationFitType.FitBH:
                    {
                        float targetTop = _top ?? page.CropBox.Height;
                        return new PdfRectangle(0, targetTop, 0, targetTop);
                    }
                case PdfDestinationFitType.FitV:
                case PdfDestinationFitType.FitBV:
                    {
                        float targetLeft = _left ?? 0f;
                        return new PdfRectangle(targetLeft, 0, targetLeft, 0);
                    }
                case PdfDestinationFitType.FitR:
                    {
                        if (_left.HasValue && _bottom.HasValue && _right.HasValue && _top.HasValue)
                        {
                            return new PdfRectangle(
                                Math.Min(_left.Value, _right.Value),
                                Math.Min(_bottom.Value, _top.Value),
                                Math.Max(_left.Value, _right.Value),
                                Math.Max(_bottom.Value, _top.Value));
                        }

                        return null;
                    }
                case PdfDestinationFitType.Fit:
                case PdfDestinationFitType.FitB:
                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// Returns a string representation of this destination.
    /// </summary>
    public override string ToString() => $"Destination: {FitType}";
}
