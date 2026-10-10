using PdfPixel.OptionalContent.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Bindable layers of a panel: the document's layer configurations and the one content is rendered with.
/// </summary>
public sealed class PdfLayersViewModel : INotifyPropertyChanged
{
    private readonly PdfPanelLayers _layers;
    private PdfLayerConfigurationViewModel? _configuration;

    /// <summary>
    /// Initializes a view model over <paramref name="layers"/>.
    /// </summary>
    /// <param name="layers">The panel's layers.</param>
    public PdfLayersViewModel(PdfPanelLayers layers)
    {
        _layers = layers ?? throw new ArgumentNullException(nameof(layers));

        List<PdfLayerConfigurationViewModel> configurations = new(layers.Configurations.Count);
        foreach (PdfUserOptionalContentConfiguration configuration in layers.Configurations)
        {
            PdfLayerConfigurationViewModel viewModel = new(configuration, configuration.Source == layers.DefaultSource, OnChanged);
            configurations.Add(viewModel);

            if (configuration == layers.Configuration)
            {
                _configuration = viewModel;
            }
        }

        Configurations = configurations;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised when the rendered content changes: a layer was shown or hidden, or another configuration was selected.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// The document's layer configurations: the default configuration first, then the alternate ones.
    /// </summary>
    public IReadOnlyList<PdfLayerConfigurationViewModel> Configurations { get; }

    /// <summary>
    /// Configuration content is rendered with, one of <see cref="Configurations"/>.
    /// </summary>
    public PdfLayerConfigurationViewModel? Configuration
    {
        get => _configuration;

        set
        {
            if (_configuration == value)
            {
                return;
            }

            _configuration = value;
            _layers.Configuration = value?.Configuration;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Configuration)));
            OnChanged();
        }
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
