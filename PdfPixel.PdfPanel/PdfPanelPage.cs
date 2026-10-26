using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Caching;
using System;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Represents a page in the PDF document in <see cref="PdfPanelDocument"/>.
/// </summary>
public class PdfPanelPage
{
    private int _rotation;

    internal PdfPanelPage(IPdfPage pdfPage, int pageNumber, PdfPanelDocument panelDocument)
    {
        PdfPage = pdfPage ?? throw new ArgumentNullException(nameof(pdfPage));
        PageNumber = pageNumber;
        PanelDocument = panelDocument ?? throw new ArgumentNullException(nameof(panelDocument));
        Label = pdfPage.PageLabel.DecodePdfString();
        CacheEntry = new PdfPageCacheEntry(pageNumber);
        _rotation = (pdfPage.Rotation % 90 == 0) ? pdfPage.Rotation : 0;
    }

    /// <summary>
    /// Number of the page.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Human-readable page label (e.g. "iv", "1", "A-1") as defined in the PDF.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Size of the page without rotation, in panel units.
    /// </summary>
    public PdfSize Size => new(PdfPage.CropBox.Width * PdfPage.UserUnit, PdfPage.CropBox.Height * PdfPage.UserUnit);

    /// <summary>
    /// Clockwise rotation of the page in degrees, normalized to 0-359. Starts at the rotation defined in the PDF.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when the value is not a multiple of 90.</exception>
    public int Rotation
    {
        get => _rotation;

        set
        {
            if (value % 90 != 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Rotation must be a multiple of 90.");
            }

            _rotation = ((value % 360) + 360) % 360;
        }
    }

    /// <summary>
    /// Collection of annotation popups for this page.
    /// </summary>
    public PdfAnnotationPopup[] Popups => CacheEntry.GetAnnotations(this);

    /// <summary>
    /// Gets the offset of the page.
    /// Offset is expressed in the scaled panel space (affected by the current zoom factor) and
    /// represents the distance in device pixels from the top-left corner of the content area to
    /// the top-left corner of this page.
    /// </summary>
    public PdfPoint Offset { get; set; }

    /// <summary>
    /// Page of the PDF document this page displays. Property reads are thread-safe;
    /// method calls and stream access must lock <see cref="PdfPanelDocument.DocumentLocker"/>.
    /// </summary>
    public IPdfPage PdfPage { get; }

    /// <summary>
    /// Document this page belongs to.
    /// </summary>
    internal PdfPanelDocument PanelDocument { get; }

    /// <summary>
    /// Content produced for this page.
    /// </summary>
    internal PdfPageCacheEntry CacheEntry { get; }
}
