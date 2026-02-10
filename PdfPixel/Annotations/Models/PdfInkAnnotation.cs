using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;

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
        InkList = annotationObject.Dictionary.GetArray(PdfTokens.InkListKey);
    }

    /// <summary>
    /// Gets the ink list containing the paths that make up the annotation.
    /// </summary>
    /// <remarks>
    /// InkList is an array of arrays, where each sub-array represents a path
    /// with alternating x,y coordinates.
    /// </remarks>
    public PdfArray InkList { get; }

    /// <summary>
    /// Creates a fallback rendering for ink annotations when no appearance stream is available.
    /// </summary>
    /// <param name="page">The PDF page containing this annotation.</param>
    /// <param name="visualStateKind">The visual state to render (Normal, Rollover, Down).</param>
    /// <returns>An SKPicture containing the rendered ink paths.</returns>
    public override SKPicture CreateFallbackRender(PdfPage page, PdfAnnotationVisualStateKind visualStateKind)
    {
        if (InkList == null || InkList.Count == 0)
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

        // Render each path in the ink list
        for (int i = 0; i < InkList.Count; i++)
        {
            var pathArray = InkList.GetArray(i);
            if (pathArray == null || pathArray.Count < 4) // Need at least 2 points (4 coordinates)
                continue;

            using var path = new SKPath();
            var coords = pathArray.GetFloatArray();

            if (coords == null || coords.Length < 4)
                continue;

            path.MoveTo(coords[0], coords[1]);

            for (int j = 2; j < coords.Length - 1; j += 2)
            {
                path.LineTo(coords[j], coords[j + 1]);
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
        var pathCount = InkList?.Count ?? 0;
        
        if (!string.IsNullOrEmpty(contentsText))
        {
            return $"Ink Annotation ({pathCount} paths): {contentsText}";
        }
        
        return $"Ink Annotation ({pathCount} paths)";
    }
}