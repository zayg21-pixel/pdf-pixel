using Microsoft.Extensions.Logging;
using PdfPixel.Color.Filters;
using PdfPixel.Color.Paint;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using SkiaSharp;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PdfPixel.Commands;

/// <summary>
/// Draws a PDF image lazily at Execute time.
/// Stores the <see cref="PdfImage"/> model and an <see cref="ImageDecodingContext"/> snapshot
/// so decoding, shader construction, and drawing are deferred until replay.
/// Caches decoded images and only re-decodes when the <see cref="PdfRenderingParameters.ScaleFactor"/> changes.
/// </summary>
public sealed class DrawImageCommand : PdfCommand
{
    private readonly PdfImage _pdfImage;
    private readonly ImageDecodingContext _decodingContext;
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger _logger;

    // Cached decoded images — rebuilt when ScaleFactor changes
    private SKImage _cachedImage;
    private SKImage _cachedMaskImage;
    private SKImage _cachedStencilMaskImage;
    private SKMatrix? _cachedMatrix;
    private bool _cacheBuilt;
    private readonly PdfImageDecoder _imageDecoder;
    private readonly PdfImageDecoder _maskDecoder;
    private readonly PdfImageDecoder _stencilMaskDecoder;


    /// <summary>
    /// Initializes a new instance of the <see cref="DrawImageCommand"/> class.
    /// </summary>
    /// <param name="pdfImage">The PDF image model to decode and render.</param>
    /// <param name="decodingContext">Snapshot of graphics-state values captured at record time.</param>
    /// <param name="loggerFactory">Logger factory for decoder creation and diagnostic output.</param>
    public DrawImageCommand(PdfImage pdfImage, ImageDecodingContext decodingContext, ILoggerFactory loggerFactory)
    {
        _pdfImage = pdfImage;
        _decodingContext = decodingContext;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<DrawImageCommand>();

        _imageDecoder = PdfImageDecoder.GetDecoder(_pdfImage, loggerFactory);

        if (_pdfImage.SoftMask != null)
        {
            _maskDecoder = PdfImageDecoder.GetDecoder(_pdfImage.SoftMask, loggerFactory);
        }

        if (_pdfImage.StencilMask != null)
        {
            _stencilMaskDecoder = PdfImageDecoder.GetDecoder(_pdfImage.StencilMask, loggerFactory);
        }
    }

    public override bool IsScaleDependent => true;

    /// <summary>
    /// Fixed source-pixel tile size for the experimental per-tile draw path.
    /// </summary>
    private const int TileSize = 1000;

    /// <inheritdoc />
    public override void Execute(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        var renderingParameters = executionContext.RenderingParameters;
        bool antialias = renderingParameters.Antialias;

        var scale = executionContext.RenderingParameters.ScaleFactor ?? 1;
        _decodingContext.CTM = canvas.TotalMatrix.PostConcat(SKMatrix.CreateScale(scale, scale));

        RebuildCacheIfNeeded(executionContext.CancellationToken);

        if (_cachedImage == null)
        {
            return;
        }

        var count = canvas.Save();

        try
        {
            canvas.Concat(SKMatrix.CreateScale(1, -1));
            canvas.Concat(SKMatrix.CreateTranslation(0, -1));

            // Switch local space from unit-square to source-image-pixel space.
            // Applied once: the only floating-point step in the per-tile matrix chain.
            // Per tile we then apply an integer-exact Translate; tile draws live in tile-pixel
            // local space (size tileWidth × tileHeight), letting child shaders skip any local matrix
            // for the image and use a deterministic offset for masks.
            int width = _cachedImage.Width;
            int height = _cachedImage.Height;
            canvas.Concat(SKMatrix.CreateScale(1f / width, 1f / height));

            var sampling = GetSamplingOptions();

            var modifierList = modifiers as IReadOnlyCollection<IPdfCommandModifier>
                ?? new List<IPdfCommandModifier>(modifiers);

            foreach (var tileRect in EnumerateTiles(width, height, TileSize))
            {
                DrawTile(canvas, tileRect, sampling, antialias, modifierList);
            }

        }
        finally
        {
            canvas.RestoreToCount(count);
        }

    }

    /// <summary>
    /// Yields the source-pixel rects of a fixed-size tile grid covering the image.
    /// </summary>
    private static IEnumerable<SKRectI> EnumerateTiles(int width, int height, int tileSize)
    {
        for (int y = 0; y < height; y += tileSize)
        {
            int h = System.Math.Min(tileSize, height - y);
            for (int x = 0; x < width; x += tileSize)
            {
                int w = System.Math.Min(tileSize, width - x);
                yield return new SKRectI(x, y, x + w, y + h);
            }
        }
    }

    /// <summary>
    /// Subsets the source image to the given tile rectangle and builds a raw shader with no
    /// local matrix. The caller arranges the canvas so the runtime effect's <c>coord</c> arrives
    /// in tile-pixel space (0..tileWidth, 0..tileHeight); the shader then samples directly.
    /// Used for both image and mask children.
    /// Caller takes ownership of the returned shader.
    /// </summary>
    private static SKShader BuildTileChild(
        SKImage source,
        SKRectI tileRect,
        SKSamplingOptions sampling)
    {
        var tile = source.Subset(tileRect);
        if (tile == null)
        {
            return null;
        }

        var shader = tile.ToShader(
            SKShaderTileMode.Clamp,
            SKShaderTileMode.Clamp, sampling);
        tile.Dispose();
        return shader;
    }

    /// <summary>
    /// Draws a single tile of the cached image. The caller has already concatenated the global
    /// Scale(1/W, 1/H) onto the canvas; this method only adds the integer-exact tile Translate
    /// and renders in tile-pixel local space. No shader-level local matrices anywhere — image
    /// and mask are both pre-subset to the tile rect.
    /// </summary>
    private void DrawTile(
        SKCanvas canvas,
        SKRectI tileRect,
        SKSamplingOptions sampling,
        bool antialias,
        IReadOnlyCollection<IPdfCommandModifier> modifiers)
    {
        using var tileChild = BuildTileChild(_cachedImage, tileRect, sampling);
        if (tileChild == null)
        {
            return;
        }

        // Soft-mask / stencil-mask: subset the mask the same way as the image.
        // HasImageMask uses tileChild itself as the mask, so no separate maskChild is built.
        SKImage separateMask = null;
        if (_pdfImage.SoftMask != null && _cachedMaskImage != null)
        {
            separateMask = _cachedMaskImage;
        }
        else if (_pdfImage.StencilMask != null && _cachedStencilMaskImage != null)
        {
            separateMask = _cachedStencilMaskImage;
        }

        using var maskChild = separateMask != null
            ? BuildTileChild(separateMask, tileRect, sampling)
            : null;

        using var shader = BuildShader(tileChild, maskChild);
        if (shader == null)
        {
            return;
        }

        using var paint = PdfPaintFactory.CreateImageShaderPaint(_decodingContext.BlendMode, shader);
        paint.IsAntialias = antialias;

        foreach (var modifier in modifiers)
        {
            modifier.ModifyPaint(paint);
        }

        canvas.Save();

        canvas.Concat(SKMatrix.CreateTranslation(tileRect.Left, tileRect.Top));

        using var recording = new SKPictureRecorder();

        var localRect = new SKRect(0, 0, tileRect.Width, tileRect.Height);
        using var recordingCanvas = recording.BeginRecording(localRect);
        //recordingCanvas.ClipRect(localRect, SKClipOperation.Intersect, antialias: antialias);
        recordingCanvas.DrawPaint(paint);

        using var picture = recording.EndRecording();

        canvas.ClipRect(
    new SKRect(0, 0, tileRect.Width, tileRect.Height),
    SKClipOperation.Intersect,
    antialias: true);


        canvas.DrawPicture(picture);


        //canvas.DrawPaint(paint);
        canvas.Restore();
    }

    /// <summary>
    /// Wraps the pre-built tile and (optional) mask children in the appropriate runtime effect
    /// based on the PDF image's mask configuration.
    /// </summary>
    private SKShader BuildShader(SKShader tileChild, SKShader maskChild)
    {
        if (_pdfImage.HasImageMask)
        {
            // tileChild represents a tile of _cachedImage, which IS the stencil image.
            bool inverse = _pdfImage.DecodeArray == null
                || (_pdfImage.DecodeArray.Length == 2 && _pdfImage.DecodeArray[0] < _pdfImage.DecodeArray[1]);

            return ImageBlending.CreateImageMaskBlendingShader(
                tileChild,
                _decodingContext.FillColor,
                inverse);
        }

        if (_pdfImage.SoftMask != null && maskChild != null)
        {
            SKColor? matteColor = null;
            if (_pdfImage.SoftMask.MatteArray != null)
            {
                matteColor = _pdfImage.SoftMask.ColorSpaceConverter.ToSrgb(
                    _pdfImage.SoftMask.MatteArray,
                    _pdfImage.SoftMask.RenderingIntent,
                    default);
            }

            return ImageBlending.CreateSoftMaskBlendingShader(
                tileChild,
                maskChild,
                matteColor);
        }

        if (_pdfImage.StencilMask != null && maskChild != null)
        {
            return ImageBlending.CreateStencilMaskShader(
                tileChild,
                maskChild);
        }

        return ImageBlending.CreateImageShader(tileChild);
    }

    /// <summary>
    /// Rebuilds the decoded image cache when the scale factor has changed.
    /// </summary>
    private void RebuildCacheIfNeeded(CancellationToken cancellationToken)
    {
        if (_cacheBuilt && _cachedMatrix?.ScaleX == _decodingContext.CTM.ScaleX && _cachedMatrix?.ScaleY == _decodingContext.CTM.ScaleY)
        {
            return;
        }

        DisposeCache();

        if (_imageDecoder == null)
        {
            _logger.LogWarning("No decoder for image '{ImageName}' of type {ImageType}. Skipping.", _pdfImage?.Name, _pdfImage?.Type);
            _cacheBuilt = true;
            _cachedMatrix = _decodingContext.CTM;
            return;
        }

        _cachedImage = _imageDecoder.Decode(
            _decodingContext,
            cancellationToken);

        if (_cachedImage == null)
        {
            _logger.LogWarning("Decoder returned null for image '{ImageName}'. Skipping.", _pdfImage?.Name);
        }

        // Decode the soft mask image when present
        if (_pdfImage.SoftMask != null && _cachedImage != null)
        {
            if (_maskDecoder != null)
            {
                _cachedMaskImage = _maskDecoder.Decode(
                    _decodingContext,
                    cancellationToken);

                if (_cachedMaskImage == null)
                {
                    _logger.LogWarning("Decoder returned null for soft mask of image '{ImageName}'. Skipping.", _pdfImage?.Name);
                }
            }
            else
            {
                _logger.LogWarning("No decoder for soft mask of image '{ImageName}'. Skipping.", _pdfImage?.Name);
            }
        }

        // Decode the stencil mask image when present
        if (_pdfImage.StencilMask != null && _cachedImage != null)
        {
            if (_stencilMaskDecoder != null)
            {
                _cachedStencilMaskImage = _stencilMaskDecoder.Decode(
                    _decodingContext,
                    cancellationToken);

                if (_cachedStencilMaskImage == null)
                {
                    _logger.LogWarning("Decoder returned null for stencil mask of image '{ImageName}'. Skipping.", _pdfImage?.Name);
                }
            }
            else
            {
                _logger.LogWarning("No decoder for stencil mask of image '{ImageName}'. Skipping.", _pdfImage?.Name);
            }
        }

        _cacheBuilt = true;
        _cachedMatrix = _decodingContext.CTM;
    }

    /// <summary>
    /// Computes the sampling options for the current image based on scale and interpolation flags.
    /// </summary>
    private SKSamplingOptions GetSamplingOptions()
    {
        bool isDownscaled = _decodingContext.GetScaledSize(new SKSizeI(_pdfImage.Width, _pdfImage.Height)).HasValue;

        if (isDownscaled || _decodingContext.IsType3Rendering)
        {
            return new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        }

        if (_pdfImage.Interpolate)
        {
            return new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear);
        }

        return new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
    }

    /// <summary>
    /// Disposes all cached images.
    /// </summary>
    private void DisposeCache()
    {
        _cachedImage?.Dispose();
        _cachedImage = null;

        _cachedMaskImage?.Dispose();
        _cachedMaskImage = null;

        _cachedStencilMaskImage?.Dispose();
        _cachedStencilMaskImage = null;

        _cacheBuilt = false;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        DisposeCache();
        base.Dispose(disposing);
    }
}
