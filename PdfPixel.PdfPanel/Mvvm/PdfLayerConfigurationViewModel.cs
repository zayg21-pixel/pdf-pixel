using PdfPixel.OptionalContent.Model;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Bindable layer configuration: its name and tree of switchable layers.
/// </summary>
public sealed class PdfLayerConfigurationViewModel : INotifyPropertyChanged
{
    private string? _name;

    internal PdfLayerConfigurationViewModel(PdfUserOptionalContentConfiguration configuration, bool isDefault, Action onVisibilityChanged)
    {
        Configuration = configuration;
        IsDefault = isDefault;
        _name = configuration.Source.Name?.DecodePdfString();

        List<PdfLayerViewModel> layers = [];
        Layers = BuildLayers(configuration.Items, onVisibilityChanged, layers);
        LinkRadioButtonSiblings(layers);
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Name of the configuration (/Name), or <see langword="null"/> when the document defines none.
    /// </summary>
    public string? Name
    {
        get => _name;

        set
        {
            if (_name != value)
            {
                _name = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
            }
        }
    }

    /// <summary>
    /// Whether this is the document's default configuration (/D).
    /// </summary>
    public bool IsDefault { get; }

    /// <summary>
    /// Tree of the configuration's layers.
    /// </summary>
    public IReadOnlyList<PdfLayerViewModel> Layers { get; }

    internal PdfUserOptionalContentConfiguration Configuration { get; }

    /// <summary>
    /// Reports the visibility of the layers whose state changed since it was last reported.
    /// </summary>
    internal void Synchronize()
    {
        foreach (PdfLayerViewModel layer in Layers)
        {
            layer.Synchronize();
        }
    }

    private static List<PdfLayerViewModel> BuildLayers(
        IReadOnlyList<PdfOptionalContentItem> items,
        Action onVisibilityChanged,
        List<PdfLayerViewModel> layers)
    {
        List<PdfLayerViewModel> result = new(items.Count);

        foreach (PdfOptionalContentItem item in items)
        {
            PdfLayerViewModel viewModel = new(item, onVisibilityChanged, BuildLayers(item.Children, onVisibilityChanged, layers));
            result.Add(viewModel);

            if (viewModel.IsLayer)
            {
                layers.Add(viewModel);
            }
        }

        return result;
    }

    private static void LinkRadioButtonSiblings(List<PdfLayerViewModel> layers)
    {
        foreach (PdfLayerViewModel layer in layers)
        {
            if (layer.State == null)
            {
                continue;
            }

            foreach (PdfLayerViewModel candidate in layers)
            {
                if (candidate.State != null && candidate != layer && layer.State.RadioButtonSiblings.Contains(candidate.State))
                {
                    layer.AddRadioButtonSibling(candidate);
                }
            }
        }
    }
}
