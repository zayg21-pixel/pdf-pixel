using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Shading;
using PdfPixel.Shading.Model;
using SkiaSharp;

namespace PdfPixel.Commands;

/// <summary>
/// Draws PDF shading. Initialize prepares expensive data (function sampling, mesh decoding,
/// gradient construction). Execute draws the prepared results to the canvas.
/// Caches expensive results and only rebuilds when the relevant rendering parameter changes.
/// </summary>
public sealed class DrawShadingCommand : PdfCommand
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
    /// Initializes a new instance of the <see cref="DrawShadingCommand"/> class.
    /// </summary>
    /// <param name="shading">Parsed shading model.</param>
    /// <param name="context">Snapshot of graphics-state values captured at record time.</param>
    /// <param name="loggerFactory">Logger factory for diagnostic output.</param>
    public DrawShadingCommand(PdfShading shading, ShadingDecodingContext context, ILoggerFactory loggerFactory)
    {
        _shading = shading;
        _context = context;
        _builder = new PdfShadingBuilder(loggerFactory);
    }

    /// <inheritdoc />
    public override void Initialize(PdfCommandExecutionContext executionContext)
    {
        lock (executionContext.ContentLocker)
        {
            switch (_shading.ShadingType)
            {
                case PdfShadingType.FunctionBased:
                {
                    InitializeFunctionBased(executionContext);
                    break;
                }
                case PdfShadingType.Axial:
                {
                    InitializeAxial(executionContext);
                    break;
                }
                case PdfShadingType.Radial:
                {
                    InitializeRadial(executionContext);
                    break;
                }
                case PdfShadingType.FreeFormGouraud:
                case PdfShadingType.LatticeFormGouraud:
                {
                    InitializeGouraud();
                    break;
                }
                case PdfShadingType.CoonsPatchMesh:
                case PdfShadingType.TensorProductPatchMesh:
                {
                    InitializePatchMesh(executionContext);
                    break;
                }
            }
        }
    }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
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
                ExecuteAxial(executionContext);
                break;
            }
            case PdfShadingType.Radial:
            {
                ExecuteRadial(executionContext);
                break;
            }
            case PdfShadingType.FreeFormGouraud:
            case PdfShadingType.LatticeFormGouraud:
            {
                ExecuteGouraud(executionContext);
                break;
            }
            case PdfShadingType.CoonsPatchMesh:
            case PdfShadingType.TensorProductPatchMesh:
            {
                ExecutePatchMesh(executionContext);
                break;
            }
        }
    }

    private void InitializeFunctionBased(PdfCommandExecutionContext executionContext)
    {
        int defaultFunctionSamples = executionContext.Parameters.DefaultFunctionSamples;

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
    }

    private void InitializeAxial(PdfCommandExecutionContext executionContext)
    {
        int defaultFunctionSamples = executionContext.Parameters.DefaultFunctionSamples;

        if (_axialCacheSamples != defaultFunctionSamples)
        {
            _axialCache?.Dispose();
            _axialCache = CreateAxialPaint(defaultFunctionSamples);
            _axialCacheSamples = defaultFunctionSamples;
        }
    }

    private void InitializeRadial(PdfCommandExecutionContext executionContext)
    {
        int defaultFunctionSamples = executionContext.Parameters.DefaultFunctionSamples;

        if (_radialCacheSamples != defaultFunctionSamples)
        {
            _radialCache?.Dispose();
            _radialCache = CreateRadialPaints(defaultFunctionSamples);
            _radialCacheSamples = defaultFunctionSamples;
        }
    }

    private void InitializeGouraud()
    {
        if (!_gouraudCacheBuilt)
        {
            Color.Sampling.ColorTransformSampler sampler = _context.Converter.GetRgbaSampler(_context.RenderingIntent, _context.FullTransferFunction);
            _gouraudCache = _builder.BuildGouraudVertices(_shading, sampler);
            _gouraudCacheBuilt = true;
        }
    }

    private void InitializePatchMesh(PdfCommandExecutionContext executionContext)
    {
        int maxTessellationVertices = executionContext.Parameters.MaxTessellationVertices;

        if (_patchMeshCacheMaxVertices != maxTessellationVertices)
        {
            _patchMeshCache?.Dispose();
            Color.Sampling.ColorTransformSampler sampler = _context.Converter.GetRgbaSampler(_context.RenderingIntent, _context.FullTransferFunction);
            _patchMeshCache = _builder.BuildPatchMeshVertices(_shading, sampler, maxTessellationVertices, executionContext.ExecutionObserver);
            _patchMeshCacheMaxVertices = maxTessellationVertices;
        }
    }

    private void ExecuteFunctionBased(PdfCommandExecutionContext executionContext)
    {
        if (_functionCache == null)
        {
            return;
        }

        executionContext.Canvas.Save();
        executionContext.Canvas.Concat(_functionCache.Matrix);
        executionContext.Canvas.DrawBitmap(_functionCache.Bitmap, SKPoint.Empty, SKSamplingOptions.Default);
        executionContext.Canvas.Restore();
    }

    private void ExecuteAxial(PdfCommandExecutionContext executionContext)
    {
        if (_axialCache == null)
        {
            return;
        }

        DrawPaintToCanvas(executionContext, _axialCache);
    }

    private void ExecuteRadial(PdfCommandExecutionContext executionContext)
    {
        if (_radialCache == null)
        {
            return;
        }

        DrawPaintToCanvas(executionContext, _radialCache.InnerPaint);
        DrawPaintToCanvas(executionContext, _radialCache.OuterPaint);
    }

    private void ExecuteGouraud(PdfCommandExecutionContext executionContext)
    {
        if (_gouraudCache == null)
        {
            return;
        }

        DrawVerticesToCanvas(executionContext, _gouraudCache);
    }

    private void ExecutePatchMesh(PdfCommandExecutionContext executionContext)
    {
        if (_patchMeshCache == null)
        {
            return;
        }

        DrawVerticesToCanvas(executionContext, _patchMeshCache);
    }

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

    private void DrawPaintToCanvas(PdfCommandExecutionContext executionContext, SKPaint basePaint)
    {
        using SKPaint paint = basePaint.Clone();
        paint.Color = PdfPaintFactory.ApplyAlpha(paint.Color, _context.FillAlpha);
        paint.IsAntialias = executionContext.Parameters.Antialias;

        CommandHelpers.ApplyModifiers(paint, executionContext);

        executionContext.Canvas.DrawPaint(paint);
    }

    private void DrawVerticesToCanvas(PdfCommandExecutionContext executionContext, SKVertices vertices)
    {
        using SKPaint paint = PdfPaintFactory.CreateShaderPaint();
        paint.Color = PdfPaintFactory.ApplyAlpha(paint.Color, _context.FillAlpha);
        paint.IsAntialias = executionContext.Parameters.Antialias;

        CommandHelpers.ApplyModifiers(paint, executionContext);

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
