namespace PdfPixel.Models;

/// <summary>
/// File structure keywords (ISO 32000-2, 7.3.8, 7.3.10, 7.5.4, 7.5.5), valued by their token bytes packed little-endian.
/// </summary>
public enum PdfKeyword : ulong
{
    /// <summary>
    /// Token longer than eight bytes.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// <c>obj</c>: begin indirect object.
    /// </summary>
    Obj = 'o' | ('b' << 8) | ('j' << 16),

    /// <summary>
    /// <c>stream</c>: begin stream data.
    /// </summary>
    Stream = 's' | ('t' << 8) | ('r' << 16) | ((ulong)'e' << 24) | ((ulong)'a' << 32) | ((ulong)'m' << 40),

    /// <summary>
    /// <c>trailer</c>: begin file trailer.
    /// </summary>
    Trailer = 't' | ('r' << 8) | ('a' << 16) | ((ulong)'i' << 24) | ((ulong)'l' << 32) | ((ulong)'e' << 40) | ((ulong)'r' << 48),

    /// <summary>
    /// <c>n</c>: in-use cross-reference entry.
    /// </summary>
    InUse = 'n',

    /// <summary>
    /// <c>f</c>: free cross-reference entry.
    /// </summary>
    Free = 'f'
}
