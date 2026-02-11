using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfPixel.Forms;

/// <summary>
/// Represents a PDF choice form field.
/// </summary>
/// <remarks>
/// Choice fields allow the user to select one or more options from a list.
/// They can be combo boxes (drop-down lists) or list boxes.
/// </remarks>
public class PdfChoiceFormField : PdfFormField
{
    private int _highlightedIndex = -1;
    private bool _isDropdownOpen;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfChoiceFormField"/> class.
    /// </summary>
    /// <param name="fieldObject">The PDF object representing this choice field.</param>
    public PdfChoiceFormField(PdfObject fieldObject)
        : base(fieldObject, PdfFormFieldType.Choice)
    {
        var dictionary = fieldObject.Dictionary;
        Options = dictionary.GetArray(PdfTokens.OptKey);
        TopIndex = dictionary.GetIntegerOrDefault(PdfTokens.TopIndexKey);
        SelectedIndices = dictionary.GetArray(PdfTokens.IndicesKey);
    }

    /// <summary>
    /// Gets the options array.
    /// </summary>
    /// <remarks>
    /// Each element can be either a text string or a two-element array containing
    /// the export value and display text.
    /// </remarks>
    public PdfArray Options { get; }

    /// <summary>
    /// Gets the index of the first visible option in a scrollable list box.
    /// </summary>
    public int TopIndex { get; }

    /// <summary>
    /// Gets the array of selected option indices for multiselect list boxes.
    /// </summary>
    public PdfArray SelectedIndices { get; }

    /// <summary>
    /// Gets a value indicating whether this is a combo box.
    /// </summary>
    public bool IsCombo => Flags.HasFlag(PdfFormFieldFlags.Combo);

    /// <summary>
    /// Gets a value indicating whether this combo box is editable.
    /// </summary>
    public bool IsEditable => Flags.HasFlag(PdfFormFieldFlags.Edit);

    /// <summary>
    /// Gets a value indicating whether options are sorted.
    /// </summary>
    public bool IsSorted => Flags.HasFlag(PdfFormFieldFlags.Sort);

    /// <summary>
    /// Gets a value indicating whether multiple selection is allowed.
    /// </summary>
    public bool IsMultiSelect => Flags.HasFlag(PdfFormFieldFlags.MultiSelect);

    /// <summary>
    /// Gets the display texts for all options.
    /// </summary>
    /// <returns>An enumerable of display texts.</returns>
    public IEnumerable<string> GetDisplayTexts()
    {
        if (Options == null || Options.Count == 0)
        {
            yield break;
        }

        for (int i = 0; i < Options.Count; i++)
        {
            var option = Options.GetValue(i);
            if (option != null && option.Type == PdfValueType.String)
            {
                yield return option.AsString().ToString();
            }
            else if (option != null && option.Type == PdfValueType.Array)
            {
                var optionArray = option.AsArray();
                if (optionArray != null && optionArray.Count >= 2)
                {
                    var displayText = optionArray.GetString(1);
                    if (!displayText.IsEmpty)
                    {
                        yield return displayText.ToString();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets the selected value(s) as text.
    /// </summary>
    /// <returns>An enumerable of selected values.</returns>
    public IEnumerable<string> GetSelectedValues()
    {
        if (Value == null)
        {
            yield break;
        }

        if (Value.Type == PdfValueType.String)
        {
            yield return Value.AsString().ToString();
        }
        else if (Value.Type == PdfValueType.Array)
        {
            var valueArray = Value.AsArray();
            if (valueArray != null)
            {
                for (int i = 0; i < valueArray.Count; i++)
                {
                    var item = valueArray.GetValue(i);
                    if (item != null && item.Type == PdfValueType.String)
                    {
                        yield return item.AsString().ToString();
                    }
                }
            }
        }
    }

    /// <summary>
    /// Gets the index of the currently highlighted option (for keyboard navigation).
    /// </summary>
    public int HighlightedIndex => _highlightedIndex;

    /// <summary>
    /// Gets a value indicating whether the dropdown is currently open (for combo boxes).
    /// </summary>
    public bool IsDropdownOpen => _isDropdownOpen;

    /// <summary>
    /// Gets the number of available options.
    /// </summary>
    public int OptionCount => Options?.Count ?? 0;

    /// <summary>
    /// Handles mouse down event on the choice field.
    /// </summary>
    public override bool OnMouseDown(SKPoint position, FormFieldPointerState state)
    {
        if (IsReadOnly)
        {
            return false;
        }

        if (IsCombo)
        {
            _isDropdownOpen = !_isDropdownOpen;

            if (_isDropdownOpen && !HasFocus)
            {
                Focus();
            }

            return true;
        }
        else
        {
            if (!HasFocus)
            {
                Focus();
            }

            int clickedIndex = GetOptionIndexAtPosition(position);
            if (clickedIndex >= 0 && clickedIndex < OptionCount)
            {
                SelectOption(clickedIndex, state == FormFieldPointerState.Pressed);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Handles mouse up event on the choice field.
    /// </summary>
    public override bool OnMouseUp(SKPoint position, FormFieldPointerState state)
    {
        if (IsReadOnly || !_isDropdownOpen)
        {
            return false;
        }

        int clickedIndex = GetOptionIndexAtPosition(position);
        if (clickedIndex >= 0 && clickedIndex < OptionCount)
        {
            SelectOption(clickedIndex, false);

            if (IsCombo)
            {
                _isDropdownOpen = false;
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Handles mouse move event to update highlighted option.
    /// </summary>
    public override bool OnMouseMove(SKPoint position, FormFieldPointerState state)
    {
        if (IsReadOnly)
        {
            return false;
        }

        int hoveredIndex = GetOptionIndexAtPosition(position);
        if (hoveredIndex != _highlightedIndex)
        {
            _highlightedIndex = hoveredIndex;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Handles mouse leave event.
    /// </summary>
    public override void OnMouseLeave()
    {
        _highlightedIndex = -1;
    }

    /// <summary>
    /// Handles key down event for option navigation and selection.
    /// </summary>
    public override bool OnKeyDown(FormFieldKey key, FormFieldKeyModifiers modifiers)
    {
        if (IsReadOnly || !HasFocus)
        {
            return false;
        }

        switch (key)
        {
            case FormFieldKey.Up:
                NavigateOptions(-1);
                return true;

            case FormFieldKey.Down:
                if (IsCombo && !_isDropdownOpen)
                {
                    _isDropdownOpen = true;
                }
                else
                {
                    NavigateOptions(1);
                }
                return true;

            case FormFieldKey.Home:
                _highlightedIndex = 0;
                return true;

            case FormFieldKey.End:
                _highlightedIndex = OptionCount - 1;
                return true;

            case FormFieldKey.Enter:
            case FormFieldKey.Space:
                if (_highlightedIndex >= 0 && _highlightedIndex < OptionCount)
                {
                    SelectOption(_highlightedIndex, modifiers.HasFlag(FormFieldKeyModifiers.Control));

                    if (IsCombo)
                    {
                        _isDropdownOpen = false;
                    }
                }
                return true;

            case FormFieldKey.Escape:
                if (IsCombo && _isDropdownOpen)
                {
                    _isDropdownOpen = false;
                    return true;
                }
                break;

            case FormFieldKey.Tab:
                if (IsCombo && _isDropdownOpen)
                {
                    _isDropdownOpen = false;
                }
                return false;
        }

        return false;
    }

    /// <summary>
    /// Handles text input for editable combo boxes.
    /// </summary>
    public override bool OnTextInput(string text)
    {
        if (IsReadOnly || !HasFocus || !IsEditable || string.IsNullOrEmpty(text))
        {
            return false;
        }

        FilterOptions(text);
        return true;
    }

    /// <summary>
    /// Sets focus and initializes the highlighted index.
    /// </summary>
    public override void Focus()
    {
        base.Focus();

        if (HasFocus && _highlightedIndex < 0)
        {
            _highlightedIndex = GetFirstSelectedIndex();
            if (_highlightedIndex < 0 && OptionCount > 0)
            {
                _highlightedIndex = 0;
            }
        }
    }

    /// <summary>
    /// Removes focus and closes dropdown.
    /// </summary>
    public override void Blur()
    {
        base.Blur();
        _isDropdownOpen = false;
        _highlightedIndex = -1;
    }

    /// <summary>
    /// Gets the option index at the specified position in PDF coordinates.
    /// </summary>
    /// <param name="position">Position in PDF coordinates relative to field rectangle.</param>
    /// <returns>Option index, or -1 if no option at that position.</returns>
    /// <remarks>
    /// This is a simplified implementation. A complete implementation would need
    /// to measure the actual option layout using the field's font and appearance.
    /// </remarks>
    private int GetOptionIndexAtPosition(SKPoint position)
    {
        if (Options == null || Options.Count == 0)
        {
            return -1;
        }

        float itemHeight = 20.0f;
        int index = (int)(position.Y / itemHeight);

        return index >= 0 && index < Options.Count ? index : -1;
    }

    /// <summary>
    /// Navigates the highlighted option by the specified offset.
    /// </summary>
    /// <param name="offset">Number of options to move (negative for up, positive for down).</param>
    private void NavigateOptions(int offset)
    {
        if (OptionCount == 0)
        {
            return;
        }

        if (_highlightedIndex < 0)
        {
            _highlightedIndex = offset > 0 ? 0 : OptionCount - 1;
        }
        else
        {
            _highlightedIndex = Math.Max(0, Math.Min(_highlightedIndex + offset, OptionCount - 1));
        }
    }

    /// <summary>
    /// Gets the index of the first selected option.
    /// </summary>
    /// <returns>Index of the first selected option, or -1 if none selected.</returns>
    private int GetFirstSelectedIndex()
    {
        if (SelectedIndices != null && SelectedIndices.Count > 0)
        {
            var firstIndex = SelectedIndices.GetInteger(0);
            return firstIndex ?? -1;
        }

        return -1;
    }

    /// <summary>
    /// Selects the option at the specified index.
    /// </summary>
    /// <param name="index">Index of the option to select.</param>
    /// <param name="addToSelection">Whether to add to existing selection (for multiselect).</param>
    /// <remarks>
    /// This method updates the highlighted index and should trigger a value change event
    /// in the higher-level implementation to persist the selection to the PDF document.
    /// </remarks>
    protected virtual void SelectOption(int index, bool addToSelection)
    {
        if (index >= 0 && index < OptionCount)
        {
            _highlightedIndex = index;
        }
    }

    /// <summary>
    /// Filters options based on the input text (for editable combo boxes).
    /// </summary>
    /// <param name="text">Text to filter by.</param>
    /// <remarks>
    /// This method should update the visible options in the higher-level implementation.
    /// </remarks>
    protected virtual void FilterOptions(string text)
    {
    }
}
