using PdfPixel.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Applies a clipping path to the canvas.
/// Clones the path on construction to ensure immutability.
/// </summary>
public sealed class ClipPathCommand : PdfCommand
{
    private readonly SKPath _path;
    private readonly SKClipOperation _operation;

    public ClipPathCommand(SKPath path, SKClipOperation operation)
    {
        _path = new SKPath(path);
        _operation = operation;
    }

    /// <inheritdoc />
    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        bool antialias = CommandHelpers.GetPathIsAntialias(_path, canvas, executionContext);
        canvas.ClipPath(_path, _operation, antialias);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _path.Dispose();
    }

    public static SKPath CreateBleedClipPath(SKPath clipPath, float deviceScale, SKMatrix ctm, float bleedPixels = 1f)
    {
        // TODO: refactor
        // Extract local->device scale factors from CTM basis vectors
        var vx = new SKPoint(ctm.ScaleX, ctm.SkewY);
        var vy = new SKPoint(ctm.SkewX, ctm.ScaleY);

        float scaleX = vx.Length * deviceScale;
        float scaleY = vy.Length * deviceScale;

        if (scaleX <= 0f) scaleX = 1f;
        if (scaleY <= 0f) scaleY = 1f;

        // Conservative local-space expansion
        float localBleed = bleedPixels / MathF.Min(scaleX, scaleY);

        using var strokePaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = localBleed * 2f,
            StrokeJoin = SKStrokeJoin.Miter,
            StrokeCap = SKStrokeCap.Butt,
            IsAntialias = false
        };

        using var expandedStroke = strokePaint.GetFillPath(clipPath);

        var result = new SKPath(clipPath);
        result.AddPath(expandedStroke);

        return result;
    }
}
