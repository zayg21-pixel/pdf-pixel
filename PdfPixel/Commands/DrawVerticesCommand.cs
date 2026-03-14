using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Draws vertices with a destination-in blend mode.
/// Takes ownership of both the vertices and the paint.
/// </summary>
public sealed class DrawVerticesCommand : PdfCommand
{
    private readonly SKVertices _vertices;
    private readonly SKPaint _basePaint;

    public DrawVerticesCommand(SKVertices vertices, SKPaint basePaint)
    {
        _vertices = vertices;
        _basePaint = basePaint;
    }

    /// <inheritdoc />
    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers)
    {
        using var paint = _basePaint.Clone(); // TODO: not sure about this
        foreach (var modifier in modifiers)
        {
            modifier.ModifyPaint(paint);
        }
        canvas.DrawVertices(_vertices, SKBlendMode.DstIn, paint);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _vertices.Dispose();
        _basePaint.Dispose();
    }
}
