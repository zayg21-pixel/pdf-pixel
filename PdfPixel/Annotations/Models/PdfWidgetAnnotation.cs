using PdfPixel.Annotations.Models;
using PdfPixel.Forms;
using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
using System;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Cursor types for widget annotations.
/// </summary>
public enum WidgetCursorType
{
    /// <summary>
    /// Default arrow cursor.
    /// </summary>
    Arrow,

    /// <summary>
    /// Hand cursor for clickable widgets.
    /// </summary>
    Hand,

    /// <summary>
    /// Text input cursor (I-beam) for text fields.
    /// </summary>
    IBeam
}

/// <summary>
/// Represents a PDF widget annotation (form field visual representation).
/// </summary>
/// <remarks>
/// Widget annotations provide the visual appearance and user interaction for form fields.
/// They are typically associated with a parent form field that contains the field's data and behavior.
/// </remarks>
public class PdfWidgetAnnotation : PdfAnnotationBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfWidgetAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this widget annotation.</param>
    public PdfWidgetAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Widget)
    {
        var dictionary = annotationObject.Dictionary;

        HighlightMode = dictionary.GetName(PdfTokens.HighlightModeKey);
        AppearanceCharacteristics = dictionary.GetDictionary(PdfTokens.AppearanceCharacteristicsKey);
        Action = PdfAction.FromDictionary(dictionary.GetDictionary(PdfTokens.AKey));

        Field = ResolveField();
    }

    /// <summary>
    /// Gets the highlighting mode.
    /// </summary>
    /// <remarks>
    /// Valid values: N (None), I (Invert), O (Outline), P (Push), T (Toggle).
    /// Default is I (Invert).
    /// </remarks>
    public PdfString HighlightMode { get; }

    /// <summary>
    /// Gets the appearance characteristics dictionary.
    /// </summary>
    /// <remarks>
    /// Contains information for constructing the widget's appearance, such as
    /// background color, border color, caption text, and icon fit.
    /// </remarks>
    public PdfDictionary AppearanceCharacteristics { get; }

    /// <summary>
    /// Gets the action to be performed when the widget is activated.
    /// </summary>
    public PdfAction Action { get; }

    /// <summary>
    /// Gets the associated form field.
    /// </summary>
    /// <remarks>
    /// Widget annotations are linked to form fields. This property resolves the field
    /// by checking if the widget object itself is a field or if it has a parent field.
    /// </remarks>
    public PdfFormField Field { get; }

    /// <summary>
    /// Gets a value indicating whether this widget should not display a content bubble.
    /// </summary>
    /// <remarks>
    /// Widget annotations typically don't show bubbles since they are interactive form elements.
    /// </remarks>
    public override bool ShouldDisplayBubble => false;

    /// <summary>
    /// Creates a fallback rendering for widget annotations when no appearance stream is available.
    /// </summary>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render (Normal, Rollover, Down).</param>
    /// <returns>An empty SKPicture since widgets rely on appearance streams.</returns>
    /// <remarks>
    /// Widget annotations should always have appearance streams defined.
    /// This method returns null.
    /// </remarks>
    public override SKPicture CreateFallbackRender(PdfPage page, PdfAnnotationVisualStateKind visualStateKind)
    {
        return null;
    }

    /// <summary>
    /// Gets the text value for text field widgets.
    /// </summary>
    /// <returns>The text value, or an empty string if not a text field or no value is set.</returns>
    public string GetTextValue()
    {
        if (Field is PdfTextFormField textField)
        {
            return textField.GetTextValue();
        }

        return string.Empty;
    }

    /// <summary>
    /// Checks if this widget represents a checked button (checkbox or radio button).
    /// </summary>
    /// <returns>True if the button is checked, false otherwise.</returns>
    public bool IsButtonChecked()
    {
        if (Field is PdfButtonFormField buttonField)
        {
            return buttonField.IsChecked();
        }

        return false;
    }

    /// <summary>
    /// Returns a string representation of this widget annotation.
    /// </summary>
    /// <returns>A string containing the annotation type and field information.</returns>
    public override string ToString()
    {
        if (Field != null)
        {
            var fieldName = Field.PartialName.ToString();
            if (!string.IsNullOrEmpty(fieldName))
            {
                return $"Widget Annotation: {fieldName} ({Field.FieldType})";
            }

            return $"Widget Annotation: {Field.FieldType}";
        }

        return "Widget Annotation";
    }

    /// <summary>
    /// Propagates mouse down event to the associated form field.
    /// </summary>
    /// <param name="position">Mouse position in PDF page coordinates.</param>
    /// <param name="state">Current pointer state.</param>
    /// <returns>True if the event was handled by the field.</returns>
    public bool HandleMouseDown(SKPoint position, FormFieldPointerState state)
    {
        if (Field == null)
        {
            return false;
        }

        SKPoint relativePosition = GetRelativePosition(position);
        return Field.OnMouseDown(relativePosition, state);
    }

    /// <summary>
    /// Propagates mouse up event to the associated form field.
    /// </summary>
    /// <param name="position">Mouse position in PDF page coordinates.</param>
    /// <param name="state">Current pointer state.</param>
    /// <returns>True if the event was handled by the field.</returns>
    public bool HandleMouseUp(SKPoint position, FormFieldPointerState state)
    {
        if (Field == null)
        {
            return false;
        }

        SKPoint relativePosition = GetRelativePosition(position);
        return Field.OnMouseUp(relativePosition, state);
    }

    /// <summary>
    /// Propagates mouse move event to the associated form field.
    /// </summary>
    /// <param name="position">Mouse position in PDF page coordinates.</param>
    /// <param name="state">Current pointer state.</param>
    /// <returns>True if the event was handled by the field.</returns>
    public bool HandleMouseMove(SKPoint position, FormFieldPointerState state)
    {
        if (Field == null)
        {
            return false;
        }

        SKPoint relativePosition = GetRelativePosition(position);
        return Field.OnMouseMove(relativePosition, state);
    }

    /// <summary>
    /// Propagates mouse enter event to the associated form field.
    /// </summary>
    public void HandleMouseEnter()
    {
        Field?.OnMouseEnter();
    }

    /// <summary>
    /// Propagates mouse leave event to the associated form field.
    /// </summary>
    public void HandleMouseLeave()
    {
        Field?.OnMouseLeave();
    }

    /// <summary>
    /// Propagates key down event to the associated form field.
    /// </summary>
    /// <param name="key">The key that was pressed.</param>
    /// <param name="modifiers">Keyboard modifiers.</param>
    /// <returns>True if the event was handled by the field.</returns>
    public bool HandleKeyDown(FormFieldKey key, FormFieldKeyModifiers modifiers)
    {
        if (Field == null)
        {
            return false;
        }

        return Field.OnKeyDown(key, modifiers);
    }

    /// <summary>
    /// Propagates text input event to the associated form field.
    /// </summary>
    /// <param name="text">The text that was input.</param>
    /// <returns>True if the event was handled by the field.</returns>
    public bool HandleTextInput(string text)
    {
        if (Field == null)
        {
            return false;
        }

        return Field.OnTextInput(text);
    }

    /// <summary>
    /// Gets a value indicating whether the widget's field can receive keyboard focus.
    /// </summary>
    public bool CanReceiveFocus => Field?.CanReceiveFocus ?? false;

    /// <summary>
    /// Gets a value indicating whether the widget's field currently has keyboard focus.
    /// </summary>
    public bool HasFocus => Field?.HasFocus ?? false;

    /// <summary>
    /// Sets focus to the widget's field.
    /// </summary>
    public void Focus()
    {
        Field?.Focus();
    }

    /// <summary>
    /// Removes focus from the widget's field.
    /// </summary>
    public void Blur()
    {
        Field?.Blur();
    }

    /// <summary>
    /// Gets the cursor type that should be displayed when hovering over this widget.
    /// </summary>
    /// <returns>The appropriate cursor type for this widget.</returns>
    public WidgetCursorType GetCursorType()
    {
        if (Field == null || Field.IsReadOnly)
        {
            return WidgetCursorType.Arrow;
        }

        return Field.FieldType switch
        {
            PdfFormFieldType.Text => WidgetCursorType.IBeam,
            PdfFormFieldType.Button => WidgetCursorType.Hand,
            PdfFormFieldType.Choice => WidgetCursorType.Hand,
            _ => WidgetCursorType.Arrow
        };
    }

    /// <summary>
    /// Converts a position in PDF page coordinates to coordinates relative to the widget's rectangle.
    /// </summary>
    /// <param name="pagePosition">Position in PDF page coordinates (bottom-left origin, Y up).</param>
    /// <returns>Position relative to the widget's rectangle (top-left origin, Y down).</returns>
    /// <remarks>
    /// PDF uses bottom-left origin with Y increasing upward, but form fields internally
    /// use top-left origin with Y increasing downward (like UI elements).
    /// This method converts from PDF page coordinates to field-relative UI coordinates.
    /// </remarks>
    private SKPoint GetRelativePosition(SKPoint pagePosition)
    {
        return new SKPoint(
            pagePosition.X - Rectangle.Left,
            Rectangle.Top - pagePosition.Y);
    }

    /// <summary>
    /// Resolves the form field associated with this widget annotation.
    /// </summary>
    /// <returns>The form field, or null if no field could be resolved.</returns>
    private PdfFormField ResolveField()
    {
        var dictionary = AnnotationObject.Dictionary;
        var fieldType = dictionary.GetName(PdfTokens.FieldTypeKey);

        if (!fieldType.IsEmpty)
        {
            return PdfFormFieldFactory.CreateField(AnnotationObject);
        }

        var parentObject = dictionary.GetObject(PdfTokens.ParentKey);
        if (parentObject != null)
        {
            return PdfFormFieldFactory.CreateField(parentObject);
        }

        return null;
    }
}
