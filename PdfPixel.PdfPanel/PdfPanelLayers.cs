using PdfPixel.Models;
using PdfPixel.OptionalContent.Model;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Layers (optional content) shown by the panel: the document's configurations and the one content is rendered with.
/// </summary>
public sealed class PdfPanelLayers
{
    internal PdfPanelLayers(IPdfDocument document)
    {
        List<PdfUserOptionalContentConfiguration> configurations = [];
        PdfOptionalContentProperties? properties = document.OptionalContentProperties;

        if (properties != null)
        {
            DefaultSource = properties.DefaultConfiguration;
            configurations.Add(properties.DefaultConfiguration.ToUserConfiguration());

            foreach (PdfOptionalContentConfiguration configuration in properties.Configurations)
            {
                configurations.Add(configuration.ToUserConfiguration());
            }
        }

        Configurations = configurations;

        if (configurations.Count > 0)
        {
            Configuration = configurations[0];
        }
    }

    /// <summary>
    /// The document's configurations resolved for the user to switch: the default configuration (/D) first,
    /// then the alternate configurations (/Configs). Empty when the document has no optional content.
    /// </summary>
    public IReadOnlyList<PdfUserOptionalContentConfiguration> Configurations { get; }

    /// <summary>
    /// Configuration content is rendered with; initially the default configuration, or <see langword="null"/>
    /// when the document has no optional content.
    /// </summary>
    public PdfUserOptionalContentConfiguration? Configuration { get; set; }

    /// <summary>
    /// The document's default configuration (/D), or null when the document has no optional content.
    /// </summary>
    internal PdfOptionalContentConfiguration? DefaultSource { get; }
}
