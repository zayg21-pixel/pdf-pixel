using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Web.Dto.Model;

/// <summary>
/// Layer configuration, <c>{ handle, name, isDefault, layers }</c>.
/// </summary>
internal sealed class LayerConfigurationDto
{
    public int Handle { get; set; }

    public string? Name { get; set; }

    public bool IsDefault { get; set; }

    public required IReadOnlyList<LayerDto> Layers { get; set; }
}
