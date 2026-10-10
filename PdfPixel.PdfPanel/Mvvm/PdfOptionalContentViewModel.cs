using PdfPixel.OptionalContent.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Bindable optional content (layers) of a panel: the document's configurations and the one content is rendered with.
/// </summary>
public sealed class PdfOptionalContentViewModel : INotifyPropertyChanged
{
    private readonly PdfPanelOptionalContent _optionalContent;
    private PdfOptionalContentConfigurationViewModel? _configuration;

    /// <summary>
    /// Initializes a view model over <paramref name="optionalContent"/>.
    /// </summary>
    /// <param name="optionalContent">The panel's optional content.</param>
    public PdfOptionalContentViewModel(PdfPanelOptionalContent optionalContent)
    {
        _optionalContent = optionalContent ?? throw new ArgumentNullException(nameof(optionalContent));

        List<PdfOptionalContentConfigurationViewModel> configurations = new(optionalContent.Configurations.Count);
        foreach (PdfUserOptionalContentConfiguration configuration in optionalContent.Configurations)
        {
            PdfOptionalContentConfigurationViewModel viewModel = new(configuration, OnChanged);
            configurations.Add(viewModel);

            if (configuration == optionalContent.Configuration)
            {
                _configuration = viewModel;
            }
        }

        Configurations = configurations;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised when the rendered content changes: a group was switched or another configuration was selected.
    /// </summary>
    public event EventHandler? Changed;

    /// <summary>
    /// The document's configurations: the default configuration first, then the alternate ones.
    /// </summary>
    public IReadOnlyList<PdfOptionalContentConfigurationViewModel> Configurations { get; }

    /// <summary>
    /// Configuration content is rendered with, one of <see cref="Configurations"/>.
    /// </summary>
    public PdfOptionalContentConfigurationViewModel? Configuration
    {
        get => _configuration;

        set
        {
            if (_configuration == value)
            {
                return;
            }

            _configuration = value;
            _optionalContent.Configuration = value?.Configuration;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Configuration)));
            OnChanged();
        }
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
