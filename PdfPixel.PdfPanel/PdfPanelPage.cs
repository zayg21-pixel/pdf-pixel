using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.ContentProvider;
using System;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Represents a page in the PDF document in <see cref="PdfPanelPageCollection"/>.
/// </summary>
public class PdfPanelPage
{
    private readonly IPdfPageContentProvider _contentProvider;

    internal PdfPanelPage(in PdfPanelPageInfo info, int pageNumber, IPdfPageContentProvider contentProvider)
    {
        Info = info;
        PageNumber = pageNumber;
        _contentProvider = contentProvider ?? throw new ArgumentNullException(nameof(contentProvider));
    }

    /// <summary>
    /// Information about the page, it's size and rotation.
    /// </summary>
    public PdfPanelPageInfo Info { get; }

    /// <summary>
    /// Number of the page.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// Collection of annotation popups for this page.
    /// </summary>
    public PdfAnnotationPopup[] Popups => _contentProvider.GetAnnotationPopups(PageNumber);

    /// <summary>
    /// Gets the offset of the page.
    /// Offset is expressed in the scaled canvas space (affected by the current zoom factor) and
    /// represents the distance in device pixels from the top-left corner of the content area to
    /// the top-left corner of this page.
    /// </summary>
    public PdfPoint Offset { get; set; }

    /// <summary>
    /// User defined rotation of the page in degrees.
    /// Can be any value that is a multiple of 90.
    /// </summary>
    public int UserRotation { get; set; }
}
