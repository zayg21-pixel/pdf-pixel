using PdfPixel.Models;
using PdfPixel.PdfPanel.Annotations;
using System;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Describes which pages to decode and the rendering parameters to use.
/// Passed to <see cref="IPdfPageContentProvider.UpdateContent"/>.
/// </summary>
public class UpdateContentRequest
{
    /// <summary>
    /// 1-based page numbers that should be kept in cache. Pages not listed are cancelled and cleared.
    /// </summary>
    public int[] VisiblePages { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Rendering parameters to apply when decoding pages.
    /// </summary>
    public PdfRenderingParameters RenderingParameters { get; set; } = new();

    /// <summary>
    /// Current pointer state, used to determine annotation visual states during decoding.
    /// </summary>
    public PdfPanelPointerState PointerState { get; set; }

    /// <summary>
    /// The annotation currently under the pointer, or <see langword="null"/> if none.
    /// </summary>
    public PdfAnnotationPopup? ActiveAnnotation { get; set; }
}
