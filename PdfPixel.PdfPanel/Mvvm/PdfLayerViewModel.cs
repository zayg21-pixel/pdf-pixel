using PdfPixel.OptionalContent.Model;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Bindable node of a layers tree: a switchable layer, or a list of layers with an optional label.
/// </summary>
public sealed class PdfLayerViewModel : INotifyPropertyChanged
{
    private readonly PdfOptionalContentGroupState? _state;
    private readonly Action _onVisibilityChanged;
    private readonly List<PdfLayerViewModel> _radioButtonSiblings = [];
    private string? _name;
    private bool _reportedIsVisible;

    internal PdfLayerViewModel(PdfOptionalContentItem item, Action onVisibilityChanged, IReadOnlyList<PdfLayerViewModel> children)
    {
        _state = item.State;
        _onVisibilityChanged = onVisibilityChanged;
        Children = children;
        _reportedIsVisible = IsVisible;

        if (_state != null)
        {
            _name = _state.Group.Name.DecodePdfString();
        }
        else if (item.Label != null)
        {
            _name = item.Label.Value.DecodePdfString();
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Name of the layer (/Name) or label of the list, or <see langword="null"/> when the document defines none.
    /// </summary>
    public string? Name
    {
        get => _name;

        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged(nameof(Name));
            }
        }
    }

    /// <summary>
    /// Whether the node is a switchable layer; <see langword="false"/> for a list.
    /// </summary>
    public bool IsLayer => _state != null;

    /// <summary>
    /// Whether the layer is visible; making it visible hides the other layers of its radio-button sets.
    /// </summary>
    public bool IsVisible
    {
        get => _state != null && _state.IsOn;

        set
        {
            if (_state == null || _state.IsOn == value)
            {
                return;
            }

            _state.IsOn = value;
            ReportIsVisible();

            foreach (PdfLayerViewModel sibling in _radioButtonSiblings)
            {
                sibling.ReportIsVisible();
            }

            _onVisibilityChanged();
        }
    }

    /// <summary>
    /// Whether the document locks the layer against changes through a user interface.
    /// </summary>
    public bool IsLocked => _state != null && _state.IsLocked;

    /// <summary>
    /// Nodes of a list; empty for a layer.
    /// </summary>
    public IReadOnlyList<PdfLayerViewModel> Children { get; }

    internal PdfOptionalContentGroupState? State => _state;

    internal void AddRadioButtonSibling(PdfLayerViewModel sibling) => _radioButtonSiblings.Add(sibling);

    /// <summary>
    /// Reports <see cref="IsVisible"/> of this node and its children where the layer state changed since it was
    /// last reported.
    /// </summary>
    internal void Synchronize()
    {
        if (IsVisible != _reportedIsVisible)
        {
            ReportIsVisible();
        }

        foreach (PdfLayerViewModel child in Children)
        {
            child.Synchronize();
        }
    }

    private void ReportIsVisible()
    {
        _reportedIsVisible = IsVisible;
        OnPropertyChanged(nameof(IsVisible));
    }

    private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
