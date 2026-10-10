using PdfPixel.OptionalContent.Model;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Bindable node of an optional content presentation tree: a switchable group, or a list with an optional label.
/// </summary>
public sealed class PdfOptionalContentItemViewModel : INotifyPropertyChanged
{
    private readonly PdfOptionalContentGroupState? _state;
    private readonly Action _onStateChanged;
    private readonly List<PdfOptionalContentItemViewModel> _radioButtonSiblings = [];

    internal PdfOptionalContentItemViewModel(PdfOptionalContentItem item, Action onStateChanged, IReadOnlyList<PdfOptionalContentItemViewModel> children)
    {
        _state = item.State;
        _onStateChanged = onStateChanged;
        Children = children;

        if (_state != null)
        {
            Text = _state.Group.Name.DecodePdfString();
        }
        else if (item.Label != null)
        {
            Text = item.Label.Value.DecodePdfString();
        }
        else
        {
            Text = string.Empty;
        }
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Group name, or the label of a list.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Whether the node is a switchable group.
    /// </summary>
    public bool IsGroup => _state != null;

    /// <summary>
    /// Whether the group is ON; turning it ON turns OFF the other groups of its radio-button sets.
    /// </summary>
    public bool IsOn
    {
        get => _state != null && _state.IsOn;

        set
        {
            if (_state == null || _state.IsOn == value)
            {
                return;
            }

            _state.IsOn = value;
            OnPropertyChanged(nameof(IsOn));

            foreach (PdfOptionalContentItemViewModel sibling in _radioButtonSiblings)
            {
                sibling.OnPropertyChanged(nameof(IsOn));
            }

            _onStateChanged();
        }
    }

    /// <summary>
    /// Whether the document locks the group against changes through a user interface.
    /// </summary>
    public bool IsLocked => _state != null && _state.IsLocked;

    /// <summary>
    /// Nodes of a list; empty for a group.
    /// </summary>
    public IReadOnlyList<PdfOptionalContentItemViewModel> Children { get; }

    internal PdfOptionalContentGroupState? State => _state;

    internal void AddRadioButtonSibling(PdfOptionalContentItemViewModel sibling) => _radioButtonSiblings.Add(sibling);

    private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
