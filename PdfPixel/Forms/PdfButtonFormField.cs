using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Text;

namespace PdfPixel.Forms;

/// <summary>
/// Represents a PDF button form field.
/// </summary>
public class PdfButtonFormField : PdfFormField
{
    private bool _isPressed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfButtonFormField"/> class.
    /// </summary>
    /// <param name="fieldObject">The PDF object representing this button field.</param>
    internal PdfButtonFormField(PdfObject fieldObject)
        : base(fieldObject, PdfFormFieldType.Button)
    {
    }

    /// <summary>
    /// Gets a value indicating whether this is a push button.
    /// </summary>
    public bool IsPushButton => (Flags & PdfFormFieldFlags.PushButton) != 0;

    /// <summary>
    /// Gets a value indicating whether this is a radio button.
    /// </summary>
    public bool IsRadio => (Flags & PdfFormFieldFlags.Radio) != 0;

    /// <summary>
    /// Gets a value indicating whether this is a checkbox.
    /// </summary>
    public bool IsCheckbox => !IsPushButton && !IsRadio;

    /// <summary>
    /// Gets a value indicating whether the button is checked.
    /// </summary>
    /// <returns>True if the button is checked, false otherwise.</returns>
    public bool IsChecked()
    {
        PdfString? valueName = Value.AsName();
        if (valueName == null)
        {
            return false;
        }

        return !valueName.Value.IsEmpty && valueName.Value != (PdfString)"Off"u8;
    }

    /// <summary>
    /// Handles mouse down event on the button field.
    /// </summary>
    public override bool OnMouseDown(PdfPoint position, FormFieldPointerState state)
    {
        if (IsReadOnly)
        {
            return false;
        }

        _isPressed = true;
        return true;
    }

    /// <summary>
    /// Handles mouse up event on the button field.
    /// </summary>
    public override bool OnMouseUp(PdfPoint position, FormFieldPointerState state)
    {
        if (IsReadOnly)
        {
            return false;
        }

        if (_isPressed)
        {
            _isPressed = false;

            if (IsPushButton)
            {
                OnButtonClicked();
            }
            else
            {
                ToggleCheckedState();
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Handles mouse leave event.
    /// </summary>
    public override void OnMouseLeave() => _isPressed = false;

    /// <summary>
    /// Gets a value indicating whether this field can receive keyboard focus.
    /// </summary>
    public override bool CanReceiveFocus => !IsPushButton && base.CanReceiveFocus;

    /// <summary>
    /// Handles key down event for checkbox/radio button activation.
    /// </summary>
    public override bool OnKeyDown(FormFieldKey key, FormFieldKeyModifiers modifiers)
    {
        if (IsReadOnly)
        {
            return false;
        }

        if (key == FormFieldKey.Space && HasFocus && !IsPushButton)
        {
            ToggleCheckedState();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Toggles the checked state of the button (for checkboxes and radio buttons).
    /// </summary>
    protected virtual void ToggleCheckedState()
    {
    }

    /// <summary>
    /// Handles button click action (for push buttons).
    /// </summary>
    protected virtual void OnButtonClicked()
    {
    }
}
