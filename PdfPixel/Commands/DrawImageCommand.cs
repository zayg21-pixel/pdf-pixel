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
    private float? _cachedScaleFactor;
    private bool _cacheBuilt;

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
    }

    public override bool IsScaleDependant => true;

    /// <inheritdoc />
    public override async Task ExecuteAsync(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
        var renderingParameters = executionContext.RenderingParameters;
        bool antialias = renderingParameters.Antialias;

        await RebuildCacheIfNeeded(renderingParameters, executionContext.CancellationToken);

        if (_cachedImage == null)
        {
            return;
        }

        canvas.Save();
        canvas.Concat(SKMatrix.CreateScale(1, -1));
        canvas.Concat(SKMatrix.CreateTranslation(0, -1));
        canvas.ClipRect(new SKRect(0, 0, 1, 1), SKClipOperation.Intersect, antialias);

        using var shader = CreateShader(renderingParameters);
        if (shader != null)
        {
            using var paint = PdfPaintFactory.CreateImageShaderPaint(_decodingContext.BlendMode, shader);
            paint.IsAntialias = antialias;
            foreach (var modifier in modifiers)
            {
                modifier.ModifyPaint(paint);
            }

            canvas.DrawPaint(paint);
        }

        canvas.Restore();
    }

    /// <summary>
    /// Rebuilds the decoded image cache when the scale factor has changed.
    /// </summary>
    private async Task RebuildCacheIfNeeded(PdfRenderingParameters renderingParameters, CancellationToken cancellationToken)
    {
        if (_cacheBuilt && _cachedScaleFactor == renderingParameters.ScaleFactor)
        {
            return;
        }

        DisposeCache();

        var decoder = PdfImageDecoder.GetDecoder(_pdfImage, _loggerFactory);
        if (decoder == null)
        {
            _logger.LogWarning("No decoder for image '{ImageName}' of type {ImageType}. Skipping.", _pdfImage?.Name, _pdfImage?.Type);
            _cacheBuilt = true;
            _cachedScaleFactor = renderingParameters.ScaleFactor;
            return;
        }

        _cachedImage = await decoder.DecodeAsync(
            _decodingContext,
            renderingParameters,
            cancellationToken);

        if (_cachedImage == null)
        {
            _logger.LogWarning("Decoder returned null for image '{ImageName}'. Skipping.", _pdfImage?.Name);
        }

        // Decode the soft mask image when present
        if (_pdfImage.SoftMask != null && _cachedImage != null)
        {
            var softMaskDecoder = PdfImageDecoder.GetDecoder(_pdfImage.SoftMask, _loggerFactory);
            if (softMaskDecoder != null)
            {
                _cachedMaskImage = await softMaskDecoder.DecodeAsync(
                    _decodingContext,
                    renderingParameters,
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

        _cacheBuilt = true;
        _cachedScaleFactor = renderingParameters.ScaleFactor;
    }

    /// <summary>
    /// Creates the appropriate shader for the image type (normal, image mask, or soft mask).
    /// </summary>
    private SKShader CreateShader(PdfRenderingParameters renderingParameters)
    {
        if (_cachedImage == null)
        {
            return null;
        }

        var sampling = GetSamplingOptions(renderingParameters);

        if (_pdfImage.HasImageMask)
        {
            return CreateImageMaskShader(sampling);
        }

        if (_pdfImage.SoftMask != null && _cachedMaskImage != null)
        {
            return CreateSoftMaskShader(sampling);
        }

        return ImageBlending.CreateImageShader(_cachedImage, sampling);
    }

    /// <summary>
    /// Creates a stencil mask shader using the cached mask image and the fill color.
    /// </summary>
    private SKShader CreateImageMaskShader(SKSamplingOptions sampling)
    {
        // TODO: [MEDIUM] apply patterns to stencil image
        bool inverse = _pdfImage.DecodeArray == null
            || (_pdfImage.DecodeArray.Length == 2 && _pdfImage.DecodeArray[0] < _pdfImage.DecodeArray[1]);

        return ImageBlending.CreateImageMaskBlendingShader(
            _cachedImage,
            _decodingContext.FillColor,
            inverse,
            sampling);
    }

    /// <summary>
    /// Creates a soft-mask blending shader combining the cached base and mask images.
    /// </summary>
    private SKShader CreateSoftMaskShader(SKSamplingOptions sampling)
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
            _cachedImage,
            _cachedMaskImage,
            matteColor,
            sampling);
    }

    /// <summary>
    /// Computes the sampling options for the current image based on scale and interpolation flags.
    /// </summary>
    private SKSamplingOptions GetSamplingOptions(PdfRenderingParameters renderingParameters)
    {
        bool isDownscaled = renderingParameters
            .GetScaledSize(new SKSizeI(_pdfImage.Width, _pdfImage.Height), _decodingContext.CTM)
            .HasValue;

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

        _cacheBuilt = false;
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        DisposeCache();
        base.Dispose(disposing);
    }
}
