using System;

namespace PdfPixel.Tiff.Model;

/// <summary>
/// Kind of data an image file directory holds (NewSubfileType, tag 254).
/// </summary>
[Flags]
public enum TiffSubfileType
{
    /// <summary>
    /// A full-resolution image.
    /// </summary>
    None = 0,

    /// <summary>
    /// A reduced-resolution version of another image in the file.
    /// </summary>
    ReducedResolution = 1 << 0,

    /// <summary>
    /// A single page of a multi-page image.
    /// </summary>
    Page = 1 << 1,

    /// <summary>
    /// A transparency mask for another image in the file.
    /// </summary>
    TransparencyMask = 1 << 2
}
