using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Web.Dto.Model;

/// <summary>
/// Outline item, <c>{ handle, title, isBold, isItalic, color, isExpanded, children }</c>.
/// </summary>
internal sealed class OutlineItemDto
{
    public int Handle { get; set; }

    public string? Title { get; set; }

    public bool IsBold { get; set; }

    public bool IsItalic { get; set; }

    public string? Color { get; set; }

    public bool IsExpanded { get; set; }

    public required IReadOnlyList<OutlineItemDto> Children { get; set; }
}
