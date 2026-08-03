using PdfPixel.Models;
using PdfPixel.Text;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Annotations.Rendering;
using PdfPixel.Commands;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using System;
using PdfPixel.Geometry;

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
        Subtype = subtype;

        // Initialize all properties in constructor to avoid re-parsing
        Rectangle = PdfRectangle.FromArray(annotationObject.Dictionary.GetArray(PdfTokens.RectKey)) ?? PdfRectangle.Empty;
        RichContents = annotationObject.Dictionary.GetString(PdfTokens.RichContentsKey);

        PdfString contents = annotationObject.Dictionary.GetString(PdfTokens.ContentsKey);
        Contents = (contents.IsEmpty)
            ? PdfRichTextContentParser.ExtractPlainText(RichContents)
            : contents;
        Name = annotationObject.Dictionary.GetString(PdfTokens.NameKey);

        PdfString modDateString = annotationObject.Dictionary.GetString(PdfTokens.ModificationDateKey);
        ModificationDate = PdfDateParser.ParsePdfDate(modDateString);

        // Parse additional annotation metadata
        Title = annotationObject.Dictionary.GetString(PdfTokens.TitleKey);
        Subject = annotationObject.Dictionary.GetString(PdfTokens.SubjectKey);

        PdfString creationDateString = annotationObject.Dictionary.GetString(PdfTokens.CreationDateKey);
        CreationDate = PdfDateParser.ParsePdfDate(creationDateString);

        Flags = (PdfAnnotationFlags)annotationObject.Dictionary.GetIntegerOrDefault(PdfTokens.FlagsKey);
        AppearanceDictionary = annotationObject.Dictionary.GetDictionary(PdfTokens.AppearanceKey);
        AppearanceState = annotationObject.Dictionary.GetString(PdfTokens.AppearanceStateKey);

        PdfDictionary? borderStyleDict = annotationObject.Dictionary.GetDictionary(PdfTokens.BorderStyleKey);
        PdfArray? borderArray = annotationObject.Dictionary.GetArray(PdfTokens.BorderKey);
        BorderStyle = PdfAnnotationBorderParser.ParseBorderStyle(borderStyleDict, borderArray);

        Color = annotationObject.Dictionary.GetArray(PdfTokens.ColorKey)?.GetFloatArray();
        InteriorColor = annotationObject.Dictionary.GetArray(PdfTokens.InteriorColorKey)?.GetFloatArray();
        Opacity = annotationObject.Dictionary.GetFloat(PdfTokens.StrokeAlphaKey) ?? 1.0f;
        PageReference = annotationObject.Dictionary.GetObject(PdfTokens.PageKey)?.Reference;
        StructuralParent = annotationObject.Dictionary.GetInteger(PdfTokens.StructParentKey);
        OptionalContent = annotationObject.Dictionary.GetDictionary(PdfTokens.OptionalContentKey);
        Popup = annotationObject.Dictionary.GetObject(PdfTokens.PopupKey)?.Reference;
        InReplyTo = annotationObject.Dictionary.GetObject(PdfTokens.InReplyToKey)?.Reference;
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
    public virtual PdfRectangle Rectangle { get; }

    /// <summary>
    /// Starting point of actual content.
    /// </summary>
    protected virtual PdfPoint ContentStart => new(Rectangle.Left, Rectangle.Bottom);

    /// <summary>
    /// Whether fallback rendering is wrapped in a translucent layer carrying <see cref="Opacity"/>, so that
    /// overlapping parts of the annotation fade as one instead of stacking on each other.
    /// </summary>
    protected virtual bool UsesOpacityLayer => Opacity < 1.0f;

    /// <summary>
    /// Gets whether this annotation should display a content bubble indicator.
    /// </summary>
    /// <remarks>
    /// When true, indicates that the annotation has content (like comments) that should
    /// be accessible through a bubble indicator. The HoverRectangle will be the bubble area only.
    /// </remarks>
    public virtual bool ShouldDisplayBubble => !Contents.Value.IsEmpty;

    /// <summary>
    /// Gets the pointer cursor to display when hovering over this annotation.
    /// </summary>
    public virtual PdfAnnotationCursorType CursorType => IsInteractive ? PdfAnnotationCursorType.Hand : PdfAnnotationCursorType.Arrow;

    /// <summary>
    /// Gets whether this annotation responds to pointer interaction with a visual state change.
    /// </summary>
    /// <remarks>
    /// True when the appearance stream defines rollover or down states, or when the annotation
    /// displays a bubble (which has its own hover rendering). Subclasses with fallback interactive
    /// rendering should override this to return true unconditionally.
    /// </remarks>
    public virtual bool IsInteractive
    {
        get
        {
            return (SupportedVisualStates & (PdfAnnotationVisualStateKind.Rollover | PdfAnnotationVisualStateKind.Down)) != 0
                || ShouldDisplayBubble;
        }
    }

    /// <summary>
    /// Gets the annotation's contents, which is typically the text displayed
    /// for the annotation or associated with it.
    /// </summary>
    public PdfString Contents { get; }

    /// <summary>
    /// Gets the annotation's rich text contents (the RC entry), an XHTML-subset
    /// markup representation of <see cref="Contents"/>.
    /// </summary>
    public PdfString RichContents { get; }

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
    public PdfDictionary? AppearanceDictionary { get; }

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
    public PdfAnnotationBorderStyle? BorderStyle { get; }

    /// <summary>
    /// Gets the color array that specifies the annotation's color.
    /// </summary>
    public float[]? Color { get; }

    /// <summary>
    /// Gets the interior color array that specifies the annotation's fill color.
    /// </summary>
    /// <remarks>
    /// Used by annotations that support filled shapes (Circle, Square, Line, Polygon, etc.).
    /// The array format depends on the color space (grayscale, RGB, or CMYK).
    /// </remarks>
    public float[]? InteriorColor { get; }

    /// <summary>
    /// Gets the constant opacity value for the annotation (CA entry). Applied to all fallback-rendered
    /// content. Default is 1.0 (fully opaque).
    /// </summary>
    public float Opacity { get; }

    /// <summary>
    /// Gets the reference to the popup annotation associated with this markup annotation, if any.
    /// </summary>
    public PdfReference? Popup { get; }

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
    public PdfDictionary? OptionalContent { get; }

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
    /// Gets the hover rectangle for interaction purposes (hit testing, popups, etc.).
    /// </summary>
    /// <remarks>
    /// If ShouldDisplayBubble is true, this returns a small rectangle for the bubble indicator
    /// positioned just above and to the left of the annotation content. The bubble does not
    /// overlap the annotation's own Rectangle. Coordinates are in PDF space where the origin
    /// is at the bottom-left of the page.
    /// </remarks>
    /// <param name="page">Owning PDF page to crop margins to.</param>
    public virtual PdfRectangle GetHoverRectangle(IPdfPage page)
    {
        if (page == null)
        {
            throw new ArgumentNullException(nameof(page));
        }

        PdfRectangle hoverRect = ShouldDisplayBubble
            ? new PdfRectangle(ContentStart.X - PdfAnnotationGraphics.DefaultBubbleSize, ContentStart.Y, ContentStart.X, ContentStart.Y + PdfAnnotationGraphics.DefaultBubbleSize)
            : Rectangle;

        if (hoverRect.Width <= 0 || hoverRect.Height <= 0)
        {
            return PdfRectangle.Empty;
        }

        PdfRectangle crop = page.CropBox;

        float dx = 0f;
        if (hoverRect.Left < crop.Left)
        {
            dx = crop.Left - hoverRect.Left;
        }
        else if (hoverRect.Right > crop.Right)
        {
            dx = crop.Right - hoverRect.Right;
        }

        float dy = 0f;
        if (hoverRect.Top < crop.Top)
        {
            dy = crop.Top - hoverRect.Top;
        }
        else if (hoverRect.Bottom > crop.Bottom)
        {
            dy = crop.Bottom - hoverRect.Bottom;
        }

        return new PdfRectangle(hoverRect.Left + dx, hoverRect.Top + dy, hoverRect.Right + dx, hoverRect.Bottom + dy);
    }

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
    /// Renders this annotation via the command processor.
    /// </summary>
    /// <param name="processor">The command processor to emit commands to.</param>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render (Normal, Rollover, Down).</param>
    /// <param name="renderer">The renderer context for rendering appearance streams.</param>
    /// <param name="renderingParameters">Parameters for PDF page rendering.</param>
    /// <param name="observer">Observer for long-running operations.</param>
    /// <returns>True if the annotation was rendered, false otherwise.</returns>
    internal virtual bool Render(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        IPdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        if (ShouldDisplayBubble)
        {
            PdfAnnotationIconDefinition? bubbleIcon = PdfAnnotationGraphics.GetAnnotationBubbleIcon(visualStateKind);

            if (bubbleIcon != null)
            {
                PdfColor borderColor = ResolveColor(page, PdfAnnotationGraphics.DefaultBubbleBorderColor);
                PdfColor backgroundColor = ResolveInteriorColor(page, PdfAnnotationGraphics.DefaultBubbleBackgroundColor);
                PdfAnnotationGraphics.RenderIcon(processor, bubbleIcon, GetHoverRectangle(page), borderColor, backgroundColor);
            }
        }

        processor.Process(SaveStateCommand.Instance);

        if (AppearanceDictionary != null)
        {
            processor.Process(new ClipRectangleCommand(Rectangle, PdfClipOperation.Intersect));

            if (RenderAppearanceStream(processor, page, visualStateKind, renderer, renderingParameters, observer))
            {
                processor.Process(RestoreStateCommand.Instance);
                return true;
            }
        }

        bool useOpacityLayer = UsesOpacityLayer;
        if (useOpacityLayer)
        {
            processor.Process(new SaveLayerCommand(Rectangle, PdfAnnotationPaintFactory.CreateOpacityLayerPaint(Opacity)));
        }

        bool rendered = RenderFallback(processor, page, visualStateKind);

        if (useOpacityLayer)
        {
            processor.Process(RestoreLayerCommand.Instance);
        }

        processor.Process(RestoreStateCommand.Instance);

        return rendered;
    }

    /// <summary>
    /// Renders the fallback content for this annotation when no appearance stream is available.
    /// </summary>
    /// <param name="processor">The command processor to emit commands to.</param>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render (Normal, Rollover, Down).</param>
    /// <returns>True if fallback rendering was emitted, false if no fallback is available.</returns>
    /// <remarks>
    /// This method allows each annotation type to provide its own custom rendering logic
    /// when the annotation doesn't have an appearance stream.
    /// The visual state allows annotations to change their appearance based on user interaction.
    /// </remarks>
    internal abstract bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind);

    /// <summary>
    /// Renders the appearance stream for this annotation.
    /// </summary>
    /// <param name="processor">The command processor to emit commands to.</param>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render.</param>
    /// <param name="renderer">The renderer context.</param>
    /// <param name="renderingParameters">Parameters for PDF page rendering.</param>
    /// <param name="observer">Observer for long-running operations.</param>
    /// <returns>True if the appearance stream was rendered successfully.</returns>
    internal virtual bool RenderAppearanceStream(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        IPdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        return PdfAnnotationAppearanceRenderer.RenderAppearanceStream(
            processor,
            this,
            page,
            visualStateKind,
            renderer,
            renderingParameters,
            observer);
    }

    /// <summary>
    /// Returns the annotation rectangle after applying RD (RectDifferences) insets, or the original
    /// rectangle unchanged when <paramref name="differences"/> is null.
    /// </summary>
    protected static PdfRectangle ApplyRectDifferences(in PdfRectangle rect, PdfRectangle? differences)
    {
        if (differences == null)
        {
            return rect;
        }

        return new PdfRectangle(
            rect.Left + differences.Value.Left,
            rect.Top + differences.Value.Top,
            rect.Right - differences.Value.Right,
            rect.Bottom - differences.Value.Bottom);
    }

    /// <summary>
    /// Resolves the annotation color using proper color space conversion.
    /// </summary>
    /// <param name="page">The PDF page for color space resolution.</param>
    /// <param name="defaultColor">Default color to use if annotation has no color specified. If null, returns transparent.</param>
    /// <returns>The resolved color for rendering.</returns>
    internal PdfColor ResolveColor(IPdfPageInternal page, PdfColor? defaultColor = null) => PdfAnnotationColorResolver.ResolveColor(Color, page, defaultColor);

    /// <summary>
    /// Resolves the annotation interior color using proper color space conversion.
    /// </summary>
    /// <param name="page">The PDF page for color space resolution.</param>
    /// <param name="defaultColor">Default color to use if annotation has no interior color specified. If null, returns transparent.</param>
    /// <returns>The resolved color for rendering.</returns>
    internal PdfColor ResolveInteriorColor(IPdfPageInternal page, PdfColor? defaultColor = null) => PdfAnnotationColorResolver.ResolveColor(InteriorColor, page, defaultColor);

    /// <summary>
    /// Returns a string representation of this annotation.
    /// </summary>
    /// <returns>A string containing the annotation subtype and basic information.</returns>
    public override string ToString()
    {
        string contentsText = Contents.ToString();
        string nameText = Name.ToString();

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
