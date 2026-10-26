using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Web.Dto;

/// <summary>
/// Document information, the dates in ISO 8601.
/// </summary>
internal sealed class DocumentInformationDto
{
    public string? Title { get; set; }

    public string? Author { get; set; }

    public string? Subject { get; set; }

    public string? Keywords { get; set; }

    public string? Creator { get; set; }

    public string? Producer { get; set; }

    public string? CreationDate { get; set; }

    public string? ModificationDate { get; set; }

    public bool? IsTrapped { get; set; }

    public required Dictionary<string, string> CustomEntries { get; set; }
}
