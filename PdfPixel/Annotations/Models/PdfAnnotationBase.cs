using PdfPixel.Models;
using PdfPixel.Text;
using PdfPixel.Rendering.Operators;
using PdfPixel.Parsing;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Rendering;
using PdfPixel.Annotations.Rendering;
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

        var borderStyleDict = annotationObject.Dictionary.GetDictionary(PdfTokens.BorderStyleKey);
        var borderArray = annotationObject.Dictionary.GetArray(PdfTokens.BorderKey);
        BorderStyle = PdfBorderStyle.FromDictionary(borderStyleDict, borderArray);

        Color = annotationObject.Dictionary.GetArray(PdfTokens.ColorKey)?.GetFloatArray();
        InteriorColor = annotationObject.Dictionary.GetArray(PdfTokens.InteriorColorKey)?.GetFloatArray();
        PageReference = annotationObject.Dictionary.GetValue(PdfTokens.PageKey)?.AsReference();
        StructuralParent = annotationObject.Dictionary.GetInteger(PdfTokens.StructParentKey);
        OptionalContent = annotationObject.Dictionary.GetDictionary(PdfTokens.OptionalContentKey);
        InReplyTo = annotationObject.Dictionary.GetValue(PdfTokens.InReplyToKey)?.AsReference();
        ReplyType = annotationObject.Dictionary.GetName(PdfTokens.ReplyTypeKey).AsEnum<PdfAnnotationReplyType>();
        SupportedVisualStates = DetectSupportedVisualStates();
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
    /// Gets whether this annotation should display a content bubble indicator.
    /// </summary>
    /// <remarks>
    /// When true, indicates that the annotation has content (like comments) that should
    /// be accessible through a bubble indicator. The HoverRectangle will be the bubble area only.
    /// </remarks>
    public virtual bool ShouldDisplayBubble => !Contents.Value.IsEmpty;

    /// <summary>
    /// Gets the hover rectangle for interaction purposes (hit testing, popups, etc.).
    /// </summary>
    /// <remarks>
    /// If ShouldDisplayBubble is true, this returns a small rectangle for the bubble indicator
    /// positioned just above and to the left of the annotation content. The bubble does not
    /// overlap the annotation's own Rectangle. Coordinates are in PDF space where the origin
    /// is at the bottom-left of the page.
    /// </remarks>
    public SKRect HoverRectangle
    {
        get
        {
            if (!ShouldDisplayBubble)
            {
                return Rectangle;
            }

            const float bubbleSize = 16.0f;

            var bubbleLeft = Rectangle.Left - bubbleSize;
            var bubbleTop = Rectangle.Bottom;

            return SKRect.Create(
                bubbleLeft,
                bubbleTop,
                bubbleSize,
                bubbleSize);
        }
    }

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
    /// <remarks>
    /// The border style includes width, style type (Solid, Dashed, Beveled, Inset, Underline),
    /// and dash pattern for dashed borders. This is parsed from the BS (Border Style) dictionary
    /// or the older Border array entry. Returns null if no border information is present.
    /// </remarks>
    public PdfBorderStyle BorderStyle { get; }

    /// <summary>
    /// Gets the color array that specifies the annotation's color.
    /// </summary>
    public float[] Color { get; }

    /// <summary>
    /// Gets the interior color array that specifies the annotation's fill color.
    /// </summary>
    /// <remarks>
    /// Used by annotations that support filled shapes (Circle, Square, Line, Polygon, etc.).
    /// The array format depends on the color space (grayscale, RGB, or CMYK).
    /// </remarks>
    public float[] InteriorColor { get; }

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
    /// Gets the reference to the annotation that this annotation is in reply to.
    /// </summary>
    /// <remarks>
    /// Used to create threaded discussions where annotations can reply to other annotations.
    /// Returns null if this annotation is not a reply.
    /// </remarks>
    public PdfReference? InReplyTo { get; }

    /// <summary>
    /// Gets the reply type indicating the relationship between this annotation and the one specified by InReplyTo.
    /// </summary>
    /// <remarks>
    /// Reply (R) creates a linear thread, Group allows multiple replies to same parent.
    /// Returns None if not specified or if this annotation is not a reply.
    /// </remarks>
    public PdfAnnotationReplyType ReplyType { get; }

    /// <summary>
    /// Gets the visual states supported by this annotation's appearance dictionary.
    /// </summary>
    /// <remarks>
    /// This property indicates which visual states (Normal, Rollover, Down) have appearance streams
    /// defined in the annotation's appearance dictionary. It is used to optimize rendering by
    /// avoiding lookups for states that don't exist.
    /// </remarks>
    public PdfAnnotationVisualStateKind SupportedVisualStates { get; }

    /// <summary>
    /// Detects which visual states are supported by examining the appearance dictionary.
    /// </summary>
    private PdfAnnotationVisualStateKind DetectSupportedVisualStates()
    {
        if (AppearanceDictionary == null)
        {
            return PdfAnnotationVisualStateKind.None;
        }

        var supported = PdfAnnotationVisualStateKind.None;

        if (AppearanceDictionary.HasKey(PdfTokens.NKey))
        {
            supported |= PdfAnnotationVisualStateKind.Normal;
        }

        if (AppearanceDictionary.HasKey(PdfTokens.RolloverKey))
        {
            supported |= PdfAnnotationVisualStateKind.Rollover;
        }

        if (AppearanceDictionary.HasKey(PdfTokens.DownKey))
        {
            supported |= PdfAnnotationVisualStateKind.Down;
        }

        return supported;
    }

    /// <summary>
    /// Renders this annotation to the canvas.
    /// </summary>
    /// <param name="canvas">The canvas to render to.</param>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render (Normal, Rollover, Down).</param>
    /// <param name="renderer">The renderer context for rendering appearance streams.</param>
    /// <param name="renderingParameters">Rendering parameters.</param>
    /// <returns>True if the annotation was rendered, false otherwise.</returns>
    /// <remarks>
    /// This method handles all rendering logic including bubble indicators, appearance streams,
    /// and fallback rendering. Derived classes can override this to customize rendering behavior
    /// such as applying blend modes or special effects.
    /// </remarks>
    public virtual bool Render(
        SKCanvas canvas,
        PdfPage page,
        PdfAnnotationVisualStateKind visualStateKind,
        IPdfRenderer renderer,
        PdfRenderingParameters renderingParameters)
    {
        canvas.Save();

        try
        {
            if (ShouldDisplayBubble)
            {
                PdfAnnotationBubbleRenderer.RenderBubble(canvas, this, page, visualStateKind);
            }

            if (AppearanceDictionary != null && RenderAppearanceStream(canvas, page, visualStateKind, renderer, renderingParameters))
            {
                return true;
            }

            var fallbackPicture = CreateFallbackRender(page, visualStateKind);
            if (fallbackPicture != null)
            {
                canvas.DrawPicture(fallbackPicture);
                return true;
            }

            return false;
        }
        finally
        {
            canvas.Restore();
        }
    }

    /// <summary>
    /// Creates a fallback rendering for this annotation when no appearance stream is available.
    /// </summary>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render (Normal, Rollover, Down).</param>
    /// <returns>An SKPicture containing the fallback rendering, or null if no fallback is available.</returns>
    /// <remarks>
    /// This method allows each annotation type to provide its own custom rendering logic
    /// when the annotation doesn't have an appearance stream. The returned SKPicture
    /// should be scaled and positioned appropriately for the annotation's rectangle.
    /// The visual state allows annotations to change their appearance based on user interaction.
    /// </remarks>
    public abstract SKPicture CreateFallbackRender(PdfPage page, PdfAnnotationVisualStateKind visualStateKind);

    /// <summary>
    /// Renders the appearance stream for this annotation.
    /// </summary>
    /// <param name="canvas">The canvas to render to.</param>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render.</param>
    /// <param name="renderer">The renderer context.</param>
    /// <param name="renderingParameters">Rendering parameters.</param>
    /// <returns>True if the appearance stream was rendered successfully.</returns>
    protected virtual bool RenderAppearanceStream(
        SKCanvas canvas,
        PdfPage page,
        PdfAnnotationVisualStateKind visualStateKind,
        IPdfRenderer renderer,
        PdfRenderingParameters renderingParameters)
    {
        return PdfAnnotationAppearanceRenderer.RenderAppearanceStream(
            canvas,
            this,
            page,
            visualStateKind,
            renderer,
            renderingParameters);
    }

    /// <summary>
    /// Resolves the annotation color using proper color space conversion.
    /// </summary>
    /// <param name="page">The PDF page for color space resolution.</param>
    /// <param name="defaultColor">Default color to use if annotation has no color specified. If null, returns transparent.</param>
    /// <returns>The resolved SKColor for rendering.</returns>
    internal SKColor ResolveColor(PdfPage page, SKColor? defaultColor = null)
    {
        return PdfAnnotationColorResolver.ResolveColor(Color, page, defaultColor);
    }

    /// <summary>
    /// Resolves the annotation interior color using proper color space conversion.
    /// </summary>
    /// <param name="page">The PDF page for color space resolution.</param>
    /// <param name="defaultColor">Default color to use if annotation has no interior color specified. If null, returns transparent.</param>
    /// <returns>The resolved SKColor for rendering.</returns>
    internal SKColor ResolveInteriorColor(PdfPage page, SKColor? defaultColor = null)
    {
        return PdfAnnotationColorResolver.ResolveColor(InteriorColor, page, defaultColor);
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