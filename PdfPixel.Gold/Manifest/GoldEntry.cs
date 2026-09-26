namespace PdfPixel.Gold.Manifest;

/// <summary>
/// One PDF registered in the corpus: where it is downloaded from, the exact bytes expected, which of
/// its pages take part in a run, and whether it is slow enough to be left out of a short run.
/// </summary>
internal sealed class GoldEntry
{
    /// <summary>
    /// File name of the PDF inside the source folder, extension included.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Address the PDF is downloaded from.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 of the PDF's bytes, lowercase hexadecimal.
    /// </summary>
    public string Sha256 { get; set; } = string.Empty;

    /// <summary>
    /// Where the PDF comes from, such as the issue it was reported in. Absent when unknown.
    /// </summary>
    public string? Origin { get; set; }

    /// <summary>
    /// Page numbers to render, 1-based. An empty array means every page of the document.
    /// </summary>
    public List<int> Pages { get; set; } = new();

    /// <summary>
    /// True when only a full run processes this PDF. Absent when it takes part in both the short
    /// and the full run.
    /// </summary>
    public bool? FullRun { get; set; }

    /// <summary>
    /// User password of an encrypted PDF. Absent when the document is not encrypted.
    /// </summary>
    public string? Password { get; set; }
}
