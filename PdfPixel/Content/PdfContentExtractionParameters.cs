using PdfPixel.Commands;
using System.Collections.Generic;

namespace PdfPixel.Content;

/// <summary>
/// Parameters controlling which content <see cref="PdfContentExtractor"/> extracts from a content tree.
/// </summary>
public sealed class PdfContentExtractionParameters
{
    /// <summary>
    /// Kinds of content to extract, or <see langword="null"/> to extract every kind.
    /// </summary>
    public IReadOnlyList<PdfContentKind>? ContentKinds { get; set; }

    /// <summary>
    /// When true, content of Type 3 glyph descriptions is extracted as well.
    /// </summary>
    public bool IncludeType3Content { get; set; }

    /// <summary>
    /// When true, content of soft mask transparency groups is extracted as well.
    /// </summary>
    public bool IncludeSoftMaskContent { get; set; }
}
