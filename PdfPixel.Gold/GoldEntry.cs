using System.Text.Json.Serialization;

namespace PdfPixel.Gold;

/// <summary>
/// One PDF registered in the corpus: which of its pages take part in a run, and whether it is
/// slow enough to be left out of a short run.
/// </summary>
internal sealed class GoldEntry
{
    /// <summary>
    /// File name of the PDF inside the source folder, extension included.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Page numbers to render, 1-based. An empty array means every page of the document.
    /// </summary>
    public List<int> Pages { get; set; } = new();

    /// <summary>
    /// True when only a full run processes this PDF. Absent when it takes part in both the short
    /// and the full run.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? FullRun { get; set; }

    /// <summary>
    /// User password of an encrypted PDF. Absent when the document is not encrypted.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Password { get; set; }
}
