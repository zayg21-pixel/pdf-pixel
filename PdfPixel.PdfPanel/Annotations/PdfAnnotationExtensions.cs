using PdfPixel.Annotations;
using PdfPixel.Annotations.Icons;
using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.PdfPanel.Input;
using PdfPixel.PdfPanel.Rendering;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Annotations;

internal static class PdfAnnotationExtensions
{
    private const float BubbleSize = 16f;

    private static readonly PdfPaint BubbleBorderPaint = new(PdfPaintStyle.Fill, new PdfRgbaColor(180f / 255f, 140f / 255f, 60f / 255f));

    private static readonly PdfPaint BubbleBackgroundPaint = new(PdfPaintStyle.Fill, new PdfRgbaColor(1f, 1f, 235f / 255f));

    /// <summary>
    /// Gets whether the panel shows a bubble for the annotation: a markup annotation with content to show in a popup,
    /// unless it shows its content itself or is itself an icon.
    /// </summary>
    public static bool ShouldDisplayBubble(this PdfAnnotation annotation)
    {
        return annotation switch
        {
            PdfTextAnnotation or PdfFreeTextAnnotation or PdfFileAttachmentAnnotation or PdfStampAnnotation or PdfCaretAnnotation or PdfRedactAnnotation => false,
            PdfLineAnnotation line => !line.HasCaption && line.Contents != null,
            PdfMarkupAnnotation markup => markup.Contents != null,
            _ => false
        };
    }

    /// <summary>
    /// Gets whether the panel shows the annotation's content in a popup: a markup annotation with contents.
    /// </summary>
    public static bool HasPopupContent(this PdfAnnotation annotation) => annotation is PdfMarkupAnnotation && annotation.Contents != null;

    /// <summary>
    /// Gets the rectangle of a bubble placed at <paramref name="anchor"/>, in the space of its bubble matrix.
    /// </summary>
    public static PdfRectangle GetBubbleRectangle(in PdfPoint anchor) => new(anchor.X - BubbleSize, anchor.Y, anchor.X, anchor.Y + BubbleSize);

    /// <summary>
    /// Records the bubble placed at <paramref name="anchor"/> in <paramref name="visualState"/>.
    /// </summary>
    public static void RenderBubble(IPdfCommandProcessor processor, in PdfPoint anchor, PdfAnnotationVisualStateKind visualState)
    {
        PdfAnnotationIconDefinition? bubbleIcon = PdfAnnotationGraphics.GetAnnotationBubbleIcon(visualState);

        if (bubbleIcon != null)
        {
            PdfAnnotationGraphics.RenderIcon(processor, bubbleIcon, GetBubbleRectangle(anchor), BubbleBorderPaint, BubbleBackgroundPaint);
        }
    }

    /// <summary>
    /// Gets whether the panel lets the user interact with the annotation.
    /// </summary>
    public static bool IsInteractive(this PdfAnnotation annotation)
    {
        bool isReadOnly = (annotation.Flags & PdfAnnotationFlags.ReadOnly) != 0
            && annotation is not PdfWidgetAnnotation;

        return !IsHiddenFromView(annotation)
            && !isReadOnly
            && (RespondsToPointer(annotation) || (annotation.Flags & PdfAnnotationFlags.ToggleNoView) != 0);
    }

    /// <summary>
    /// Gets whether the panel displays the annotation, given whether it is the active annotation.
    /// </summary>
    public static bool IsVisible(this PdfAnnotation annotation, bool isActive)
    {
        bool isNoView = (annotation.Flags & PdfAnnotationFlags.NoView) != 0;

        if (isActive && (annotation.Flags & PdfAnnotationFlags.ToggleNoView) != 0)
        {
            isNoView = !isNoView;
        }

        return !IsHiddenFromView(annotation) && !isNoView;
    }

    /// <summary>
    /// Gets the state the panel draws the annotation in at the given scale, the scale of a zoom of 100% and the page
    /// rotation in degrees, given whether it is the active annotation, whether the pointer is over its content rather
    /// than its bubble, and the pointer state over it; an annotation drawn the same in every pointer state stays normal.
    /// </summary>
    public static PdfAnnotationState GetState(
        this PdfAnnotation annotation,
        bool isActive,
        bool isOverContent,
        PdfPanelPointerState pointerState,
        float scale,
        float referenceScale,
        int rotation)
    {
        var visualState = PdfAnnotationVisualStateKind.Normal;

        if (isActive && (annotation.SupportedStates & (PdfAnnotationVisualStateKind.Rollover | PdfAnnotationVisualStateKind.Down)) != 0)
        {
            visualState = GetVisualState(pointerState);
        }

        return new PdfAnnotationState(visualState, annotation.GetDisplayMatrix(scale, referenceScale, rotation), isHidden: !annotation.IsVisible(isActive && isOverContent));
    }

    /// <summary>
    /// Returns the areas in page space of the annotations of <paramref name="page"/> whose state in
    /// <paramref name="states"/> differs from the one in <paramref name="previousStates"/>, under the previous and the
    /// new matrix; empty when none changed.
    /// </summary>
    public static PdfRectangle[] GetChangedAnnotationRegions(this PdfPanelPage page, PdfAnnotationStates previousStates, PdfAnnotationStates states)
    {
        if (ReferenceEquals(previousStates, states))
        {
            return Array.Empty<PdfRectangle>();
        }

        List<PdfRectangle> regions = [];
        IReadOnlyList<PdfPageAnnotation> pageAnnotations;

        lock (page.PanelDocument.DocumentLocker)
        {
            pageAnnotations = page.PdfPage.GetAnnotations();
        }

        foreach (PdfPageAnnotation pageAnnotation in pageAnnotations)
        {
            PdfAnnotation annotation = pageAnnotation.Content;
            PdfAnnotationState previousState = previousStates.GetState(annotation);
            PdfAnnotationState state = states.GetState(annotation);

            if (!state.Equals(previousState))
            {
                regions.Add(page.FromPdfRect(previousState.Matrix.MapRect(annotation.ContentBounds)));
                regions.Add(page.FromPdfRect(state.Matrix.MapRect(annotation.ContentBounds)));
            }
        }

        return regions.ToArray();
    }

    /// <summary>
    /// Gets the visual state the active annotation is drawn in under <paramref name="pointerState"/>.
    /// </summary>
    public static PdfAnnotationVisualStateKind GetVisualState(PdfPanelPointerState pointerState)
    {
        return pointerState switch
        {
            PdfPanelPointerState.Pressed => PdfAnnotationVisualStateKind.Down,
            PdfPanelPointerState.Hovered => PdfAnnotationVisualStateKind.Rollover,
            _ => PdfAnnotationVisualStateKind.Normal
        };
    }

    /// <summary>
    /// Gets whether the annotation keeps its size on screen at every zoom factor.
    /// </summary>
    public static bool IsNoZoom(this PdfAnnotation annotation)
    {
        return (annotation.Flags & PdfAnnotationFlags.NoZoom) != 0
            || annotation is PdfTextAnnotation;
    }

    /// <summary>
    /// Gets whether the annotation stays upright at every page rotation.
    /// </summary>
    public static bool IsNoRotate(this PdfAnnotation annotation)
    {
        return (annotation.Flags & PdfAnnotationFlags.NoRotate) != 0
            || annotation is PdfTextAnnotation;
    }

    /// <summary>
    /// Gets the matrix, in PDF coordinates, the panel draws the annotation with at the given scale, the scale of a
    /// zoom of 100% and the page rotation in degrees.
    /// </summary>
    public static PdfMatrix GetDisplayMatrix(this PdfAnnotation annotation, float scale, float referenceScale, int rotation)
    {
        PdfPoint pivot = new(annotation.Rectangle.Left, annotation.Rectangle.Bottom);

        return CreateScreenFixedMatrix(pivot, annotation.IsNoZoom(), annotation.IsNoRotate(), scale, referenceScale, rotation);
    }

    /// <summary>
    /// Gets the matrix, applied inside <see cref="GetDisplayMatrix"/>, that keeps the bubble placed at
    /// <paramref name="anchor"/> at its size on screen and upright at every zoom factor and page rotation, like an icon,
    /// and moves it back inside <paramref name="cropBox"/> where it would stick out of it.
    /// </summary>
    public static PdfMatrix GetBubbleMatrix(this PdfAnnotation annotation, in PdfPoint anchor, float scale, float referenceScale, int rotation, in PdfRectangle cropBox)
    {
        PdfMatrix displayMatrix = annotation.GetDisplayMatrix(scale, referenceScale, rotation);
        PdfMatrix bubbleMatrix = CreateScreenFixedMatrix(anchor, !annotation.IsNoZoom(), !annotation.IsNoRotate(), scale, referenceScale, rotation);
        PdfMatrix bubbleToPage = bubbleMatrix.PostConcat(displayMatrix);
        PdfPoint shift = GetShiftIntoCropBox(bubbleToPage.MapRect(GetBubbleRectangle(anchor)), cropBox);

        return bubbleToPage
            .PostConcat(PdfMatrix.CreateTranslation(shift.X, shift.Y))
            .PostConcat(displayMatrix.Invert());
    }

    /// <summary>
    /// Gets the panel cursor shown over the annotation.
    /// </summary>
    public static PdfPanelCursor GetCursor(this PdfAnnotation annotation)
    {
        if (!annotation.IsVisible(isActive: true))
        {
            return PdfPanelCursor.Arrow;
        }
        else if (RespondsToPointer(annotation))
        {
            return PdfPanelCursor.Hand;
        }
        else
        {
            return PdfPanelCursor.Arrow;
        }
    }

    /// <summary>
    /// Creates annotation message from an annotation's metadata.
    /// </summary>
    public static PdfAnnotationMessage? CreateAnnotationMessage(this PdfMarkupAnnotation annotation, PdfAnnotationMessage[] replies)
    {
        if (annotation.Contents == null)
        {
            return null;
        }

        string contents = annotation.Contents.Value.DecodePdfString();

        if (string.IsNullOrEmpty(contents))
        {
            return null;
        }

        string? title = annotation.Title?.DecodePdfString();
        string? messageTitle = (!string.IsNullOrEmpty(title)) ? title : null;
        DateTimeOffset? messageDate = (annotation.CreationDate.HasValue) ? new DateTimeOffset(annotation.CreationDate.Value) : (DateTimeOffset?)null;

        return new PdfAnnotationMessage(messageDate, messageTitle, contents, replies);
    }

    /// <summary>
    /// Returns whether the pointer does something over the annotation: links, file attachments, redactions and text
    /// annotations always, others when they are drawn differently under it or have content to show in a popup.
    /// </summary>
    private static bool RespondsToPointer(PdfAnnotation annotation)
    {
        return annotation switch
        {
            PdfLinkAnnotation or PdfFileAttachmentAnnotation or PdfRedactAnnotation or PdfTextAnnotation => true,
            _ => (annotation.SupportedStates & (PdfAnnotationVisualStateKind.Rollover | PdfAnnotationVisualStateKind.Down)) != 0
                || annotation.HasPopupContent()
        };
    }

    private static bool IsHiddenFromView(PdfAnnotation annotation)
    {
        bool isInvisibleUnknownType = (annotation.Flags & PdfAnnotationFlags.Invisible) != 0
            && annotation.Subtype == PdfAnnotationSubType.Unknown;
        bool isPermanentNoView = (annotation.Flags & PdfAnnotationFlags.NoView) != 0
            && (annotation.Flags & PdfAnnotationFlags.ToggleNoView) == 0;

        return isInvisibleUnknownType
            || annotation.IsHidden
            || isPermanentNoView;
    }

    /// <summary>
    /// Creates the matrix that, around <paramref name="pivot"/>, keeps the size on screen when
    /// <paramref name="keepsSize"/> is set and turns back the page rotation when <paramref name="keepsUpright"/> is set.
    /// </summary>
    private static PdfMatrix CreateScreenFixedMatrix(in PdfPoint pivot, bool keepsSize, bool keepsUpright, float scale, float referenceScale, int rotation)
    {
        PdfMatrix matrix = PdfMatrix.CreateTranslation(-pivot.X, -pivot.Y);

        if (keepsSize)
        {
            float inverseScale = referenceScale / scale;
            matrix = matrix.PostConcat(PdfMatrix.CreateScale(inverseScale, inverseScale));
        }

        if (keepsUpright)
        {
            matrix = matrix.PostConcat(PdfMatrix.CreateRotationDegrees(rotation));
        }

        return matrix.PostConcat(PdfMatrix.CreateTranslation(pivot.X, pivot.Y));
    }

    /// <summary>
    /// Returns how far <paramref name="rectangle"/> has to move along each axis to lie inside <paramref name="cropBox"/>,
    /// 0 along an axis where it already does.
    /// </summary>
    private static PdfPoint GetShiftIntoCropBox(in PdfRectangle rectangle, in PdfRectangle cropBox)
    {
        float shiftX = 0f;
        if (rectangle.Left < cropBox.Left)
        {
            shiftX = cropBox.Left - rectangle.Left;
        }
        else if (rectangle.Right > cropBox.Right)
        {
            shiftX = cropBox.Right - rectangle.Right;
        }

        float shiftY = 0f;
        if (rectangle.Top < cropBox.Top)
        {
            shiftY = cropBox.Top - rectangle.Top;
        }
        else if (rectangle.Bottom > cropBox.Bottom)
        {
            shiftY = cropBox.Bottom - rectangle.Bottom;
        }

        return new PdfPoint(shiftX, shiftY);
    }
}
