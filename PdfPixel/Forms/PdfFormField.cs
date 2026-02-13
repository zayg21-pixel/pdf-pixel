using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
using System;

namespace PdfPixel.Forms;

/// <summary>
/// Base class for PDF form fields (AcroForm fields).
/// </summary>
/// <remarks>
/// Form fields define the interactive data entry and selection elements in a PDF.
/// Widget annotations provide the visual representation and user interaction for fields.
/// </remarks>
public abstract class PdfFormField : IFormFieldMouseInteraction, IFormFieldKeyboardInteraction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFormField"/> class.
    /// </summary>
    /// <param name="fieldObject">The PDF object representing this form field.</param>
    /// <param name="fieldType">The field type.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fieldObject"/> is null.</exception>
    protected PdfFormField(PdfObject fieldObject, PdfFormFieldType fieldType)
    {
        FieldObject = fieldObject ?? throw new ArgumentNullException(nameof(fieldObject));
        FieldType = fieldType == PdfFormFieldType.Unknown ? throw new ArgumentException("FieldType cannot be Unknown", nameof(fieldType)) : fieldType;

        var dictionary = fieldObject.Dictionary;

        PartialName = dictionary.GetString(PdfTokens.TitleKey);
        AlternateName = dictionary.GetString(PdfTokens.NameKey);
        MappingName = dictionary.GetString(PdfTokens.NameKey);
        Flags = (PdfFormFieldFlags)dictionary.GetIntegerOrDefault(PdfTokens.FlagsKey);
        Value = dictionary.GetValue(PdfTokens.ValueKey);
        DefaultValue = dictionary.GetValue(PdfTokens.DefaultValueKey);
        Parent = dictionary.GetObject(PdfTokens.ParentKey)?.Reference ?? default;
        Kids = dictionary.GetArray(PdfTokens.KidsKey);
        DefaultAppearance = dictionary.GetString(PdfTokens.DefaultAppearanceKey);
        Quadding = dictionary.GetIntegerOrDefault(PdfTokens.QuadKey);
    }

    /// <summary>
    /// Gets the PDF object that represents this form field.
    /// </summary>
    public PdfObject FieldObject { get; }

    /// <summary>
    /// Gets the field type.
    /// </summary>
    public PdfFormFieldType FieldType { get; }

    /// <summary>
    /// Gets the partial field name.
    /// </summary>
    /// <remarks>
    /// The fully qualified field name is constructed by concatenating the partial names
    /// of all ancestors, separated by periods.
    /// </remarks>
    public PdfString PartialName { get; }

    /// <summary>
    /// Gets the alternate field name for user interface purposes.
    /// </summary>
    /// <remarks>
    /// This is the name displayed to the user in tooltips or dialogs.
    /// </remarks>
    public PdfString AlternateName { get; }

    /// <summary>
    /// Gets the mapping name for export operations.
    /// </summary>
    public PdfString MappingName { get; }

    /// <summary>
    /// Gets the field flags.
    /// </summary>
    public PdfFormFieldFlags Flags { get; }

    /// <summary>
    /// Gets the field value.
    /// </summary>
    /// <remarks>
    /// The type and format depend on the field type.
    /// </remarks>
    public IPdfValue Value { get; }

    /// <summary>
    /// Gets the default field value.
    /// </summary>
    public IPdfValue DefaultValue { get; }

    /// <summary>
    /// Gets the reference to the parent field.
    /// </summary>
    /// <remarks>
    /// Form fields can be organized hierarchically. Terminal fields have widgets.
    /// </remarks>
    public PdfReference Parent { get; }

    /// <summary>
    /// Gets the array of child fields or widget annotations.
    /// </summary>
    public PdfArray Kids { get; }

    /// <summary>
    /// Gets the default appearance string.
    /// </summary>
    /// <remarks>
    /// Contains instructions for generating the field's appearance, including font, size, and color.
    /// </remarks>
    public PdfString DefaultAppearance { get; }

    /// <summary>
    /// Gets the quadding (text alignment) value.
    /// </summary>
    /// <remarks>
    /// 0 = left-justified, 1 = centered, 2 = right-justified.
    /// </remarks>
    public int Quadding { get; }

    /// <summary>
    /// Gets a value indicating whether this field is read-only.
    /// </summary>
    public bool IsReadOnly => Flags.HasFlag(PdfFormFieldFlags.ReadOnly);

    /// <summary>
    /// Gets a value indicating whether this field is required.
    /// </summary>
    public bool IsRequired => Flags.HasFlag(PdfFormFieldFlags.Required);

    /// <summary>
    /// Gets the fully qualified field name.
    /// </summary>
    /// <returns>The fully qualified field name.</returns>
    public virtual string GetFullyQualifiedName()
    {
        return PartialName.ToString();
    }

    /// <summary>
    /// Handles mouse down event on the form field.
    /// </summary>
    /// <param name="position">Mouse position in PDF coordinates relative to the field's rectangle.</param>
    /// <param name="state">Current pointer state.</param>
    /// <returns>True if the event was handled.</returns>
    public virtual bool OnMouseDown(SKPoint position, FormFieldPointerState state)
    {
        return false;
    }

    /// <summary>
    /// Handles mouse up event on the form field.
    /// </summary>
    /// <param name="position">Mouse position in PDF coordinates relative to the field's rectangle.</param>
    /// <param name="state">Current pointer state.</param>
    /// <returns>True if the event was handled.</returns>
    public virtual bool OnMouseUp(SKPoint position, FormFieldPointerState state)
    {
        return false;
    }

    /// <summary>
    /// Handles mouse move event on the form field.
    /// </summary>
    /// <param name="position">Mouse position in PDF coordinates relative to the field's rectangle.</param>
    /// <param name="state">Current pointer state.</param>
    /// <returns>True if the event was handled.</returns>
    public virtual bool OnMouseMove(SKPoint position, FormFieldPointerState state)
    {
        return false;
    }

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
    public virtual bool OnKeyDown(FormFieldKey key, FormFieldKeyModifiers modifiers)
    {
        return false;
    }

    /// <summary>
    /// Handles text input event on the form field.
    /// </summary>
    /// <param name="text">The text that was input.</param>
    /// <returns>True if the event was handled.</returns>
    public virtual bool OnTextInput(string text)
    {
        return false;
    }

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
    public virtual void Blur()
    {
        HasFocus = false;
    }
}
