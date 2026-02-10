using PdfPixel.Models;
using PdfPixel.Text;
using PdfPixel.Rendering.Operators;
using PdfPixel.Parsing;
using PdfPixel.Color.ColorSpace;
using SkiaSharp;
using System;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Base class for all PDF annotations containing common fields and properties.
/// </summary>
/// <remarks>
/// This class provides the foundation for all PDF annotation types as defined in 
/// the PDF specification. All annotations share a common set of properties including
/// position, appearance, and metadata.
/// </remarks>
public abstract class PdfAnnotationBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfAnnotationBase"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this annotation.</param>
    /// <param name="subtype">The annotation subtype.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="annotationObject"/> is null.</exception>
    protected PdfAnnotationBase(PdfObject annotationObject, PdfAnnotationSubType subtype)
    {
        AnnotationObject = annotationObject ?? throw new ArgumentNullException(nameof(annotationObject));
        Subtype = subtype == PdfAnnotationSubType.Unknown ? throw new ArgumentException("Subtype cannot be Unknown", nameof(subtype)) : subtype;
        
        // Initialize all properties in constructor to avoid re-parsing
        Rectangle = PdfLocationUtilities.CreateBBox(annotationObject.Dictionary.GetArray(PdfTokens.RectKey)) ?? SKRect.Empty;
        Contents = annotationObject.Dictionary.GetString(PdfTokens.ContentsKey);
        Name = annotationObject.Dictionary.GetString(PdfTokens.NameKey);
        
        var modDateString = annotationObject.Dictionary.GetString(PdfTokens.ModificationDateKey);
        ModificationDate = PdfDateParser.ParsePdfDate(modDateString);
        
        // Parse additional annotation metadata
        Title = annotationObject.Dictionary.GetString(PdfTokens.TitleKey);
        Subject = annotationObject.Dictionary.GetString(PdfTokens.SubjectKey);
        
        var creationDateString = annotationObject.Dictionary.GetString(PdfTokens.CreationDateKey);
        CreationDate = PdfDateParser.ParsePdfDate(creationDateString);
        
        Flags = (PdfAnnotationFlags)annotationObject.Dictionary.GetIntegerOrDefault(PdfTokens.FlagsKey);
        AppearanceDictionary = annotationObject.Dictionary.GetDictionary(PdfTokens.AppearanceKey);
        AppearanceState = annotationObject.Dictionary.GetString(PdfTokens.AppearanceStateKey);
        BorderStyle = annotationObject.Dictionary.GetDictionary(PdfTokens.BorderStyleKey);
        Color = annotationObject.Dictionary.GetArray(PdfTokens.ColorKey)?.GetFloatArray();
        PageReference = annotationObject.Dictionary.GetValue(PdfTokens.PageKey)?.AsReference();
        StructuralParent = annotationObject.Dictionary.GetInteger(PdfTokens.StructParentKey);
        OptionalContent = annotationObject.Dictionary.GetDictionary(PdfTokens.OptionalContentKey);
    }

    /// <summary>
    /// Gets the PDF object that represents this annotation.
    /// </summary>
    public PdfObject AnnotationObject { get; }

    /// <summary>
    /// Gets the annotation subtype (e.g., Text, Link, Widget, etc.).
    /// </summary>
    public PdfAnnotationSubType Subtype { get; }

    /// <summary>
    /// Gets the rectangle defining the annotation's location on the page.
    /// </summary>
    /// <remarks>
    /// The rectangle is specified in default user space coordinates and 
    /// represents the annotation's bounding box.
    /// </remarks>
    public SKRect Rectangle { get; }

    /// <summary>
    /// Gets the annotation's contents, which is typically the text displayed 
    /// for the annotation or associated with it.
    /// </summary>
    public PdfString Contents { get; }

    /// <summary>
    /// Gets the annotation's name, a text string uniquely identifying it among 
    /// all the annotations on its page.
    /// </summary>
    public PdfString Name { get; }

    /// <summary>
    /// Gets the annotation title/author.
    /// </summary>
    /// <remarks>
    /// The title is typically used to identify the author or creator of the annotation.
    /// </remarks>
    public PdfString Title { get; }

    /// <summary>
    /// Gets the annotation subject.
    /// </summary>
    /// <remarks>
    /// The subject represents a short description of the subject being addressed by the annotation.
    /// </remarks>
    public PdfString Subject { get; }

    /// <summary>
    /// Gets the creation date of the annotation.
    /// </summary>
    /// <remarks>
    /// The creation date represents when the annotation was first created.
    /// Returns null if the date is not present or could not be parsed.
    /// </remarks>
    public DateTime? CreationDate { get; }

    /// <summary>
    /// Gets the modification date of the annotation.
    /// </summary>
    /// <remarks>
    /// The modification date represents when the annotation was last modified.
    /// Returns null if the date is not present or could not be parsed.
    /// </remarks>
    public DateTime? ModificationDate { get; }

    /// <summary>
    /// Gets the annotation flags that specify various characteristics of the annotation.
    /// </summary>
    /// <remarks>
    /// Flags include: Invisible, Hidden, Print, NoZoom, NoRotate, NoView, ReadOnly, etc.
    /// </remarks>
    public PdfAnnotationFlags Flags { get; }

    /// <summary>
    /// Gets the appearance dictionary that specifies how the annotation is presented visually on the page.
    /// </summary>
    public PdfDictionary AppearanceDictionary { get; }

    /// <summary>
    /// Gets the appearance state that, along with the appearance dictionary, controls 
    /// the annotation's appearance.
    /// </summary>
    public PdfString AppearanceState { get; }

    /// <summary>
    /// Gets the border style dictionary that specifies the characteristics of the annotation's border.
    /// </summary>
    public PdfDictionary BorderStyle { get; }

    /// <summary>
    /// Gets the color array that specifies the annotation's color.
    /// </summary>
    public float[] Color { get; }

    /// <summary>
    /// Gets the page reference that specifies which page this annotation appears on.
    /// </summary>
    /// <remarks>
    /// This is typically an indirect reference to a page object. If not present,
    /// the annotation is associated with the page that contains it.
    /// </remarks>
    public PdfReference? PageReference { get; }

    /// <summary>
    /// Gets the structural parent key that indicates this annotation's structural parent 
    /// in the document's structure tree.
    /// </summary>
    public int? StructuralParent { get; }

    /// <summary>
    /// Gets the optional content configuration dictionary that determines when this annotation is visible.
    /// </summary>
    public PdfDictionary OptionalContent { get; }

    /// <summary>
    /// Creates a fallback rendering for this annotation when no appearance stream is available.
    /// </summary>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <returns>An SKPicture containing the fallback rendering, or null if no fallback is available.</returns>
    /// <remarks>
    /// This method allows each annotation type to provide its own custom rendering logic
    /// when the annotation doesn't have an appearance stream. The returned SKPicture
    /// should be scaled and positioned appropriately for the annotation's rectangle.
    /// </remarks>
    public abstract SKPicture CreateFallbackRender(PdfPage page);

    /// <summary>
    /// Resolves the annotation color using proper color space conversion.
    /// </summary>
    /// <param name="page">The PDF page for color space resolution.</param>
    /// <param name="defaultColor">Default color to use if annotation has no color specified. If null, returns transparent.</param>
    /// <returns>The resolved SKColor for rendering.</returns>
    internal SKColor ResolveColor(PdfPage page, SKColor? defaultColor = null)
    {
        if (Color == null || Color.Length == 0)
        {
            return defaultColor ?? SKColors.Transparent;
        }

        // Get the appropriate color space converter based on component count
        var converter = page.Cache.ColorSpace.ResolveDeviceConverter(Color.Length);
        if (converter == null)
        {
            // Fallback to DeviceRGB for unknown component counts
            converter = page.Cache.ColorSpace.ResolveDeviceConverter(3);
            var paddedColor = Color;
            Array.Resize(ref paddedColor, 3);
            return converter.ToSrgb(paddedColor, PdfRenderingIntent.RelativeColorimetric, null);
        }

        return converter.ToSrgb(Color, PdfRenderingIntent.RelativeColorimetric, null);
    }

    /// <summary>
    /// Returns a string representation of this annotation.
    /// </summary>
    /// <returns>A string containing the annotation subtype and basic information.</returns>
    public override string ToString()
    {
        var contentsText = Contents.ToString();
        var nameText = Name.ToString();
        
        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"{Subtype} Annotation: {contentsText}";
        }
        
        if (!string.IsNullOrEmpty(nameText))
        {
            return $"{Subtype} Annotation: {nameText}";
        }
        
        return $"{Subtype} Annotation";
    }
}