using PdfPixel.OptionalContent.Model;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Bindable optional content configuration: its name and presentation tree of switchable groups.
/// </summary>
public sealed class PdfOptionalContentConfigurationViewModel
{
    internal PdfOptionalContentConfigurationViewModel(PdfUserOptionalContentConfiguration configuration, Action onStateChanged)
    {
        Configuration = configuration;
        Name = configuration.Source.Name?.DecodePdfString() ?? string.Empty;

        List<PdfOptionalContentItemViewModel> groupItems = [];
        Items = BuildItems(configuration.Items, onStateChanged, groupItems);
        LinkRadioButtonSiblings(groupItems);
    }

    /// <summary>
    /// Name of the configuration; empty when the document gives none.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Presentation tree of the configuration's groups.
    /// </summary>
    public IReadOnlyList<PdfOptionalContentItemViewModel> Items { get; }

    internal PdfUserOptionalContentConfiguration Configuration { get; }

    private static List<PdfOptionalContentItemViewModel> BuildItems(
        IReadOnlyList<PdfOptionalContentItem> items,
        Action onStateChanged,
        List<PdfOptionalContentItemViewModel> groupItems)
    {
        List<PdfOptionalContentItemViewModel> result = new(items.Count);

        foreach (PdfOptionalContentItem item in items)
        {
            PdfOptionalContentItemViewModel viewModel = new(item, onStateChanged, BuildItems(item.Children, onStateChanged, groupItems));
            result.Add(viewModel);

            if (viewModel.IsGroup)
            {
                groupItems.Add(viewModel);
            }
        }

        return result;
    }

    private static void LinkRadioButtonSiblings(List<PdfOptionalContentItemViewModel> groupItems)
    {
        foreach (PdfOptionalContentItemViewModel item in groupItems)
        {
            if (item.State == null)
            {
                continue;
            }

            foreach (PdfOptionalContentItemViewModel candidate in groupItems)
            {
                if (candidate.State != null && candidate != item && item.State.RadioButtonSiblings.Contains(candidate.State))
                {
                    item.AddRadioButtonSibling(candidate);
                }
            }
        }
    }
}
