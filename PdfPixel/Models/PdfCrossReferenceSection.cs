using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// One cross-reference section of a file: its indexed entries and its trailer.
/// </summary>
internal sealed class PdfCrossReferenceSection
{
    public PdfCrossReferenceSection(Dictionary<PdfReference, PdfObjectInfo> entries, PdfTrailer? trailer)
    {
        Entries = entries;
        Trailer = trailer;
    }

    /// <summary>
    /// Entries of the section; the first entry of a reference wins.
    /// </summary>
    public Dictionary<PdfReference, PdfObjectInfo> Entries { get; }

    /// <summary>
    /// Trailer of the section, or null when none could be read.
    /// </summary>
    public PdfTrailer? Trailer { get; }
}
