using System;
using PdfReader.Color.Paint;
using PdfReader.Models;
using PdfReader.Pattern.Utilities;
using PdfReader.Rendering;
using PdfReader.Rendering.State;
using SkiaSharp;

namespace PdfReader.Pattern.Model;

/// <summary>
/// Strongly typed paint type for tiling patterns (PDF spec Table 90)
/// </summary>
public enum PdfTilingPaintType
{
    Colored = 1,
    Uncolored = 2
}

/// <summary>
/// Strongly typed tiling type (PDF spec Table 91)
/// </summary>
public enum PdfTilingSpacingType
{
    ConstantSpacing = 1,
    NoDistortion = 2,
    ConstantSpacingFast = 3
}

/// <summary>
/// Represents a parsed tiling (/PatternType 1) pattern.
/// </summary>
public sealed class PdfTilingPattern : PdfPattern
{
    private readonly IPdfRenderer _renderer;

    internal PdfTilingPattern(
        IPdfRenderer renderer,
        PdfObject sourceObject,
        SKRect bbox,
        float xStep,
        float yStep,
        PdfTilingPaintType paintTypeKind,
        PdfTilingSpacingType tilingTypeKind,
        SKMatrix matrix)
        : base(sourceObject, matrix, PdfPatternType.Tiling)
    {
        _renderer = renderer;
        BBox = bbox;
        XStep = xStep;
        YStep = yStep;
        PaintTypeKind = paintTypeKind;
        TilingTypeKind = tilingTypeKind;
    }

    /// <summary>
    /// Gets the bounding box of the pattern cell.
    /// </summary>
    public SKRect BBox { get; }

    /// <summary>
    /// Gets the horizontal spacing between pattern cells.
    /// </summary>
    public float XStep { get; }

    /// <summary>
    /// Gets the vertical spacing between pattern cells.
    /// </summary>
    public float YStep { get; }

    /// <summary>
    /// Gets the paint type (colored or uncolored).
    /// </summary>
    public PdfTilingPaintType PaintTypeKind { get; }

    /// <summary>
    /// Gets the tiling type (spacing and distortion rules).
    /// </summary>
    public PdfTilingSpacingType TilingTypeKind { get; }

    internal override void RenderPattern(SKCanvas canvas, PdfGraphicsState state, IRenderTarget renderTarget)
    {
        var tile = TilingPatternShaderBuilder.RenderTilingCell(_renderer, this);

        var matrix = SKMatrix.Concat(state.CTM.Invert(), PatternMatrix);
        canvas.Save();

        var clipPath = renderTarget.ClipPath;
        canvas.ClipPath(clipPath, SKClipOperation.Intersect, antialias: true);

        canvas.Concat(matrix);

        using var paint = PdfPaintFactory.CreateShadingPaint(state);

        if (PaintTypeKind == PdfTilingPaintType.Uncolored)
        {
            paint.ColorFilter = SKColorFilter.CreateBlendMode(renderTarget.Color, SKBlendMode.SrcIn);
        }

        var bounds = matrix.Invert().MapRect(clipPath.Bounds);

        float startX = bounds.Left - bounds.Left % XStep;
        float startY = bounds.Top - bounds.Top % YStep;
        float endX = bounds.Right + bounds.Right % XStep;
        float endY = bounds.Bottom + bounds.Bottom % YStep;

        int xCount = (int)Math.Ceiling((endX - startX) / XStep);
        int yCount = (int)Math.Ceiling((endY - startY) / YStep);

        for (int i = 0; i <= xCount; i++)
        {
            float x = startX + i * XStep;
            for (int j = 0; j <= yCount; j++)
            {
                float y = startY + j * YStep;
                canvas.DrawPicture(tile, x, y, paint);
            }
        }

        canvas.Restore();
    }
}
