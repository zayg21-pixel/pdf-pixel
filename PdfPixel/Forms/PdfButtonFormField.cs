using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;

namespace PdfPixel.Forms;

/// <summary>
/// Represents a PDF button form field.
/// </summary>
/// <remarks>
/// Button fields include push buttons, checkboxes, and radio buttons.
/// The field flags determine the specific button type.
/// </remarks>
public class PdfButtonFormField : PdfFormField
{
    private bool _isPressed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfButtonFormField"/> class.
    /// </summary>
    /// <param name="fieldObject">The PDF object representing this button field.</param>
    public PdfButtonFormField(PdfObject fieldObject)
        : base(fieldObject, PdfFormFieldType.Button)
    {
    }

    /// <summary>
    /// Gets a value indicating whether this is a push button.
    /// </summary>
    public bool IsPushButton => Flags.HasFlag(PdfFormFieldFlags.PushButton);

    /// <summary>
    /// Gets a value indicating whether this is a radio button.
    /// </summary>
    public bool IsRadio => Flags.HasFlag(PdfFormFieldFlags.Radio);

    /// <summary>
    /// Gets a value indicating whether this is a checkbox.
    /// </summary>
    /// <remarks>
    /// A checkbox is a button that is neither a push button nor a radio button.
    /// </remarks>
    public bool IsCheckbox => !IsPushButton && !IsRadio;

    /// <summary>
    /// Gets a value indicating whether the button is checked.
    /// </summary>
    /// <returns>True if the button is checked, false otherwise.</returns>
    public bool IsChecked()
    {
        if (Value == null || Value.Type != PdfValueType.Name)
        {
            return false;
        }

        var valueName = Value.AsName();
        return !valueName.IsEmpty && valueName != (PdfString)"Off"u8;
    }

    /// <summary>
    /// Handles mouse down event on the button field.
    /// </summary>
    public override bool OnMouseDown(SKPoint position, FormFieldPointerState state)
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
    public override bool OnMouseUp(SKPoint position, FormFieldPointerState state)
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
    public override void OnMouseLeave()
    {
        _isPressed = false;
    }

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
    /// <remarks>
    /// This method should trigger a value change event in the higher-level implementation.
    /// </remarks>
    protected virtual void ToggleCheckedState()
    {
    }

    /// <summary>
    /// Handles button click action (for push buttons).
    /// </summary>
    /// <remarks>
    /// This method should trigger an action execution in the higher-level implementation.
    /// </remarks>
    protected virtual void OnButtonClicked()
    {
    }
}
