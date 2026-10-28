using PdfPixel.Annotations;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.PdfPanel.Input;
using System;

namespace PdfPixel.PdfPanel.Annotations;

internal static class PdfAnnotationExtensions
{
    /// <summary>
    /// Gets whether the panel lets the user interact with the annotation.
    /// </summary>
    public static bool IsInteractive(this PdfAnnotation annotation)
    {
        bool isReadOnly = (annotation.Flags & PdfAnnotationFlags.ReadOnly) != 0
            && annotation is not PdfWidgetAnnotation;

        return !IsHiddenFromView(annotation)
            && !isReadOnly
            && (annotation.IsInteractive || (annotation.Flags & PdfAnnotationFlags.ToggleNoView) != 0);
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
        float pivotX = annotation.Rectangle.Left;
        float pivotY = annotation.Rectangle.Bottom;
        PdfMatrix matrix = PdfMatrix.CreateTranslation(-pivotX, -pivotY);

        if (annotation.IsNoZoom())
        {
            float inverseScale = referenceScale / scale;
            matrix = matrix.PostConcat(PdfMatrix.CreateScale(inverseScale, inverseScale));
        }

        if (annotation.IsNoRotate())
        {
            matrix = matrix.PostConcat(PdfMatrix.CreateRotationDegrees(rotation));
        }

        return matrix.PostConcat(PdfMatrix.CreateTranslation(pivotX, pivotY));
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
        else
        {
            return annotation.CursorType switch
            {
                PdfAnnotationCursorType.Hand => PdfPanelCursor.Hand,
                PdfAnnotationCursorType.IBeam => PdfPanelCursor.IBeam,
                _ => PdfPanelCursor.Arrow
            };
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
}
