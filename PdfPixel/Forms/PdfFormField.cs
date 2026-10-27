using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using System;

namespace PdfPixel.Forms;

/// <summary>
/// Base class for PDF form fields (AcroForm fields).
/// </summary>
public abstract class PdfFormField : IFormFieldMouseInteraction, IFormFieldKeyboardInteraction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFormField"/> class.
    /// </summary>
    /// <param name="fieldObject">The PDF object representing this form field.</param>
    /// <param name="fieldType">The field type.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fieldObject"/> is null.</exception>
    private protected PdfFormField(PdfObject fieldObject, PdfFormFieldType fieldType)
    {
        if (fieldObject == null)
        {
            throw new ArgumentNullException(nameof(fieldObject));
        }

        FieldType = (fieldType == PdfFormFieldType.Unknown) ? throw new ArgumentException("FieldType cannot be Unknown", nameof(fieldType)) : fieldType;

        PdfDictionary dictionary = fieldObject.Dictionary;

        PartialName = dictionary.GetString(PdfTokens.TitleKey);
        AlternateName = dictionary.GetString(PdfTokens.NameKey);
        MappingName = dictionary.GetString(PdfTokens.NameKey);
        Flags = (PdfFormFieldFlags)dictionary.GetIntegerOrDefault(PdfTokens.FieldFlagsKey);
        Value = dictionary.GetValue(PdfTokens.ValueKey);
        DefaultValue = dictionary.GetValue(PdfTokens.DefaultValueKey);
        Parent = dictionary.GetReference(PdfTokens.ParentKey);
        DefaultAppearance = dictionary.GetString(PdfTokens.DefaultAppearanceKey);
        Quadding = dictionary.GetIntegerOrDefault(PdfTokens.QuadKey);
    }

    /// <summary>
    /// Gets the field type.
    /// </summary>
    public PdfFormFieldType FieldType { get; }

    /// <summary>
    /// Gets the partial field name.
    /// </summary>
    public PdfString? PartialName { get; }

    /// <summary>
    /// Gets the alternate field name for user interface purposes.
    /// </summary>
    public PdfString? AlternateName { get; }

    /// <summary>
    /// Gets the mapping name for export operations.
    /// </summary>
    public PdfString? MappingName { get; }

    /// <summary>
    /// Gets the field flags.
    /// </summary>
    public PdfFormFieldFlags Flags { get; }

    /// <summary>
    /// Gets the field value.
    /// </summary>
    public PdfValue? Value { get; }

    /// <summary>
    /// Gets the default field value.
    /// </summary>
    public PdfValue? DefaultValue { get; }

    /// <summary>
    /// Gets the reference to the parent field, or <see langword="null"/> when absent.
    /// </summary>
    public PdfReference? Parent { get; }

    /// <summary>
    /// Gets the default appearance string.
    /// </summary>
    public PdfString? DefaultAppearance { get; }

    /// <summary>
    /// Gets the quadding (text alignment) value.
    /// </summary>
    public int Quadding { get; }

    /// <summary>
    /// Gets a value indicating whether this field is read-only.
    /// </summary>
    public bool IsReadOnly => (Flags & PdfFormFieldFlags.ReadOnly) != 0;

    /// <summary>
    /// Gets a value indicating whether this field is required.
    /// </summary>
    public bool IsRequired => (Flags & PdfFormFieldFlags.Required) != 0;

    /// <summary>
    /// Gets the fully qualified field name.
    /// </summary>
    /// <returns>The fully qualified field name, or <c>null</c> when the field carries no partial name.</returns>
    public virtual string? GetFullyQualifiedName() => PartialName?.ToString();

    /// <summary>
    /// Handles mouse down event on the form field.
    /// </summary>
    /// <param name="position">Mouse position in PDF coordinates relative to the field's rectangle.</param>
    /// <param name="state">Current pointer state.</param>
    /// <returns>True if the event was handled.</returns>
    public virtual bool OnMouseDown(PdfPoint position, FormFieldPointerState state) => false;

    /// <summary>
    /// Handles mouse up event on the form field.
    /// </summary>
    /// <param name="position">Mouse position in PDF coordinates relative to the field's rectangle.</param>
    /// <param name="state">Current pointer state.</param>
    /// <returns>True if the event was handled.</returns>
    public virtual bool OnMouseUp(PdfPoint position, FormFieldPointerState state) => false;

    /// <summary>
    /// Handles mouse move event on the form field.
    /// </summary>
    /// <param name="position">Mouse position in PDF coordinates relative to the field's rectangle.</param>
    /// <param name="state">Current pointer state.</param>
    /// <returns>True if the event was handled.</returns>
    public virtual bool OnMouseMove(PdfPoint position, FormFieldPointerState state) => false;

    /// <summary>
    /// Handles mouse enter event when the pointer enters the field's bounds.
    /// </summary>
    public virtual void OnMouseEnter()
    {
    }

    /// <summary>
    /// Handles mouse leave event when the pointer exits the field's bounds.
    /// </summary>
    public virtual void OnMouseLeave()
    {
    }

    /// <summary>
    /// Handles key down event on the form field.
    /// </summary>
    /// <param name="key">The key that was pressed.</param>
    /// <param name="modifiers">Keyboard modifiers.</param>
    /// <returns>True if the event was handled.</returns>
    public virtual bool OnKeyDown(FormFieldKey key, FormFieldKeyModifiers modifiers) => false;

    /// <summary>
    /// Handles text input event on the form field.
    /// </summary>
    /// <param name="text">The text that was input.</param>
    /// <returns>True if the event was handled.</returns>
    public virtual bool OnTextInput(string text) => false;

    /// <summary>
    /// Gets a value indicating whether this field can receive keyboard focus.
    /// </summary>
    public virtual bool CanReceiveFocus => !IsReadOnly;

    /// <summary>
    /// Gets a value indicating whether this field currently has keyboard focus.
    /// </summary>
    public bool HasFocus { get; private set; }

    /// <summary>
    /// Sets focus to this field.
    /// </summary>
    public virtual void Focus()
    {
        if (CanReceiveFocus)
        {
            HasFocus = true;
        }
    }

    /// <summary>
    /// Removes focus from this field.
    /// </summary>
    public virtual void Blur() => HasFocus = false;
}
