using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Web.Dto.Model;

/// <summary>
/// View state a redraw returns to JS; the popup, active content, search results, layers and action requests
/// are set only when their changed flag is set.
/// </summary>
internal sealed class RedrawResultDto
{
    public string? CursorStyle { get; set; }

    public bool AnnotationPopupChanged { get; set; }

    public AnnotationPopupDto? AnnotationPopup { get; set; }

    public bool ActiveContentNameChanged { get; set; }

    public string? ActiveContentName { get; set; }

    public bool IsTextExtracted { get; set; }

    public bool SearchResultsChanged { get; set; }

    public IReadOnlyList<SearchResultDto>? SearchResults { get; set; }

    public bool LayersChanged { get; set; }

    public int? LayerConfiguration { get; set; }

    public IReadOnlyList<int>? VisibleLayers { get; set; }

    public IReadOnlyList<ActionRequestDto>? ActionRequests { get; set; }

    public float ScrollWidth { get; set; }

    public float ScrollHeight { get; set; }

    public float VerticalOffset { get; set; }

    public float HorizontalOffset { get; set; }

    public float Scale { get; set; }

    public required string AutoScaleMode { get; set; }

    public int CurrentPage { get; set; }
}
