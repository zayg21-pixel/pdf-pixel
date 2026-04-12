using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Shading;
using PdfPixel.Shading.Model;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Draws PDF shading lazily at Execute time.
/// Stores the shading model and a <see cref="ShadingDecodingContext"/> snapshot so all heavy
/// work (function sampling, mesh decoding, gradient construction) is deferred until replay.
/// Caches expensive results and only rebuilds when the relevant rendering parameter changes.
/// </summary>
public sealed class PdfDrawShadingCommand : PdfCommand
{
    private readonly PdfShading _shading;
    private readonly ShadingDecodingContext _context;
    private readonly PdfShadingBuilder _builder;

    // Function-based (Type 1) cache
    private FunctionShadingResult _functionCache;
    private int _functionCacheSamples = -1;

    // Axial (Type 2) cache
    private SKPaint _axialCache;
    private int _axialCacheSamples = -1;

    // Radial (Type 3) cache
    private RadialShadingPaints _radialCache;
    private int _radialCacheSamples = -1;

    // Gouraud (Type 4/5) cache
    private SKVertices _gouraudCache;
    private bool _gouraudCacheBuilt;

    // Patch mesh (Type 6/7) cache
    private SKVertices _patchMeshCache;
    private int _patchMeshCacheMaxVertices = -1;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfDrawShadingCommand"/> class.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="context">Snapshot of graphics-state values captured at record time.</param>
    /// <param name="loggerFactory">Logger factory for diagnostic output.</param>
    public PdfDrawShadingCommand(PdfShading shading, ShadingDecodingContext context, ILoggerFactory loggerFactory)
    {
        _shading = shading;
        _context = context;
        _builder = new PdfShadingBuilder(loggerFactory);
    }

    /// <inheritdoc />
    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        bool antialias = executionContext.RenderingParameters.Antialias;
        int defaultFunctionSamples = executionContext.RenderingParameters.DefaultFunctionSamples;
        int maxTessellationVertices = executionContext.RenderingParameters.MaxTessellationVertices;

        switch (_shading.ShadingType)
        {
            case PdfShadingType.FunctionBased:
                ExecuteFunctionBased(canvas, antialias, defaultFunctionSamples);
                break;

            case PdfShadingType.Axial:
                ExecuteAxial(canvas, modifiers, antialias, defaultFunctionSamples);
                break;

            case PdfShadingType.Radial:
                ExecuteRadial(canvas, modifiers, antialias, defaultFunctionSamples);
                break;

            case PdfShadingType.FreeFormGouraud:
            case PdfShadingType.LatticeFormGouraud:
                ExecuteGouraud(canvas, modifiers, antialias);
                break;

            case PdfShadingType.CoonsPatchMesh:
            case PdfShadingType.TensorProductPatchMesh:
                ExecutePatchMesh(canvas, modifiers, antialias, maxTessellationVertices);
                break;
        }
    }

    /// <summary>
    /// Executes function-based (Type 1) shading by rendering a cached sampled bitmap.
    /// Rebuilds the bitmap when <paramref name="defaultFunctionSamples"/> changes.
    /// </summary>
    private void ExecuteFunctionBased(SKCanvas canvas, bool antialias, int defaultFunctionSamples)
    {
        if (_functionCacheSamples != defaultFunctionSamples)
        {
            _functionCache?.Dispose();
            _functionCache = _builder.BuildFunctionBasedBitmap(
                _shading,
                _context.Converter,
                _context.RenderingIntent,
                _context.FullTransferFunction,
                defaultFunctionSamples);
            _functionCacheSamples = defaultFunctionSamples;
        }

        if (_functionCache == null)
        {
            return;
        }

        canvas.Save();
        canvas.Concat(_functionCache.Matrix);
        canvas.DrawBitmap(_functionCache.Bitmap, SKPoint.Empty);
        canvas.Restore();
    }

    /// <summary>
    /// Executes axial (Type 2) shading by drawing a cached linear gradient paint.
    /// Rebuilds the paint when <paramref name="defaultFunctionSamples"/> changes.
    /// </summary>
    private void ExecuteAxial(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, bool antialias, int defaultFunctionSamples)
    {
        if (_axialCacheSamples != defaultFunctionSamples)
        {
            _axialCache?.Dispose();
            _axialCache = CreateAxialPaint(defaultFunctionSamples);
            _axialCacheSamples = defaultFunctionSamples;
        }

        if (_axialCache == null)
        {
            return;
        }

        DrawPaintToCanvas(canvas, _axialCache, modifiers, antialias);
    }

    /// <summary>
    /// Executes radial (Type 3) shading by drawing cached inner and outer conical gradient paints.
    /// Rebuilds the paints when <paramref name="defaultFunctionSamples"/> changes.
    /// </summary>
    private void ExecuteRadial(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, bool antialias, int defaultFunctionSamples)
    {
        if (_radialCacheSamples != defaultFunctionSamples)
        {
            _radialCache?.Dispose();
            _radialCache = CreateRadialPaints(defaultFunctionSamples);
            _radialCacheSamples = defaultFunctionSamples;
        }

        if (_radialCache == null)
        {
            return;
        }

        DrawPaintToCanvas(canvas, _radialCache.InnerPaint, modifiers, antialias);
        DrawPaintToCanvas(canvas, _radialCache.OuterPaint, modifiers, antialias);
    }

    /// <summary>
    /// Executes Gouraud-shaded triangle mesh (Type 4/5) shading.
    /// The mesh has no parameter dependency and is built once.
    /// </summary>
    private void ExecuteGouraud(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, bool antialias)
    {
        if (!_gouraudCacheBuilt)
        {
            var sampler = _context.Converter.GetRgbaSampler(_context.RenderingIntent, _context.FullTransferFunction);
            _gouraudCache = _builder.BuildGouraudVertices(_shading, sampler);
            _gouraudCacheBuilt = true;
        }

        if (_gouraudCache == null)
        {
            return;
        }

        DrawVerticesToCanvas(canvas, _gouraudCache, modifiers, antialias);
    }

    /// <summary>
    /// Executes Coons (Type 6) or Tensor-product (Type 7) patch mesh shading.
    /// Rebuilds the mesh when <paramref name="maxTessellationVertices"/> changes.
    /// </summary>
    private void ExecutePatchMesh(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, bool antialias, int maxTessellationVertices)
    {
        if (_patchMeshCacheMaxVertices != maxTessellationVertices)
        {
            _patchMeshCache?.Dispose();
            var sampler = _context.Converter.GetRgbaSampler(_context.RenderingIntent, _context.FullTransferFunction);
            _patchMeshCache = _builder.BuildPatchMeshVertices(_shading, sampler, maxTessellationVertices);
            _patchMeshCacheMaxVertices = maxTessellationVertices;
        }

        if (_patchMeshCache == null)
        {
            return;
        }

        DrawVerticesToCanvas(canvas, _patchMeshCache, modifiers, antialias);
    }

    /// <summary>
    /// Builds the axial gradient paint for the given number of function samples.
    /// Returns <see langword="null"/> when the shading data is invalid.
    /// </summary>
    private SKPaint CreateAxialPaint(int defaultFunctionSamples)
    {
        _builder.BuildShadingColorsAndStops(
            _shading,
            _context.Converter,
            _context.RenderingIntent,
            _context.FullTransferFunction,
            defaultFunctionSamples,
            out var colors,
            out var positions);

        if (colors == null || colors.Length == 0)
        {
            return null;
        }

        return _builder.BuildAxialPaint(_shading, colors, positions);
    }

    /// <summary>
    /// Builds the radial gradient paints for the given number of function samples.
    /// Returns <see langword="null"/> when the shading data is invalid.
    /// </summary>
    private RadialShadingPaints CreateRadialPaints(int defaultFunctionSamples)
    {
        _builder.BuildShadingColorsAndStops(
            _shading,
            _context.Converter,
            _context.RenderingIntent,
            _context.FullTransferFunction,
            defaultFunctionSamples,
            out var colors,
            out var positions);

        if (colors == null || colors.Length == 0)
        {
            return null;
        }

        return _builder.BuildRadialPaints(_shading, colors, positions);
    }

    /// <summary>
    /// Clones the given base paint, applies antialias and modifiers, then draws it onto the canvas.
    /// </summary>
    private static void DrawPaintToCanvas(SKCanvas canvas, SKPaint basePaint, IEnumerable<IPdfCommandModifier> modifiers, bool antialias)
    {
        using var paint = basePaint.Clone();
        paint.IsAntialias = antialias;
        foreach (var modifier in modifiers)
        {
            modifier.ModifyPaint(paint);
        }

        canvas.DrawPaint(paint);
    }

    /// <summary>
    /// Creates a shader paint, applies antialias and modifiers, then draws the vertices onto the canvas.
    /// </summary>
    private static void DrawVerticesToCanvas(SKCanvas canvas, SKVertices vertices, IEnumerable<IPdfCommandModifier> modifiers, bool antialias)
    {
        using var paint = PdfPaintFactory.CreateShaderPaint();
        paint.IsAntialias = antialias;
        foreach (var modifier in modifiers)
        {
            modifier.ModifyPaint(paint);
        }

        canvas.DrawVertices(vertices, SKBlendMode.DstIn, paint);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _functionCache?.Dispose();
        _functionCache = null;

        _axialCache?.Dispose();
        _axialCache = null;

        _radialCache?.Dispose();
        _radialCache = null;

        _gouraudCache?.Dispose();
        _gouraudCache = null;

        _patchMeshCache?.Dispose();
        _patchMeshCache = null;

        base.Dispose(disposing);
    }
}
