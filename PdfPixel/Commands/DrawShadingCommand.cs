using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Commands.Cache;
using PdfPixel.Shading;
using PdfPixel.Shading.Model;
using SkiaSharp;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Commands;

/// <summary>
/// Draws PDF shading. Builds expensive data (function sampling, mesh decoding, gradient construction)
/// once per shading and reuses the result from <see cref="PdfCommandExecutionContext.Cache"/> for every
/// other command instance drawing the same shading during the same execution.
/// </summary>
public sealed class DrawShadingCommand : PdfCommand
{
    private readonly PdfShading _shading;
    private readonly ShadingDecodingContext _context;
    private readonly PdfShadingBuilder _builder;
    private readonly Color.Sampling.ColorTransformSampler _sampler;

    /// <summary>
    /// Initializes a new instance of the <see cref="DrawShadingCommand"/> class.
    /// </summary>
    /// <param name="context">Snapshot of graphics-state values captured at record time.</param>
    /// <param name="loggerFactory">Logger factory for diagnostic output.</param>
    public DrawShadingCommand(ShadingDecodingContext context, ILoggerFactory loggerFactory)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _shading = _context.Shading;
        _builder = new PdfShadingBuilder(loggerFactory);
        _sampler = _context.Converter.GetRgbaSampler(_context.RenderingIntent, _context.FullTransferFunction);
    }

    /// <inheritdoc />
    public override void Execute(PdfCommandExecutionContext executionContext)
    {
        ShadingCommandCacheEntry entry = GetOrBuildEntry(executionContext);

        switch (_shading.ShadingType)
        {
            case PdfShadingType.FunctionBased:
            {
                ExecuteFunctionBased(executionContext, entry);
                break;
            }
            case PdfShadingType.Axial:
            {
                ExecuteAxial(executionContext, entry);
                break;
            }
            case PdfShadingType.Radial:
            {
                ExecuteRadial(executionContext, entry);
                break;
            }
            case PdfShadingType.FreeFormGouraud:
            case PdfShadingType.LatticeFormGouraud:
            {
                ExecuteGouraud(executionContext, entry);
                break;
            }
            case PdfShadingType.CoonsPatchMesh:
            case PdfShadingType.TensorProductPatchMesh:
            {
                ExecutePatchMesh(executionContext, entry);
                break;
            }
        }
    }

    private ShadingCommandCacheEntry GetOrBuildEntry(PdfCommandExecutionContext executionContext)
    {
        ShadingCommandCacheKey key = new(_context);

        lock (executionContext.ContentLocker)
        {
            if (executionContext.Cache.GetEntry(key) is ShadingCommandCacheEntry existing && existing.ParametersMatches(executionContext.Parameters))
            {
                return existing;
            }

            ShadingCommandCacheEntry entry = BuildEntry(executionContext);
            executionContext.Cache.StoreEntry(key, entry);
            return entry;
        }
    }

    private ShadingCommandCacheEntry BuildEntry(PdfCommandExecutionContext executionContext)
    {
        ShadingCommandCacheEntry entry = new();

        switch (_shading.ShadingType)
        {
            case PdfShadingType.FunctionBased:
            {
                entry.FunctionSamples = executionContext.Parameters.DefaultFunctionSamples;
                entry.Function = _builder.BuildFunctionBasedBitmap(
                    _shading,
                    _sampler,
                    executionContext.Parameters.DefaultFunctionSamples,
                    executionContext.ExecutionObserver);
                break;
            }
            case PdfShadingType.Axial:
            {
                entry.FunctionSamples = executionContext.Parameters.DefaultFunctionSamples;
                ShadingColorStops axialColorStops = _builder.BuildShadingColorsAndStops(_shading, _sampler, executionContext.Parameters.DefaultFunctionSamples);
                entry.Axial = _builder.BuildAxialPaint(_shading, axialColorStops);
                break;
            }
            case PdfShadingType.Radial:
            {
                entry.FunctionSamples = executionContext.Parameters.DefaultFunctionSamples;
                ShadingColorStops radialColorStops = _builder.BuildShadingColorsAndStops(_shading, _sampler, executionContext.Parameters.DefaultFunctionSamples);
                entry.Radial = _builder.BuildRadialPaints(_shading, radialColorStops);
                break;
            }
            case PdfShadingType.FreeFormGouraud:
            case PdfShadingType.LatticeFormGouraud:
            {
                entry.Gouraud = _builder.BuildGouraudVertices(_shading, _sampler);
                break;
            }
            case PdfShadingType.CoonsPatchMesh:
            case PdfShadingType.TensorProductPatchMesh:
            {
                entry.TessellationVertices = executionContext.Parameters.MaxTessellationVertices;
                entry.PatchMesh = _builder.BuildPatchMeshVertices(_shading, _sampler, executionContext.Parameters.MaxTessellationVertices, executionContext.ExecutionObserver);
                break;
            }
        }

        return entry;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExecuteFunctionBased(PdfCommandExecutionContext executionContext, ShadingCommandCacheEntry entry)
    {
        if (entry.Function == null)
        {
            return;
        }

        executionContext.Canvas.Save();
        executionContext.Canvas.Concat(entry.Function.Matrix);
        executionContext.Canvas.DrawBitmap(entry.Function.Bitmap, SKPoint.Empty, SKSamplingOptions.Default);
        executionContext.Canvas.Restore();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExecuteAxial(PdfCommandExecutionContext executionContext, ShadingCommandCacheEntry entry)
    {
        if (entry.Axial == null)
        {
            return;
        }

        DrawPaintToCanvas(executionContext, entry.Axial);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExecuteRadial(PdfCommandExecutionContext executionContext, ShadingCommandCacheEntry entry)
    {
        if (entry.Radial == null)
        {
            return;
        }

        DrawPaintToCanvas(executionContext, entry.Radial.InnerPaint);
        DrawPaintToCanvas(executionContext, entry.Radial.OuterPaint);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExecuteGouraud(PdfCommandExecutionContext executionContext, ShadingCommandCacheEntry entry)
    {
        if (entry.Gouraud == null)
        {
            return;
        }

        DrawVerticesToCanvas(executionContext, entry.Gouraud);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExecutePatchMesh(PdfCommandExecutionContext executionContext, ShadingCommandCacheEntry entry)
    {
        if (entry.PatchMesh == null)
        {
            return;
        }

        DrawVerticesToCanvas(executionContext, entry.PatchMesh);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DrawPaintToCanvas(PdfCommandExecutionContext executionContext, SKPaint basePaint)
    {
        using SKPaint paint = basePaint.Clone();
        paint.Color = PdfPaintFactory.ApplyAlpha(paint.Color, _context.FillAlpha);
        paint.IsAntialias = executionContext.Parameters.Antialias;

        CommandHelpers.ApplyModifiers(paint, executionContext);

        executionContext.Canvas.DrawPaint(paint);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
    }
}
