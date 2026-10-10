using System.Collections.Generic;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// State of one optional content group in a <see cref="PdfUserOptionalContentConfiguration"/>.
/// </summary>
public sealed class PdfOptionalContentGroupState
{
    private readonly List<PdfOptionalContentGroupState> _radioButtonSiblings = [];
    private bool _isOn;

    internal PdfOptionalContentGroupState(PdfOptionalContentGroup group, bool isLocked)
    {
        Group = group;
        IsLocked = isLocked;
    }

    /// <summary>
    /// The optional content group.
    /// </summary>
    public PdfOptionalContentGroup Group { get; }

    /// <summary>
    /// State resolved from the source configuration.
    /// </summary>
    public bool IsDefaultOn { get; private set; }

    /// <summary>
    /// Whether the group is ON when content is rendered; turning it ON turns OFF the other groups
    /// of its radio-button sets.
    /// </summary>
    public bool IsOn
    {
        get => _isOn;

        set
        {
            _isOn = value;

            if (value)
            {
                foreach (PdfOptionalContentGroupState sibling in _radioButtonSiblings)
                {
                    sibling._isOn = false;
                }
            }
        }
    }

    /// <summary>
    /// Whether the source configuration locks the group against changes through a user interface.
    /// </summary>
    public bool IsLocked { get; }

    /// <summary>
    /// Other groups sharing a radio-button set with this group.
    /// </summary>
    public IReadOnlyList<PdfOptionalContentGroupState> RadioButtonSiblings => _radioButtonSiblings;

    internal void AddRadioButtonSibling(PdfOptionalContentGroupState sibling)
    {
        if (sibling != this && !_radioButtonSiblings.Contains(sibling))
        {
            _radioButtonSiblings.Add(sibling);
        }
    }

    internal void SetIsOnIgnoringRadioButtons(bool isOn) => _isOn = isOn;

    internal void ResetState(bool isDefaultOn)
    {
        IsDefaultOn = isDefaultOn;
        _isOn = isDefaultOn;
    }
}
