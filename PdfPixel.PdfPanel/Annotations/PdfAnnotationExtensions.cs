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
        PdfPoint shift = GetShiftIntoCropBox(bubbleToPage.MapRect(PdfAnnotation.GetBubbleRectangle(anchor)), cropBox);

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
