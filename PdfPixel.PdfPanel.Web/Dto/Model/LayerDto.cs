using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Web.Dto.Model;

/// <summary>
/// Layer tree node, <c>{ handle, name, isLayer, isLocked, children }</c>.
/// </summary>
internal sealed class LayerDto
{
    public int Handle { get; set; }

    public string? Name { get; set; }

    public bool IsLayer { get; set; }

    public bool IsLocked { get; set; }

    public required IReadOnlyList<LayerDto> Children { get; set; }
}
