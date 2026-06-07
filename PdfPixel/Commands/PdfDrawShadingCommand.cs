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
    private FunctionShadingResult? _functionCache;
    private int _functionCacheSamples = -1;

    // Axial (Type 2) cache
    private SKPaint? _axialCache;
    private int _axialCacheSamples = -1;

    // Radial (Type 3) cache
    private RadialShadingPaints? _radialCache;
    private int _radialCacheSamples = -1;

    // Gouraud (Type 4/5) cache
    private SKVertices? _gouraudCache;
    private bool _gouraudCacheBuilt;

    // Patch mesh (Type 6/7) cache
    private SKVertices? _patchMeshCache;
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
    public override void Execute(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        lock (executionContext.ContentLocker)
        {
            switch (_shading.ShadingType)
            {
                case PdfShadingType.FunctionBased:
                    {
                        ExecuteFunctionBased(executionContext);
                        break;
                    }
                case PdfShadingType.Axial:
                    {
                        ExecuteAxial(executionContext, modifiers);
                        break;
                    }
                case PdfShadingType.Radial:
                    {
                        ExecuteRadial(executionContext, modifiers);
                        break;
                    }
                case PdfShadingType.FreeFormGouraud:
                case PdfShadingType.LatticeFormGouraud:
                    {
                        ExecuteGouraud(executionContext, modifiers);
                        break;
                    }
                case PdfShadingType.CoonsPatchMesh:
                case PdfShadingType.TensorProductPatchMesh:
                    {
                        ExecutePatchMesh(executionContext, modifiers);
                        break;
                    }
            }
        }
    }

    /// <summary>
    /// Executes function-based (Type 1) shading by rendering a cached sampled bitmap.
    /// Rebuilds the bitmap when the default function samples rendering parameter changes.
    /// </summary>
    private void ExecuteFunctionBased(PdfCommandExecutionContext executionContext)
    {
        int defaultFunctionSamples = executionContext.RenderingParameters.DefaultFunctionSamples;

        if (_functionCacheSamples != defaultFunctionSamples)
        {
            _functionCache?.Dispose();
            _functionCache = _builder.BuildFunctionBasedBitmap(
                _shading,
                _context.Converter,
                _context.RenderingIntent,
                _context.FullTransferFunction,
                defaultFunctionSamples,
                executionContext.ExecutionObserver);
            _functionCacheSamples = defaultFunctionSamples;
        }

        if (_functionCache == null)
        {
            return;
        }

        executionContext.Canvas.Save();
        executionContext.Canvas.Concat(_functionCache.Matrix);
        executionContext.Canvas.DrawBitmap(_functionCache.Bitmap, SKPoint.Empty);
        executionContext.Canvas.Restore();
    }

    /// <summary>
    /// Executes axial (Type 2) shading by drawing a cached linear gradient paint.
    /// Rebuilds the paint when the default function samples rendering parameter changes.
    /// </summary>
    private void ExecuteAxial(PdfCommandExecutionContext executionContext, IEnumerable<IPdfCommandModifier> modifiers)
    {
        int defaultFunctionSamples = executionContext.RenderingParameters.DefaultFunctionSamples;

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

        DrawPaintToCanvas(executionContext, _axialCache, modifiers);
    }

    /// <summary>
    /// Executes radial (Type 3) shading by drawing cached inner and outer conical gradient paints.
    /// Rebuilds the paints when the default function samples rendering parameter changes.
    /// </summary>
    private void ExecuteRadial(PdfCommandExecutionContext executionContext, IEnumerable<IPdfCommandModifier> modifiers)
    {
        int defaultFunctionSamples = executionContext.RenderingParameters.DefaultFunctionSamples;

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

        DrawPaintToCanvas(executionContext, _radialCache.InnerPaint, modifiers);
        DrawPaintToCanvas(executionContext, _radialCache.OuterPaint, modifiers);
    }

    /// <summary>
    /// Executes Gouraud-shaded triangle mesh (Type 4/5) shading.
    /// The mesh has no parameter dependency and is built once.
    /// </summary>
    private void ExecuteGouraud(PdfCommandExecutionContext executionContext, IEnumerable<IPdfCommandModifier> modifiers)
    {
        if (!_gouraudCacheBuilt)
        {
            Color.Sampling.ColorTransformSampler sampler = _context.Converter.GetRgbaSampler(_context.RenderingIntent, _context.FullTransferFunction);
            _gouraudCache = _builder.BuildGouraudVertices(_shading, sampler);
            _gouraudCacheBuilt = true;
        }

        if (_gouraudCache == null)
        {
            return;
        }

        DrawVerticesToCanvas(executionContext, _gouraudCache, modifiers);
    }

    /// <summary>
    /// Executes Coons (Type 6) or Tensor-product (Type 7) patch mesh shading.
    /// Rebuilds the mesh when the max tessellation vertices rendering parameter changes.
    /// </summary>
    private void ExecutePatchMesh(PdfCommandExecutionContext executionContext, IEnumerable<IPdfCommandModifier> modifiers)
    {
        int maxTessellationVertices = executionContext.RenderingParameters.MaxTessellationVertices;

        if (_patchMeshCacheMaxVertices != maxTessellationVertices)
        {
            _patchMeshCache?.Dispose();
            Color.Sampling.ColorTransformSampler sampler = _context.Converter.GetRgbaSampler(_context.RenderingIntent, _context.FullTransferFunction);
            _patchMeshCache = _builder.BuildPatchMeshVertices(_shading, sampler, maxTessellationVertices, executionContext.ExecutionObserver);
            _patchMeshCacheMaxVertices = maxTessellationVertices;
        }

        if (_patchMeshCache == null)
        {
            return;
        }

        DrawVerticesToCanvas(executionContext, _patchMeshCache, modifiers);
    }

    /// <summary>
    /// Builds the axial gradient paint for the given number of function samples.
    /// Returns <see langword="null"/> when the shading data is invalid.
    /// </summary>
    private SKPaint? CreateAxialPaint(int defaultFunctionSamples)
    {
        _builder.BuildShadingColorsAndStops(
            _shading,
            _context.Converter,
            _context.RenderingIntent,
            _context.FullTransferFunction,
            defaultFunctionSamples,
            out SKColor[] colors,
            out float[] positions);

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
    private RadialShadingPaints? CreateRadialPaints(int defaultFunctionSamples)
    {
        _builder.BuildShadingColorsAndStops(
            _shading,
            _context.Converter,
            _context.RenderingIntent,
            _context.FullTransferFunction,
            defaultFunctionSamples,
            out SKColor[] colors,
            out float[] positions);

        if (colors == null || colors.Length == 0)
        {
            return null;
        }

        return _builder.BuildRadialPaints(_shading, colors, positions);
    }

    /// <summary>
    /// Clones the given base paint, applies antialias and modifiers, then draws it onto the canvas.
    /// </summary>
    private void DrawPaintToCanvas(PdfCommandExecutionContext executionContext, SKPaint basePaint, IEnumerable<IPdfCommandModifier> modifiers)
    {
        using SKPaint paint = basePaint.Clone();
        paint.Color = PdfPaintFactory.ApplyAlpha(paint.Color, _context.FillAlpha);
        paint.IsAntialias = executionContext.RenderingParameters.Antialias;

        CommandHelpers.ApplyModifiers(paint, modifiers);

        executionContext.Canvas.DrawPaint(paint);
    }

    /// <summary>
    /// Creates a shader paint, applies antialias and modifiers, then draws the vertices onto the canvas.
    /// </summary>
    private void DrawVerticesToCanvas(PdfCommandExecutionContext executionContext, SKVertices vertices, IEnumerable<IPdfCommandModifier> modifiers)
    {
        using SKPaint paint = PdfPaintFactory.CreateShaderPaint();
        paint.Color = PdfPaintFactory.ApplyAlpha(paint.Color, _context.FillAlpha);
        paint.IsAntialias = executionContext.RenderingParameters.Antialias;

        CommandHelpers.ApplyModifiers(paint, modifiers);

        executionContext.Canvas.DrawVertices(vertices, SKBlendMode.DstIn, paint);
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
    }
}
