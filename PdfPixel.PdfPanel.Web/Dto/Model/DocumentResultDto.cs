using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Web.Dto.Model;

/// <summary>
/// Result of a loaded document: <c>ok: true</c>, its properties, pages, outline, layer configurations and attached files.
/// </summary>
internal sealed class DocumentResultDto
{
    public bool Ok => true;

    public int PageCount { get; set; }

    public string? Version { get; set; }

    public string? PageMode { get; set; }

    public string? PageLayout { get; set; }

    public string? Lang { get; set; }

    public DocumentInformationDto? Information { get; set; }

    public PermissionsDto? Permissions { get; set; }

    public required IReadOnlyList<PageDto> Pages { get; set; }

    public required IReadOnlyList<OutlineItemDto> Outlines { get; set; }

    public required IReadOnlyList<LayerConfigurationDto> LayerConfigurations { get; set; }

    public required IReadOnlyList<AttachmentDto> Attachments { get; set; }
}
