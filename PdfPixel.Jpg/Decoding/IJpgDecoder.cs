using System;

namespace PdfPixel.Jpg.Decoding;

/// <summary>
/// Common interface for JPEG row-based decoders (baseline and progressive).
/// </summary>
public interface IJpgDecoder
{
    /// <summary>
    /// Read the next full image row of interleaved component samples into <paramref name="rowBuffer"/>.
    /// Returns false when no more rows are available.
    /// </summary>
    /// <param name="rowBuffer">Destination buffer. Length must be at least (Width * ComponentCount) as determined by the owning decoder logic.</param>
    /// <returns>True if a row was written; false on end of image.</returns>
    bool TryReadRow(Span<byte> rowBuffer);

    /// <summary>
    /// Zero-based index of the next row to be produced.
    /// </summary>
    int CurrentRow { get; }
}
