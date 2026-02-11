using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
using System;

namespace PdfPixel.Forms;

/// <summary>
/// Represents a PDF text form field.
/// </summary>
/// <remarks>
/// Text fields allow the user to enter arbitrary text. They can be single-line or multiline,
/// and can have various formatting constraints.
/// </remarks>
public class PdfTextFormField : PdfFormField
{
    private int _caretPosition;
    private int _selectionStart;
    private int _selectionLength;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfTextFormField"/> class.
    /// </summary>
    /// <param name="fieldObject">The PDF object representing this text field.</param>
    public PdfTextFormField(PdfObject fieldObject)
        : base(fieldObject, PdfFormFieldType.Text)
    {
        var dictionary = fieldObject.Dictionary;
        MaxLength = dictionary.GetInteger(PdfTokens.MaxLenKey);
    }

    /// <summary>
    /// Gets the maximum length of the field's text, in characters.
    /// </summary>
    /// <remarks>
    /// Returns null if no maximum length is specified.
    /// </remarks>
    public int? MaxLength { get; }

    /// <summary>
    /// Gets a value indicating whether this is a multiline text field.
    /// </summary>
    public bool IsMultiline => Flags.HasFlag(PdfFormFieldFlags.Multiline);

    /// <summary>
    /// Gets a value indicating whether this is a password field.
    /// </summary>
    public bool IsPassword => Flags.HasFlag(PdfFormFieldFlags.Password);

    /// <summary>
    /// Gets a value indicating whether this is a file select field.
    /// </summary>
    public bool IsFileSelect => Flags.HasFlag(PdfFormFieldFlags.FileSelect);

    /// <summary>
    /// Gets a value indicating whether this field uses comb formatting.
    /// </summary>
    /// <remarks>
    /// Comb formatting divides the field into as many positions as MaxLength,
    /// with one character per position.
    /// </remarks>
    public bool IsComb => Flags.HasFlag(PdfFormFieldFlags.Comb);

    /// <summary>
    /// Gets the current caret position in the text.
    /// </summary>
    public int CaretPosition => _caretPosition;

    /// <summary>
    /// Gets the current selection start position.
    /// </summary>
    public int SelectionStart => _selectionStart;

    /// <summary>
    /// Gets the length of the current selection.
    /// </summary>
    public int SelectionLength => _selectionLength;

    /// <summary>
    /// Gets a value indicating whether there is an active selection.
    /// </summary>
    public bool HasSelection => _selectionLength > 0;

    /// <summary>
    /// Gets the text value of this field.
    /// </summary>
    /// <returns>The field's text value, or an empty string if not set.</returns>
    public string GetTextValue()
    {
        if (Value == null || Value.Type != PdfValueType.String)
        {
            return string.Empty;
        }

        return Value.AsString().ToString();
    }

    /// <summary>
    /// Handles mouse down event to position caret or start selection.
    /// </summary>
    public override bool OnMouseDown(SKPoint position, FormFieldPointerState state)
    {
        if (IsReadOnly)
        {
            return false;
        }

        if (!HasFocus)
        {
            Focus();
        }

        int clickPosition = GetCharacterIndexAtPosition(position);
        _caretPosition = clickPosition;
        _selectionStart = clickPosition;
        _selectionLength = 0;

        return true;
    }

    /// <summary>
    /// Handles mouse move event to update selection.
    /// </summary>
    public override bool OnMouseMove(SKPoint position, FormFieldPointerState state)
    {
        if (!HasFocus || state != FormFieldPointerState.Pressed)
        {
            return false;
        }

        int currentPosition = GetCharacterIndexAtPosition(position);
        if (currentPosition != _caretPosition)
        {
            _selectionStart = Math.Min(_caretPosition, currentPosition);
            _selectionLength = Math.Abs(currentPosition - _caretPosition);
            _caretPosition = currentPosition;
        }

        return true;
    }

    /// <summary>
    /// Handles key down event for text editing.
    /// </summary>
    public override bool OnKeyDown(FormFieldKey key, FormFieldKeyModifiers modifiers)
    {
        if (IsReadOnly || !HasFocus)
        {
            return false;
        }

        switch (key)
        {
            case FormFieldKey.Left:
                MoveCaret(-1, modifiers.HasFlag(FormFieldKeyModifiers.Shift));
                return true;

            case FormFieldKey.Right:
                MoveCaret(1, modifiers.HasFlag(FormFieldKeyModifiers.Shift));
                return true;

            case FormFieldKey.Home:
                MoveCaretToStart(modifiers.HasFlag(FormFieldKeyModifiers.Shift));
                return true;

            case FormFieldKey.End:
                MoveCaretToEnd(modifiers.HasFlag(FormFieldKeyModifiers.Shift));
                return true;

            case FormFieldKey.Backspace:
                DeleteCharacter(false);
                return true;

            case FormFieldKey.Delete:
                DeleteCharacter(true);
                return true;

            default:
                return false;
        }
    }

    /// <summary>
    /// Handles text input event to insert text at caret position.
    /// </summary>
    public override bool OnTextInput(string text)
    {
        if (IsReadOnly || !HasFocus || string.IsNullOrEmpty(text))
        {
            return false;
        }

        InsertTextAtCaret(text);
        return true;
    }

    /// <summary>
    /// Sets focus and positions caret at the end of the text.
    /// </summary>
    public override void Focus()
    {
        base.Focus();

        if (HasFocus)
        {
            string currentText = GetTextValue();
            _caretPosition = currentText.Length;
            _selectionStart = 0;
            _selectionLength = 0;
        }
    }

    /// <summary>
    /// Gets the character index at the specified position in PDF coordinates.
    /// </summary>
    /// <param name="position">Position in PDF coordinates relative to field rectangle.</param>
    /// <returns>Character index.</returns>
    /// <remarks>
    /// This is a simplified implementation. A complete implementation would need
    /// to measure the actual text layout using the field's font and appearance.
    /// </remarks>
    private int GetCharacterIndexAtPosition(SKPoint position)
    {
        string text = GetTextValue();
        int estimatedIndex = (int)(position.X / 10);
        return Math.Max(0, Math.Min(estimatedIndex, text.Length));
    }

    /// <summary>
    /// Moves the caret by the specified offset.
    /// </summary>
    /// <param name="offset">Number of characters to move (negative for left, positive for right).</param>
    /// <param name="extendSelection">Whether to extend the selection.</param>
    private void MoveCaret(int offset, bool extendSelection)
    {
        string text = GetTextValue();
        int newPosition = Math.Max(0, Math.Min(_caretPosition + offset, text.Length));

        if (extendSelection)
        {
            if (_selectionLength == 0)
            {
                _selectionStart = _caretPosition;
            }

            _caretPosition = newPosition;
            _selectionLength = Math.Abs(_caretPosition - _selectionStart);
        }
        else
        {
            _caretPosition = newPosition;
            _selectionStart = newPosition;
            _selectionLength = 0;
        }
    }

    /// <summary>
    /// Moves the caret to the start of the text.
    /// </summary>
    private void MoveCaretToStart(bool extendSelection)
    {
        if (extendSelection)
        {
            _selectionLength = _caretPosition - 0;
            _selectionStart = 0;
        }
        else
        {
            _selectionStart = 0;
            _selectionLength = 0;
        }

        _caretPosition = 0;
    }

    /// <summary>
    /// Moves the caret to the end of the text.
    /// </summary>
    private void MoveCaretToEnd(bool extendSelection)
    {
        string text = GetTextValue();

        if (extendSelection)
        {
            _selectionLength = text.Length - _caretPosition;
            _selectionStart = _caretPosition;
        }
        else
        {
            _selectionStart = text.Length;
            _selectionLength = 0;
        }

        _caretPosition = text.Length;
    }

    /// <summary>
    /// Deletes the character before or after the caret.
    /// </summary>
    /// <param name="deleteForward">True to delete after caret (Delete), false to delete before (Backspace).</param>
    /// <remarks>
    /// This method should trigger a value change event in the higher-level implementation.
    /// </remarks>
    protected virtual void DeleteCharacter(bool deleteForward)
    {
    }

    /// <summary>
    /// Inserts text at the current caret position.
    /// </summary>
    /// <param name="text">Text to insert.</param>
    /// <remarks>
    /// This method should trigger a value change event in the higher-level implementation.
    /// </remarks>
    protected virtual void InsertTextAtCaret(string text)
    {
    }
}
