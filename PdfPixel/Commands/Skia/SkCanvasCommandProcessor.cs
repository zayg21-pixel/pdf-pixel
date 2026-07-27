using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Commands.Cache;
using PdfPixel.Commands.Converters;
using PdfPixel.Commands.Image;
using PdfPixel.Fonts.Model;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing;
using PdfPixel.Models;
using PdfPixel.Shading.Model;
using PdfPixel.Text;
using PdfPixel.TextExtraction;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Commands.Skia;

/// <summary>
/// Executes PDF drawing commands against an explicit <see cref="SKCanvas"/> by dispatching on
/// <see cref="IPdfCommand.Kind"/> instead of calling the command's own execution logic. Commands
/// are treated purely as storage; every draw operation below is this processor's own copy of the
/// logic that used to live in each command's <c>Execute</c> method.
/// </summary>
public sealed class SkCanvasCommandProcessor : IPdfCommandProcessor
{
    private const float DrawShapedTextScaleTolerancePercent = 0.01f; // 1%

    private readonly SKCanvas _canvas;
    private readonly PdfCommandExecutionContext _executionContext;
    private readonly ILogger<SkCanvasCommandProcessor> _logger;

    /// <summary>
    /// Initializes the processor with the canvas to draw on, the execution context to draw with, and a logger.
    /// </summary>
    public SkCanvasCommandProcessor(SKCanvas canvas, PdfCommandExecutionContext executionContext, ILogger<SkCanvasCommandProcessor> logger)
    {
        _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public void Process(IPdfCommand command)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (!_executionContext.MarkedContent.ShouldExecute(command))
        {
            return;
        }

        try
        {
            ProcessInternal(command);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception exception)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            _logger.LogWarning(exception, "Command {CommandType} failed during execution.", command.GetType().Name);
        }
    }

    private void ProcessInternal(IPdfCommand command)
    {
        switch (command.Kind)
        {
            case PdfCommandKind.BeginMarkedContent:
            {
                _executionContext.MarkedContent.Push(((BeginMarkedContentCommand)command).MarkedContent);
                break;
            }
            case PdfCommandKind.ClipPath:
            {
                ExecuteClipPath((ClipPathCommand)command);
                break;
            }
            case PdfCommandKind.ClipRectangle:
            {
                ExecuteClipRectangle((ClipRectangleCommand)command);
                break;
            }
            case PdfCommandKind.ConcatMatrix:
            {
                var concatMatrixCommand = (ConcatMatrixCommand)command;
                _canvas.Concat(concatMatrixCommand.Matrix.ToSkMatrix());
                _executionContext.Frames.OnConcatMatrix(concatMatrixCommand.Matrix);
                break;
            }
            case PdfCommandKind.DrawNormalImageTile:
            {
                ExecuteDrawNormalImageTile((DrawNormalImageTileCommand)command);
                break;
            }
            case PdfCommandKind.DrawPath:
            {
                ExecuteDrawPath((DrawPathCommand)command);
                break;
            }
            case PdfCommandKind.DrawRecording:
            {
                ExecuteDrawRecording((DrawRecordingCommand)command);
                break;
            }
            case PdfCommandKind.DrawShading:
            {
                ExecuteDrawShading((DrawShadingCommand)command);
                break;
            }
            case PdfCommandKind.DrawShapedText:
            {
                ExecuteDrawShapedText((DrawShapedTextCommand)command);
                break;
            }
            case PdfCommandKind.DrawSoftMaskImageTile:
            {
                ExecuteDrawSoftMaskImageTile((DrawSoftMaskImageTileCommand)command);
                break;
            }
            case PdfCommandKind.DrawStencilMaskImageTile:
            {
                ExecuteDrawStencilMaskImageTile((DrawStencilMaskImageTileCommand)command);
                break;
            }
            case PdfCommandKind.DrawText:
            {
                ExecuteDrawText((DrawTextCommand)command);
                break;
            }
            case PdfCommandKind.DrawTiling:
            {
                ExecuteDrawTiling((DrawTilingCommand)command);
                break;
            }
            case PdfCommandKind.EndMarkedContent:
            {
                _executionContext.MarkedContent.Pop();
                break;
            }
            case PdfCommandKind.InitializeTileCache:
            {
                ExecuteInitializeTileCache((InitializeTileCacheCommand)command);
                break;
            }
            case PdfCommandKind.RestoreLayer:
            {
                _canvas.Restore();
                _executionContext.Frames.OnRestoreState();
                break;
            }
            case PdfCommandKind.RestoreState:
            {
                _canvas.Restore();
                _executionContext.Frames.OnRestoreState();
                break;
            }
            case PdfCommandKind.SaveLayer:
            {
                ExecuteSaveLayer((SaveLayerCommand)command);
                break;
            }
            case PdfCommandKind.SaveState:
            {
                _canvas.Save();
                _executionContext.Frames.OnSaveState();
                break;
            }
            case PdfCommandKind.TextCharacters:
            {
                ExecuteTextCharacters((TextCharactersCommand)command);
                break;
            }
        }
    }

    private void ExecuteClipPath(ClipPathCommand command)
    {
        bool antialias = CommandHelpers.GetPathIsAntialias(command.Path, _executionContext, command.Paint);

        using SKPath sourcePath = command.Path.ToSkPath();
        using SKPaint? strokePaint = BuildClipStrokePaint(command);
        using SKPath clipPath = BuildClipPathGeometry(sourcePath, strokePaint);
        SKClipOperation skOperation = command.Operation.ToSkClipOperation();

        _canvas.ClipPath(clipPath, skOperation, antialias);

        if (strokePaint != null && command.Paint != null)
        {
            _executionContext.Frames.OnClipStrokePath(command.Path, command.Operation, command.Paint);
        }
        else
        {
            _executionContext.Frames.OnClipPath(command.Path, command.Operation);
        }
    }

    private SKPaint? BuildClipStrokePaint(ClipPathCommand command)
    {
        if (command.Paint == null || command.Paint.Style != PdfPaintStyle.Stroke)
        {
            return null;
        }

        SKPaint strokePaint = command.Paint.ToSkiaPaint();
        strokePaint.StrokeWidth = CommandHelpers.GetMinimumStrokeWidth(_executionContext, command.Paint);
        return strokePaint;
    }

    private static SKPath BuildClipPathGeometry(SKPath sourcePath, SKPaint? strokePaint)
    {
        if (strokePaint == null)
        {
            return new SKPath(sourcePath);
        }

        return strokePaint.GetFillPath(sourcePath) ?? new SKPath(sourcePath);
    }

    private void ExecuteClipRectangle(ClipRectangleCommand command)
    {
        bool antialias = CommandHelpers.GetRectIsAntialias(command.Rect, _executionContext);
        PdfRectangle snappedRect = CommandHelpers.GetPixelSnappedRect(command.Rect, _executionContext);
        SKClipOperation skOperation = command.Operation.ToSkClipOperation();
        _canvas.ClipRect(snappedRect.ToSkRect(), skOperation, antialias);
        _executionContext.Frames.OnClipRect(command.Rect, command.Operation);
    }

    private void ExecuteDrawNormalImageTile(DrawNormalImageTileCommand command)
    {
        NormalImageExecutionContext context = command.Context;
        PdfImageTile tile = context.TileCache.GetNextTile(_executionContext.ExecutionObserver);

        if (tile.IsSkipped || tile.Image == null)
        {
            return;
        }

        SnappedTilePlacement placement = PdfImageCommandUtilities.GetSnappedTilePlacement(
            _executionContext, context.ImageSize, tile.TilePosition, context.Interpolate);

        using SKImage skImage = tile.Image.ToSkImage();
        using SKPaint paint = SkiaCommandUtilities.GetBaseImagePaint(context.DecodingContext);
        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);

        _canvas.Save();
        _canvas.Concat(placement.PlacementMatrix.ToSkMatrix());
        _canvas.DrawImage(skImage, placement.PlacementRectangle.ToSkRect(), SkiaCommandUtilities.GetSamplingOptions(placement.Interpolate), paint);
        _canvas.Restore();
    }

    private void ExecuteDrawPath(DrawPathCommand command)
    {
        using SKPath path = command.Path.ToSkPath();
        using SKPaint paint = command.Paint.ToSkiaPaint();
        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);
        paint.IsAntialias = CommandHelpers.GetPathIsAntialias(command.Path, _executionContext, command.Paint);
        paint.StrokeWidth = CommandHelpers.GetMinimumStrokeWidth(_executionContext, command.Paint);

        _canvas.DrawPath(path, paint);
    }

    private void ExecuteDrawRecording(DrawRecordingCommand command)
    {
        UncoloredPaintModifier? previousModifier = _executionContext.UncoloredModifier;

        if (command.Modifier != null)
        {
            _executionContext.UncoloredModifier = command.Modifier;
        }

        _canvas.Save();
        _executionContext.Frames.OnSaveState();
        _canvas.Concat(command.Matrix.ToSkMatrix());
        _executionContext.Frames.OnConcatMatrix(command.Matrix);

        int savesCountAfterOwnSave = _executionContext.Frames.SavesCount;

        ReplayRecorder(command.Recorder);

        BalanceFrames(savesCountAfterOwnSave);

        _canvas.Restore();
        _executionContext.Frames.OnRestoreState();

        _executionContext.UncoloredModifier = previousModifier;
    }

    private void ReplayRecorder(PdfCommandRecorder recorder)
    {
        foreach (IPdfCommand recordedCommand in recorder.Commands)
        {
            if (!_executionContext.MarkedContent.ShouldExecute(recordedCommand))
            {
                continue;
            }

            try
            {
                ProcessInternal(recordedCommand);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                _logger.LogWarning(exception, "Command {CommandType} failed during replay.", recordedCommand.GetType().Name);
                continue;
            }

            _executionContext.ExecutionObserver?.Notify();
        }
    }

    private void BalanceFrames(int savesCountBefore)
    {
        PdfCommandExecutionFrames frames = _executionContext.Frames;

        while (frames.SavesCount > savesCountBefore)
        {
            _canvas.Restore();
            frames.OnRestoreState();
        }

        while (frames.SavesCount < savesCountBefore)
        {
            _canvas.Save();
            frames.OnSaveState();
        }
    }

    private void ExecuteDrawShading(DrawShadingCommand command)
    {
        ShadingCommandCacheEntry entry = GetOrBuildShadingEntry(command);

        switch (command.Context.Shading.ShadingType)
        {
            case PdfShadingType.FunctionBased:
            {
                ExecuteShadingFunctionBased(entry);
                break;
            }
            case PdfShadingType.Axial:
            {
                ExecuteShadingAxial(command, entry);
                break;
            }
            case PdfShadingType.Radial:
            {
                ExecuteShadingRadial(command, entry);
                break;
            }
            case PdfShadingType.FreeFormGouraud:
            case PdfShadingType.LatticeFormGouraud:
            {
                ExecuteShadingGouraud(command, entry);
                break;
            }
            case PdfShadingType.CoonsPatchMesh:
            case PdfShadingType.TensorProductPatchMesh:
            {
                ExecuteShadingPatchMesh(command, entry);
                break;
            }
        }
    }

    private ShadingCommandCacheEntry GetOrBuildShadingEntry(DrawShadingCommand command)
    {
        ShadingCommandCacheKey key = new(command.Context);

        lock (_executionContext.ContentLocker)
        {
            if (_executionContext.Cache.GetEntry(key) is ShadingCommandCacheEntry existing && existing.ParametersMatches(_executionContext.Parameters))
            {
                return existing;
            }

            ShadingCommandCacheEntry entry = command.BuildEntry(_executionContext);
            _executionContext.Cache.StoreEntry(key, entry);
            return entry;
        }
    }

    private void ExecuteShadingFunctionBased(ShadingCommandCacheEntry entry)
    {
        if (entry.Function == null)
        {
            return;
        }

        using SKImage image = PdfImageConverter.ToSkImage(entry.Function.Image);

        _canvas.Save();
        _canvas.Concat(entry.Function.Matrix.ToSkMatrix());
        _canvas.DrawImage(image, SKPoint.Empty, SKSamplingOptions.Default);
        _canvas.Restore();
    }

    private void ExecuteShadingAxial(DrawShadingCommand command, ShadingCommandCacheEntry entry)
    {
        if (entry.Axial == null)
        {
            return;
        }

        using SKPaint paint = entry.Axial.ToSkiaPaint();
        DrawShadingPaintToCanvas(command, paint);
    }

    private void ExecuteShadingRadial(DrawShadingCommand command, ShadingCommandCacheEntry entry)
    {
        if (entry.Radial == null)
        {
            return;
        }

        using SKPaint innerPaint = entry.Radial.ToSkiaInnerPaint();
        DrawShadingPaintToCanvas(command, innerPaint);

        using SKPaint outerPaint = entry.Radial.ToSkiaOuterPaint();
        DrawShadingPaintToCanvas(command, outerPaint);
    }

    private void ExecuteShadingGouraud(DrawShadingCommand command, ShadingCommandCacheEntry entry)
    {
        if (entry.Gouraud == null)
        {
            return;
        }

        DrawShadingVerticesToCanvas(command, entry.Gouraud);
    }

    private void ExecuteShadingPatchMesh(DrawShadingCommand command, ShadingCommandCacheEntry entry)
    {
        if (entry.PatchMesh == null)
        {
            return;
        }

        DrawShadingVerticesToCanvas(command, entry.PatchMesh);
    }

    private void DrawShadingPaintToCanvas(DrawShadingCommand command, SKPaint paint)
    {
        paint.Color = SkiaCommandUtilities.ApplyAlpha(paint.Color, command.Context.FillAlpha);
        paint.IsAntialias = _executionContext.Parameters.Antialias;

        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);

        _canvas.DrawPaint(paint);
    }

    private void DrawShadingVerticesToCanvas(DrawShadingCommand command, PdfVertices vertices)
    {
        using SKPaint paint = CreateShadingVerticesPaint(command);

        using SKVertices skVertices = PdfShadingConverter.ToSkVertices(vertices);
        _canvas.DrawVertices(skVertices, SKBlendMode.DstIn, paint);
    }

    private SKPaint CreateShadingVerticesPaint(DrawShadingCommand command)
    {
        SKPaint paint = new()
        {
            Color = SkiaCommandUtilities.ApplyAlpha(SKColors.Black, command.Context.FillAlpha),
            IsAntialias = _executionContext.Parameters.Antialias
        };

        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);
        return paint;
    }

    private void ExecuteInitializeTileCache(InitializeTileCacheCommand command)
    {
        PdfMatrix ctm = PdfImageCommandUtilities.GetImageCtm(CommandHelpers.GetScaledMatrix(_executionContext));
        PdfIntegerRectangle imageRegion = PdfImageCommandUtilities.ComputeImageRegionOfInterest(command.ImageSize, _executionContext);

        command.TileCache.Initialize(ctm, imageRegion, _executionContext.ContentLocker, _executionContext.ExecutionObserver);
    }

    private void ExecuteDrawShapedText(DrawShapedTextCommand command)
    {
        ReadOnlySpan<ShapedGlyph> glyphs = command.ShapingResult.Span;
        if (glyphs.Length == 0)
        {
            return;
        }

        using SKPaint paint = command.Paint.ToSkiaPaint();
        bool antialias = _executionContext.Parameters.Antialias;
        paint.IsAntialias = antialias;
        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);

        _canvas.Save();
        _canvas.Concat(command.Matrix.ToSkMatrix());

        DrawShapedTextSpans(paint, antialias, glyphs);

        _canvas.Restore();
    }

    private void DrawShapedTextSpans(SKPaint paint, bool antialias, in ReadOnlySpan<ShapedGlyph> glyphs)
    {
        int spanStart = 0;
        IPdfTypeface currentTypeface = glyphs[0].CharacterInfo.Typeface;
        float currentScale = glyphs[0].Scale;

        for (int i = 1; i < glyphs.Length; i++)
        {
            ShapedGlyph glyph = glyphs[i];
            IPdfTypeface typeface = glyph.CharacterInfo.Typeface;

            if (typeface != currentTypeface || Math.Abs(glyph.Scale - currentScale) / currentScale >= DrawShapedTextScaleTolerancePercent)
            {
                DrawShapedTextSpan(paint, antialias, glyphs.Slice(spanStart, i - spanStart), currentTypeface, currentScale);

                spanStart = i;
                currentTypeface = typeface;
                currentScale = glyph.Scale;
            }
        }

        DrawShapedTextSpan(paint, antialias, glyphs.Slice(spanStart), currentTypeface, currentScale);
    }

    private void DrawShapedTextSpan(SKPaint paint, bool antialias, in ReadOnlySpan<ShapedGlyph> span, IPdfTypeface typeface, float scale)
    {
        SKTypeface skTypeface = SkiaCommandUtilities.GetOrCreateSkTypeface(_executionContext, typeface);
        using SKFont font = CreateShapedTextFont(skTypeface, scale);
        SkiaCommandUtilities.ApplyAntialias(font, antialias);

        using SKTextBlob? blob = BuildShapedTextBlob(span, font);

        if (blob != null)
        {
            _canvas.DrawText(blob, 0f, 0f, paint);
        }
    }

    private static SKTextBlob? BuildShapedTextBlob(in ReadOnlySpan<ShapedGlyph> shapingResult, SKFont font)
    {
        int drawableCount = 0;
        for (int i = 0; i < shapingResult.Length; i++)
        {
            if (shapingResult[i].GlyphId != 0)
            {
                drawableCount++;
            }
        }

        using SKTextBlobBuilder builder = new();
        SKPositionedRunBuffer run = builder.AllocatePositionedRun(font, drawableCount);
        Span<ushort> glyphSpan = run.Glyphs;
        Span<SKPoint> positionSpan = run.Positions;

        int drawIndex = 0;
        for (int index = 0; index < shapingResult.Length; index++)
        {
            ShapedGlyph shapedGlyph = shapingResult[index];
            if (shapedGlyph.GlyphId != 0)
            {
                glyphSpan[drawIndex] = (ushort)shapedGlyph.GlyphId;
                positionSpan[drawIndex] = new SKPoint(shapedGlyph.X, shapedGlyph.Y);
                drawIndex++;
            }
        }

        return builder.Build();
    }

    private static SKFont CreateShapedTextFont(SKTypeface skTypeface, float scale)
    {
        SKFont font = new()
        {
            Typeface = skTypeface,
            Size = 1,
            Subpixel = true,
            LinearMetrics = true,
            Hinting = SKFontHinting.Slight,
            ScaleX = scale
        };

        return font;
    }

    private void ExecuteDrawSoftMaskImageTile(DrawSoftMaskImageTileCommand command)
    {
        SoftMaskImageExecutionContext context = command.Context;
        PdfImageTile imageTile = context.ImageCache.GetNextTile(_executionContext.ExecutionObserver);
        PdfImageTile maskTile = context.MaskCache.GetNextTile(_executionContext.ExecutionObserver);

        if (imageTile.Image == null || maskTile.Image == null)
        {
            return;
        }

        SnappedTilePlacement placement = PdfImageCommandUtilities.GetSnappedTilePlacement(
            _executionContext, context.ImageSize, imageTile.TilePosition, context.Interpolate);

        SKColor? matte = null;
        if (context.MatteArray != null && maskTile.Parameters != null)
        {
            matte = maskTile.Parameters.ColorSpaceConverter.ToSrgb(context.MatteArray, maskTile.Parameters.RenderingIntent, default).ToSkiaColor();
        }

        SKSamplingOptions sampling = SkiaCommandUtilities.GetSamplingOptions(placement.Interpolate);

        using SKImage skImage = imageTile.Image.ToSkImage();
        using SKImage skMaskImage = maskTile.Image.ToSkImage();
        using SKShader imageShader = SkiaImageBlending.BuildImageShader(skImage, placement.DeviceSize.ToSkSize(), sampling);
        using SKShader maskShader = SkiaImageBlending.BuildImageShader(skMaskImage, placement.DeviceSize.ToSkSize(), sampling);
        using SKShader blendingShader = SkiaImageBlending.CreateSoftMaskBlendingShader(imageShader, maskShader, matte);
        using SKPaint paint = SkiaCommandUtilities.GetBaseImagePaint(blendingShader, context.DecodingContext);
        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);

        _canvas.Save();
        _canvas.Concat(placement.PlacementMatrix.ToSkMatrix());
        _canvas.ClipRect(placement.PlacementRectangle.ToSkRect());
        _canvas.DrawPaint(paint);
        _canvas.Restore();
    }

    private void ExecuteDrawStencilMaskImageTile(DrawStencilMaskImageTileCommand command)
    {
        StencilMaskImageExecutionContext context = command.Context;
        PdfImageTile tile = context.TileCache.GetNextTile(_executionContext.ExecutionObserver);

        if (tile.IsSkipped || tile.Image == null)
        {
            return;
        }

        SnappedTilePlacement placement = PdfImageCommandUtilities.GetSnappedTilePlacement(_executionContext, context.ImageSize, tile.TilePosition, context.Interpolate);

        SKColor fillColor = context.DecodingContext.FillColor.ToSkiaColor();
        using SKColorFilter colorFilter = SkiaImageBlending.CreateImageMaskColorFilter(in fillColor, context.InvertMask);
        using SKImage skImage = tile.Image.ToSkImage();
        using SKPaint paint = SkiaCommandUtilities.GetBaseImagePaint(context.DecodingContext);
        paint.ColorFilter = colorFilter;
        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);

        _canvas.Save();
        _canvas.Concat(placement.PlacementMatrix.ToSkMatrix());
        _canvas.DrawImage(skImage, placement.PlacementRectangle.ToSkRect(), SkiaCommandUtilities.GetSamplingOptions(placement.Interpolate), paint);
        _canvas.Restore();
    }

    private void ExecuteDrawText(DrawTextCommand command)
    {
        using SKPaint paint = command.Paint.ToSkiaPaint();
        bool antialias = _executionContext.Parameters.Antialias;
        paint.IsAntialias = antialias;
        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);

        SKTypeface skTypeface = SkiaCommandUtilities.GetOrCreateSkTypeface(_executionContext, command.Typeface);
        using SKFont font = new(skTypeface, command.FontSize);
        SkiaCommandUtilities.ApplyAntialias(font, antialias);

        _canvas.Save();
        _canvas.Concat(command.Matrix.ToSkMatrix());
        _canvas.DrawText(command.Text, 0f, 0f, SKTextAlign.Left, font, paint);
        _canvas.Restore();
    }

    private void ExecuteDrawTiling(DrawTilingCommand command)
    {
        _canvas.Save();
        _executionContext.Frames.OnSaveState();
        _canvas.Concat(command.Matrix.ToSkMatrix());
        _executionContext.Frames.OnConcatMatrix(command.Matrix);

        PdfCommandExecutionParameters childParameters = _executionContext.Parameters.Clone();
        childParameters.SnapToDevicePixels = false;

        SKRect skBBox = command.BBox.ToSkRect();

        using (SKPictureRecorder recorder = new())
        {
            using SKCanvas canvas = recorder.BeginRecording(skBBox);
            using PdfCommandExecutionContext childContext = new(
                childParameters,
                _executionContext.ContentLocker,
                _executionContext.OptionalContentGroups,
                _executionContext.ExecutionObserver);

            // do not apply AA to clip BBox to avoid seams
            canvas.ClipRect(skBBox, SKClipOperation.Intersect);
            childContext.Frames.OnConcatMatrix(_executionContext.Frames.TotalMatrix);

            SkCanvasCommandProcessor childProcessor = new(canvas, childContext, _logger);
            childProcessor.ExecuteDrawRecording(command.RecordingCommand);

            using SKPicture picture = recorder.EndRecording();

            ShaderTilingParameters shaderTilingParameters = GetShaderTilingParameters(command);

            if (shaderTilingParameters.CanUseShaders)
            {
                SKRect tileRect = new(
                    skBBox.Left,
                    skBBox.Top,
                    skBBox.Left + shaderTilingParameters.ExactTileWidth,
                    skBBox.Top + shaderTilingParameters.ExactTileHeight);

                using SKShader shader = picture.ToShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, tileRect);
                using SKPaint shaderPaint = new() { Shader = shader };

                _canvas.DrawRect(command.TilingArea.ToSkRect(), shaderPaint);
            }
            else
            {
                for (int i = 0; i <= command.XCount; i++)
                {
                    float x = command.TilingArea.Left + (i * command.XStep);
                    for (int j = 0; j <= command.YCount; j++)
                    {
                        float y = command.TilingArea.Top + (j * command.YStep);

                        SKMatrix translation = SKMatrix.CreateTranslation(x, y);

                        _canvas.Save();
                        _canvas.Concat(translation);
                        _canvas.DrawPicture(picture);

                        _canvas.Restore();
                    }
                }
            }
        }

        _canvas.Restore();
        _executionContext.Frames.OnRestoreState();
    }

    private ShaderTilingParameters GetShaderTilingParameters(DrawTilingCommand command)
    {
        PdfMatrix deviceMatrix = CommandHelpers.GetScaledMatrix(_executionContext);
        PdfPoint deviceOrigin = deviceMatrix.MapPoint(PdfPoint.Empty);
        PdfPoint mappedX = deviceMatrix.MapPoint(new PdfPoint(command.XStep, 0));
        PdfPoint mappedY = deviceMatrix.MapPoint(new PdfPoint(0, command.YStep));

        float deviceXAxisX = mappedX.X - deviceOrigin.X;
        float deviceXAxisY = mappedX.Y - deviceOrigin.Y;
        float deviceYAxisX = mappedY.X - deviceOrigin.X;
        float deviceYAxisY = mappedY.Y - deviceOrigin.Y;

        float deviceXAxisLength = MathF.Sqrt((deviceXAxisX * deviceXAxisX) + (deviceXAxisY * deviceXAxisY));
        float deviceYAxisLength = MathF.Sqrt((deviceYAxisX * deviceYAxisX) + (deviceYAxisY * deviceYAxisY));

        float scaleX = deviceXAxisLength / command.XStep;
        float scaleY = deviceYAxisLength / command.YStep;

        float targetPixelsX = MathF.Round(deviceXAxisLength);
        float targetPixelsY = MathF.Round(deviceYAxisLength);

        float exactTileWidth = targetPixelsX / scaleX;
        float exactTileHeight = targetPixelsY / scaleY;

        int maxTileDeviceDimension = _executionContext.Parameters.ImageTileSize;
        bool canUseShaders = deviceXAxisLength <= maxTileDeviceDimension && deviceYAxisLength <= maxTileDeviceDimension;

        return new ShaderTilingParameters(canUseShaders, exactTileWidth, exactTileHeight);
    }

    private void ExecuteSaveLayer(SaveLayerCommand command)
    {
        SKRect skBounds = command.Bounds.ToSkRect();

        if (command.Paint != null)
        {
            using SKPaint paint = command.Paint.ToSkiaPaint();
            paint.IsAntialias = _executionContext.Parameters.Antialias;
            _canvas.SaveLayer(skBounds, paint);
        }
        else
        {
            _canvas.SaveLayer(skBounds, null);
        }

        _executionContext.Frames.OnSaveLayer();
    }

    private void ExecuteTextCharacters(TextCharactersCommand command)
    {
        PdfMatrix matrix = _executionContext.Frames.TotalMatrix.PreConcat(command.Matrix);

        List<PdfCharacter> mapped = new(command.Characters.Length);

        for (int i = 0; i < command.Characters.Length; i++)
        {
            PdfRectangle pageRect = matrix.MapRect(command.Characters[i].BoundingBox);
            if (pageRect.Width != 0)
            {
                mapped.Add(new PdfCharacter(command.Characters[i].Text, pageRect));
            }
        }

        if (mapped.Count > 0)
        {
            _executionContext.MarkedContent.AppendCharacters(mapped);
        }
    }

    private readonly struct ShaderTilingParameters
    {
        public ShaderTilingParameters(bool canUseShaders, float exactTileWidth, float exactTileHeight)
        {
            CanUseShaders = canUseShaders;
            ExactTileWidth = exactTileWidth;
            ExactTileHeight = exactTileHeight;
        }

        public bool CanUseShaders { get; }

        public float ExactTileWidth { get; }

        public float ExactTileHeight { get; }
    }
}
