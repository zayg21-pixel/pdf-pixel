using PdfPixel.Files;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using System;
using System.Collections.Generic;
using PdfPixel.Geometry;
using PdfPixel.Commands;
using PdfPixel.Objects;
using PdfPixel.OptionalContent;
using PdfPixel.Tagging;
using PdfPixel.Transparency;

namespace PdfPixel.Annotations;

/// <summary>
/// Base class for all PDF annotations containing common fields and properties.
/// </summary>
/// <remarks>
/// This class provides the foundation for all PDF annotation types as defined in
/// the PDF specification. All annotations share a common set of properties including
/// position, appearance, and metadata.
/// </remarks>
public abstract class PdfAnnotation : IPdfAssociatedFilesHost
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this annotation.</param>
    /// <param name="subtype">The annotation subtype.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="annotationObject"/> is null.</exception>
    private protected PdfAnnotation(PdfObject annotationObject, PdfAnnotationSubType subtype)
    {
        if (annotationObject == null)
        {
            throw new ArgumentNullException(nameof(annotationObject));
        }

        Reference = annotationObject.Reference;
        Subtype = subtype;

        // Initialize all properties in constructor to avoid re-parsing
        Rectangle = PdfRectangle.FromArray(annotationObject.Dictionary.GetArray(PdfTokens.RectKey)) ?? PdfRectangle.Empty;
        Contents = annotationObject.Dictionary.GetString(PdfTokens.ContentsKey);
        Name = annotationObject.Dictionary.GetString(PdfTokens.AnnotationNameKey);
        ModificationDate = PdfDateParser.ParsePdfDate(annotationObject.Dictionary.GetString(PdfTokens.ModificationDateKey));

        Flags = (PdfAnnotationFlags)annotationObject.Dictionary.GetIntegerOrDefault(PdfTokens.FKey);
        IsHidden = (Flags & PdfAnnotationFlags.Hidden) != 0;
        AppearanceState = annotationObject.Dictionary.GetString(PdfTokens.AppearanceStateKey);
        Appearance = PdfAnnotationAppearance.FromDictionary(annotationObject.Dictionary.GetDictionary(PdfTokens.AppearanceKey), AppearanceState);

        PdfDictionary? borderStyleDict = annotationObject.Dictionary.GetDictionary(PdfTokens.BorderStyleKey);
        PdfArray? borderArray = annotationObject.Dictionary.GetArray(PdfTokens.BorderKey);
        BorderStyle = PdfAnnotationBorderParser.ParseBorderStyle(borderStyleDict, borderArray);

        Color = annotationObject.Dictionary.GetArray(PdfTokens.ColorKey)?.GetFloatArray();
        InteriorColor = annotationObject.Dictionary.GetArray(PdfTokens.InteriorColorKey)?.GetFloatArray();
        Opacity = annotationObject.Dictionary.GetFloat(PdfTokens.StrokeAlphaKey) ?? 1.0f;
        PageReference = annotationObject.Dictionary.GetReference(PdfTokens.PageKey);
        StructuralParent = annotationObject.Dictionary.GetInteger(PdfTokens.StructParentKey);
        AssociatedFiles = PdfFileSpecification.GetFileSpecifications(annotationObject.Dictionary.GetArray(PdfTokens.AssociatedFilesKey));
        OptionalContent = PdfOptionalContentMembership.FromDictionary(annotationObject.Dictionary);

        PdfString? blendModeName = annotationObject.Dictionary.GetName(PdfTokens.BlendModeKey);
        if (blendModeName != null)
        {
            PdfBlendMode blendMode = blendModeName.Value.AsEnum<PdfBlendMode>();
            BlendMode = (blendMode == PdfBlendMode.Unknown) ? PdfBlendMode.Normal : blendMode;
        }
    }

    /// <summary>
    /// Gets the reference of the PDF object that represents this annotation, or <see langword="null"/> when it is a direct object.
    /// </summary>
    public PdfReference? Reference { get; }

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
    /// Gets whether this annotation carries text to show in a pop-up window.
    /// </summary>
    public virtual bool HasPopupContent => Contents != null;

    /// <summary>
    /// Gets whether this annotation should display a content bubble indicator.
    /// </summary>
    /// <remarks>
    /// When true, indicates that the annotation has content (like comments) that should
    /// be accessible through a bubble indicator. The hover rectangles will be the bubble area only.
    /// </remarks>
    public virtual bool ShouldDisplayBubble => false;

    /// <summary>
    /// Gets the pointer cursor to display when hovering over this annotation.
    /// </summary>
    public virtual PdfAnnotationCursorType CursorType => IsInteractive ? PdfAnnotationCursorType.Hand : PdfAnnotationCursorType.Arrow;

    /// <summary>
    /// Gets whether this annotation responds to pointer interaction with a visual state change.
    /// </summary>
    /// <remarks>
    /// True when the appearance stream defines rollover or down states, or when the annotation
    /// has pop-up content to reveal. Subclasses with fallback interactive rendering should
    /// override this to return true unconditionally.
    /// </remarks>
    public virtual bool IsInteractive
    {
        get
        {
            PdfAnnotationVisualStateKind supportedStates = Appearance?.SupportedStates ?? PdfAnnotationVisualStateKind.None;

            return (supportedStates & (PdfAnnotationVisualStateKind.Rollover | PdfAnnotationVisualStateKind.Down)) != 0
                || HasPopupContent;
        }
    }

    /// <summary>
    /// Gets the annotation's contents, which is typically the text displayed
    /// for the annotation or associated with it.
    /// </summary>
    public PdfString? Contents { get; protected set; }

    /// <summary>
    /// Gets the annotation's name, a text string uniquely identifying it among
    /// all the annotations on its page.
    /// </summary>
    public PdfString? Name { get; }

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
    /// Gets or sets whether the annotation is hidden; initially whether <see cref="Flags"/> has
    /// <see cref="PdfAnnotationFlags.Hidden"/>.
    /// </summary>
    public bool IsHidden { get; set; }

    /// <summary>
    /// Gets the parsed appearance that specifies how the annotation is presented visually on the page,
    /// or null when the annotation carries no appearance stream for any visual state.
    /// </summary>
    public PdfAnnotationAppearance? Appearance { get; }

    /// <summary>
    /// Gets the appearance state that, along with the appearance dictionary, controls
    /// the annotation's appearance.
    /// </summary>
    public PdfString? AppearanceState { get; }

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

    /// <inheritdoc />
    public IReadOnlyList<PdfFileSpecification>? AssociatedFiles { get; }

    /// <summary>
    /// Gets the optional content membership controlling the visibility of the annotation (OC entry),
    /// or null when absent.
    /// </summary>
    public PdfOptionalContentMembership? OptionalContent { get; }

    /// <summary>
    /// Gets the blend mode the appearance stream is painted onto the page with (BM entry), or null when absent.
    /// </summary>
    public PdfBlendMode? BlendMode { get; }

    /// <summary>
    /// Gets the hover rectangles for interaction purposes (hit testing, popups, etc.).
    /// </summary>
    /// <remarks>
    /// If ShouldDisplayBubble is true, this returns a small rectangle for the bubble indicator
    /// positioned just above and to the left of the annotation content. The bubble does not
    /// overlap the annotation's own Rectangle. Otherwise this returns the annotation's Rectangle.
    /// Coordinates are in PDF space where the origin is at the bottom-left of the page.
    /// </remarks>
    /// <param name="page">Owning PDF page to crop margins to.</param>
    public virtual PdfRectangle[] GetHoverRectangles(IPdfPage page)
    {
        if (page == null)
        {
            throw new ArgumentNullException(nameof(page));
        }

        if (ShouldDisplayBubble)
        {
            return new PdfRectangle[] { GetBubbleRectangle(page) };
        }

        return new PdfRectangle[] { MoveIntoCropBox(Rectangle, page) };
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
        PdfRenderer renderer,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        PdfStructureTree? structureTree = null;
        if (renderingParameters.ExtractContent)
        {
            structureTree = page.Document.StructureTree;
        }

        PdfAnnotationScope? annotationScope = PdfAnnotationScope.FromAnnotation(this, structureTree);

        if (annotationScope != null)
        {
            processor.Process(new BeginScopeCommand(annotationScope));
        }

        bool rendered = RenderContent(processor, page, visualStateKind, renderer, renderingParameters, observer);

        if (annotationScope != null)
        {
            processor.Process(new EndScopeCommand());
        }

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
        PdfRenderer renderer,
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
        if (Contents != null)
        {
            return $"{Subtype} Annotation: {Contents}";
        }

        if (Name != null)
        {
            return $"{Subtype} Annotation: {Name}";
        }

        return $"{Subtype} Annotation";
    }

    private bool RenderContent(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfAnnotationVisualStateKind visualStateKind,
        PdfRenderer renderer,
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
                PdfAnnotationGraphics.RenderIcon(processor, bubbleIcon, GetBubbleRectangle(page), borderColor, backgroundColor);
            }
        }

        processor.Process(SaveStateCommand.Instance);

        if (Appearance != null)
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

    private PdfRectangle GetBubbleRectangle(IPdfPage page)
    {
        PdfRectangle bubbleRectangle = new(
            ContentStart.X - PdfAnnotationGraphics.DefaultBubbleSize,
            ContentStart.Y,
            ContentStart.X,
            ContentStart.Y + PdfAnnotationGraphics.DefaultBubbleSize);

        return MoveIntoCropBox(bubbleRectangle, page);
    }

    private static PdfRectangle MoveIntoCropBox(in PdfRectangle rectangle, IPdfPage page)
    {
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            return PdfRectangle.Empty;
        }

        PdfRectangle crop = page.CropBox;

        float dx = 0f;
        if (rectangle.Left < crop.Left)
        {
            dx = crop.Left - rectangle.Left;
        }
        else if (rectangle.Right > crop.Right)
        {
            dx = crop.Right - rectangle.Right;
        }

        float dy = 0f;
        if (rectangle.Top < crop.Top)
        {
            dy = crop.Top - rectangle.Top;
        }
        else if (rectangle.Bottom > crop.Bottom)
        {
            dy = crop.Bottom - rectangle.Bottom;
        }

        return new PdfRectangle(rectangle.Left + dx, rectangle.Top + dy, rectangle.Right + dx, rectangle.Bottom + dy);
    }

    /// <summary>
    /// The annotation at <paramref name="index"/> of <paramref name="array"/>, or <see langword="null"/> when absent.
    /// </summary>
    internal static PdfAnnotation? GetAnnotation(PdfArray array, int index)
        => array.Document.ObjectCache.GetOrCreate(array, index, CreateAnnotation);

    /// <summary>
    /// The annotation at <paramref name="reference"/> of <paramref name="document"/>, or <see langword="null"/> when absent.
    /// </summary>
    internal static PdfAnnotation? GetAnnotation(IPdfDocumentInternal document, in PdfReference reference)
        => document.ObjectCache.GetOrCreate(reference, CreateAnnotation);

    /// <summary>
    /// Creates an annotation instance from a PDF object.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing an annotation.</param>
    /// <returns>A concrete annotation instance, or null if the object is not a valid annotation.</returns>
    internal static PdfAnnotation? CreateAnnotation(PdfObject annotationObject)
    {
        if (annotationObject == null)
        {
            return null;
        }

        // Get the annotation subtype and convert to enum
        PdfAnnotationSubType subtype = annotationObject.Dictionary.GetNameOrDefault(PdfTokens.SubtypeKey).AsEnum<PdfAnnotationSubType>();

        // Create specific annotation types based on subtype
        return subtype switch
        {
            PdfAnnotationSubType.Text => new PdfTextAnnotation(annotationObject),
            PdfAnnotationSubType.Link => new PdfLinkAnnotation(annotationObject),
            PdfAnnotationSubType.FreeText => new PdfFreeTextAnnotation(annotationObject),
            PdfAnnotationSubType.Line => new PdfLineAnnotation(annotationObject),
            PdfAnnotationSubType.Square => new PdfSquareAnnotation(annotationObject),
            PdfAnnotationSubType.Circle => new PdfCircleAnnotation(annotationObject),
            PdfAnnotationSubType.Polygon => new PdfPolygonAnnotation(annotationObject),
            PdfAnnotationSubType.PolyLine => new PdfPolyLineAnnotation(annotationObject),
            PdfAnnotationSubType.Highlight => new PdfHighlightAnnotation(annotationObject),
            PdfAnnotationSubType.Underline => new PdfUnderlineAnnotation(annotationObject),
            PdfAnnotationSubType.Squiggly => new PdfSquigglyAnnotation(annotationObject),
            PdfAnnotationSubType.StrikeOut => new PdfStrikeOutAnnotation(annotationObject),
            PdfAnnotationSubType.Stamp => new PdfStampAnnotation(annotationObject),
            PdfAnnotationSubType.Caret => new PdfCaretAnnotation(annotationObject),
            PdfAnnotationSubType.Ink => new PdfInkAnnotation(annotationObject),
            PdfAnnotationSubType.Popup => new PdfPopupAnnotation(annotationObject),
            PdfAnnotationSubType.FileAttachment => new PdfFileAttachmentAnnotation(annotationObject),
            PdfAnnotationSubType.Widget => new PdfWidgetAnnotation(annotationObject), // TODO: [LOW] Widget is not fully implemented yet
            _ => new PdfGenericAnnotation(annotationObject, subtype)
        };
    }
}
