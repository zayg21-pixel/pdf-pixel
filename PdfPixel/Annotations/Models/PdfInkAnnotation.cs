using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
using System;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a PDF ink annotation for freehand drawing.
/// </summary>
/// <remarks>
/// Ink annotations represent freehand "scribbles" composed of one or more disjoint paths.
/// When displayed or printed, the paths are stroked with the annotation's color
/// using a solid line of uniform thickness.
/// </remarks>
public class PdfInkAnnotation : PdfAnnotationBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfInkAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this ink annotation.</param>
    public PdfInkAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.Ink)
    {
        var inkList = annotationObject.Dictionary.GetArray(PdfTokens.InkListKey);
        InkList = ParseInkList(inkList);
    }

    protected override SKPoint ContentStart => InkList != null && InkList.Length > 0 && InkList[0].Length > 0 ? InkList[0][0] : base.ContentStart;

    /// <summary>
    /// Gets the parsed ink list as an array of arrays of SKPoint.
    /// Each inner array represents a path (sequence of points).
    /// </summary>
    public SKPoint[][] InkList { get; }

    private static SKPoint[][] ParseInkList(PdfArray inkList)
    {
        if (inkList == null || inkList.Count == 0)
        {
            return Array.Empty<SKPoint[]>();
        }

        var result = new SKPoint[inkList.Count][];
        for (int i = 0; i < inkList.Count; i++)
        {
            var pathArray = inkList.GetArray(i);
            if (pathArray == null || pathArray.Count < 4)
            {
                result[i] = Array.Empty<SKPoint>();
                continue;
            }

            var coords = pathArray.GetFloatArray();
            if (coords == null || coords.Length < 4)
            {
                result[i] = Array.Empty<SKPoint>();
                continue;
            }

            var points = new SKPoint[coords.Length / 2];
            int p = 0;
            for (int j = 0; j < coords.Length - 1; j += 2)
            {
                points[p++] = new SKPoint(coords[j], coords[j + 1]);
            }
            result[i] = points;
        }
        return result;
    }

    /// <summary>
    /// Creates a fallback rendering for ink annotations when no appearance stream is available.
    /// </summary>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render (Normal, Rollover, Down).</param>
    /// <returns>An SKPicture containing the rendered ink paths.</returns>
    public override SKPicture CreateFallbackRender(PdfPage page, PdfAnnotationVisualStateKind visualStateKind)
    {
        if (InkList == null || InkList.Length == 0)
        {
            return null;
        }

        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(Rectangle);

        var lineWidth = BorderStyle?.Width ?? 1.0f;
        var inkColor = ResolveColor(page, SKColors.Black);

        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = lineWidth,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            IsAntialias = true,
            Color = inkColor
        };

        BorderStyle?.TryApplyEffect(paint, inkColor);

        // Render each path in the parsed ink list
        foreach (var points in InkList)
        {
            if (points == null || points.Length < 2)
            {
                continue;
            }

            using var path = new SKPath();
            path.MoveTo(points[0]);
            for (int j = 1; j < points.Length; j++)
            {
                path.LineTo(points[j]);
            }
            canvas.DrawPath(path, paint);
        }

        return recorder.EndRecording();
    }

    /// <summary>
    /// Returns a string representation of this ink annotation.
    /// </summary>
    /// <returns>A string containing the annotation type and path count.</returns>
    public override string ToString()
    {
        var contentsText = Contents.ToString();
        var pathCount = InkList?.Length ?? 0;

        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"Ink Annotation ({pathCount} paths): {contentsText}";
        }

        return $"Ink Annotation ({pathCount} paths)";
    }
}