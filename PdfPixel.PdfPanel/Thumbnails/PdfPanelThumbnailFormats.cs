using System;

namespace PdfPixel.PdfPanel.Thumbnails;

/// <summary>
/// Forms a page thumbnail is kept in.
/// </summary>
[Flags]
public enum PdfPanelThumbnailFormats
{
    /// <summary>
    /// No form.
    /// </summary>
    None = 0,

    /// <summary>
    /// A PNG file.
    /// </summary>
    Png = 1 << 0,

    /// <summary>
    /// Opaque 8-bit RGB samples.
    /// </summary>
    Samples = 1 << 1
}
