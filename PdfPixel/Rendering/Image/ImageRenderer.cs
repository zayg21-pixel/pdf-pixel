using Microsoft.Extensions.Logging;
using PdfPixel.Color.Filters;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Utilities;
using SkiaSharp;
using System;

namespace PdfPixel.Rendering.Image;

/// <summary>
/// Standard PDF image renderer supporting normal images, image masks, and soft masks.
/// </summary>
public class ImageRenderer : IImageRenderer
{
    private readonly IPdfRenderer _renderer;
    private readonly ILoggerFactory _factory;
    private readonly ILogger<ImageRenderer> _logger;

    public ImageRenderer(IPdfRenderer renderer, ILoggerFactory loggerFactory)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        _factory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _logger = loggerFactory.CreateLogger<ImageRenderer>();
    }

    /// <summary>
    /// Renders a PDF image using the provided command processor and graphics state.
    /// Emits drawing commands through the processor for all image types.
    /// </summary>
    /// <param name="processor">The command processor to draw through.</param>
    /// <param name="pdfImage">The <see cref="PdfImage"/> to be rendered. Must not be <see langword="null"/> and must have positive
    /// dimensions.</param>
    /// <param name="state">The <see cref="PdfGraphicsState"/> that defines the rendering state for the image.</param>
    public void DrawImage(IPdfCommandProcessor processor, PdfImage pdfImage, PdfGraphicsState state)
    {
        if (processor == null)
        {
            return;
        }

        if (pdfImage == null || pdfImage.Width <= 0 || pdfImage.Height <= 0)
        {
            return;
        }

        processor.Process(new SaveStateCommand());
        processor.Process(new ConcatMatrixCommand(SKMatrix.CreateScale(1, -1)));
        var destRect = new SKRect(0, -1, 1, 0);

        ImageRenderCore(processor, pdfImage, state, destRect);

        processor.Process(new RestoreStateCommand());
    }

    private void ImageRenderCore(IPdfCommandProcessor processor, PdfImage pdfImage, PdfGraphicsState state, SKRect destRect)
    {
        using var softMaskScope = new SoftMaskDrawingScope(_renderer, processor, state);
        softMaskScope.BeginDrawContent();

        if (pdfImage.HasImageMask)
        {
            DrawImageMask(processor, pdfImage, state, destRect);
            return;
        }

        if (pdfImage.SoftMask != null)
        {
            DrawWithSoftMask(processor, pdfImage, state, destRect);
            return;
        }

        DrawNormalImage(processor, pdfImage, state, destRect);
    }

    /// <summary>
    /// Draws a normal PDF image (no image mask or soft mask) via the command processor.
    /// </summary>
    /// <param name="processor">Command processor to emit drawing commands through.</param>
    /// <param name="pdfImage">Image definition.</param>
    /// <param name="state">Current graphics state.</param>
    /// <param name="destRect">Destination rectangle.</param>
    private void DrawNormalImage(
        IPdfCommandProcessor processor,
        PdfImage pdfImage,
        PdfGraphicsState state,
        SKRect destRect)
    {
        var decoder = PdfImageDecoder.GetDecoder(pdfImage, _factory);
        if (decoder == null)
        {
            _logger.LogWarning(
                "No decoder for image '{ImageName}' of type {ImageType}. Skipping.",
                pdfImage?.Name,
                pdfImage?.Type);
            return;
        }

        var baseImage = decoder.Decode(state);
        if (baseImage == null)
        {
            _logger.LogWarning(
                "Decoder returned null for image '{ImageName}'. Skipping.",
                pdfImage?.Name);
            return;
        }

        var imagePaint = PdfPaintFactory.CreateImagePaint(state);
        var sampling = PdfPaintFactory.GetImageSamplingOptions(pdfImage, state);
        processor.Process(new DrawImageCommand(baseImage, destRect, sampling, imagePaint));
    }

    /// <summary>
    /// Draws an image mask (stencil mask) via the command processor.
    /// Decodes the image mask, applies the decode filter, and fills using the current fill paint.
    /// </summary>
    /// <param name="processor">Command processor to emit drawing commands through.</param>
    /// <param name="pdfImage">The image mask definition.</param>
    /// <param name="state">Current graphics state.</param>
    /// <param name="destRect">Destination rectangle.</param>
    private void DrawImageMask(
        IPdfCommandProcessor processor,
        PdfImage pdfImage,
        PdfGraphicsState state,
        SKRect destRect)
    {
        var decoder = PdfImageDecoder.GetDecoder(pdfImage, _factory);
        if (decoder == null)
        {
            _logger.LogWarning(
                "No decoder for image mask '{ImageName}'. Skipping.",
                pdfImage?.Name);
            return;
        }

        // TODO: [MEDIUM] apply patterns to stencil image 

        using var alphaMask = decoder.Decode(state);
        if (alphaMask == null)
        {
            _logger.LogWarning(
                "Decoder returned null for image mask '{ImageName}'. Skipping.",
                pdfImage?.Name);
            return;
        }

        var sampling = PdfPaintFactory.GetImageSamplingOptions(pdfImage, state);
        bool inverse = pdfImage.DecodeArray == null || (pdfImage.DecodeArray.Length == 2 && pdfImage.DecodeArray[0] < pdfImage.DecodeArray[1]);

        using var shader = ImageBlending.CreateImageMaskBlendingShader(
            alphaMask,
            state.FillPaint.Color,
            inverse,
            sampling);

        DrawShaderToRect(processor, shader, state, destRect);
    }

    /// <summary>
    /// Draws a PDF image with a soft mask applied via the command processor.
    /// If any step fails, logs a warning and does not draw.
    /// </summary>
    /// <param name="processor">Command processor to emit drawing commands through.</param>
    /// <param name="pdfImage">The base image definition (may have SoftMask property).</param>
    /// <param name="state">Current graphics state.</param>
    /// <param name="destRect">Destination rectangle.</param>
    private void DrawWithSoftMask(
        IPdfCommandProcessor processor,
        PdfImage pdfImage,
        PdfGraphicsState state,
        SKRect destRect)
    {
        if (pdfImage.SoftMask == null)
        {
            _logger.LogWarning("No soft mask present for image '{ImageName}'. Skipping soft mask drawing.", pdfImage?.Name);
            return;
        }

        var baseDecoder = PdfImageDecoder.GetDecoder(pdfImage, _factory);
        if (baseDecoder == null)
        {
            _logger.LogWarning("No decoder for image '{ImageName}'. Skipping soft mask drawing.", pdfImage?.Name);
            return;
        }

        using var baseImage = baseDecoder.Decode(state);
        if (baseImage == null)
        {
            _logger.LogWarning("Decoder returned null for image '{ImageName}'. Skipping soft mask drawing.", pdfImage?.Name);
            return;
        }

        var softMaskDecoder = PdfImageDecoder.GetDecoder(pdfImage.SoftMask, _factory);
        if (softMaskDecoder == null)
        {
            _logger.LogWarning("No decoder for soft mask of image '{ImageName}'. Skipping soft mask drawing.", pdfImage?.Name);
            return;
        }

        using var maskImage = softMaskDecoder.Decode(state);
        if (maskImage == null)
        {
            _logger.LogWarning("Decoder returned null for soft mask of image '{ImageName}'. Skipping soft mask drawing.", pdfImage?.Name);
            return;
        }

        var sampling = PdfPaintFactory.GetImageSamplingOptions(pdfImage, state);
        SKColor? matteColor;

        if (pdfImage.SoftMask.MatteArray != null)
        {
            matteColor = pdfImage.SoftMask.ColorSpaceConverter.ToSrgb(pdfImage.SoftMask.MatteArray, pdfImage.SoftMask.RenderingIntent, default);
        }
        else
        {
            matteColor = default;
        }

        using var shader = ImageBlending.CreateSoftMaskBlendingShader(baseImage, maskImage, matteColor, sampling);
        DrawShaderToRect(processor, shader, state, destRect);
    }

    /// <summary>
    /// Emits commands for a full-unit-rectangle shader draw mapped to <paramref name="destRect"/>.
    /// Shared by image mask and soft mask rendering paths.
    /// </summary>
    /// <param name="processor">Command processor to emit drawing commands through.</param>
    /// <param name="shader">Shader to fill the unit rectangle.</param>
    /// <param name="state">Current graphics state (used for the composition paint).</param>
    /// <param name="destRect">Destination rectangle on the canvas.</param>
    private static void DrawShaderToRect(
        IPdfCommandProcessor processor,
        SKShader shader,
        PdfGraphicsState state,
        SKRect destRect)
    {
        var imageShaderPaint = PdfPaintFactory.CreateImageShaderPaint(state, shader);

        SKMatrix scale = SKMatrix.CreateScale(destRect.Width, destRect.Height);
        SKMatrix translate = SKMatrix.CreateTranslation(destRect.Left, destRect.Top);
        SKMatrix matrix = SKMatrix.Concat(scale, translate);

        processor.Process(new SaveStateCommand());
        processor.Process(new ConcatMatrixCommand(matrix));
        var unitRectangle = new SKRect(0, 0, 1, 1);
        processor.Process(new ClipRectCommand(unitRectangle, SKClipOperation.Intersect, state.RenderingParameters.AntialiasClip));
        processor.Process(new DrawPaintCommand(imageShaderPaint));
        processor.Process(new RestoreStateCommand());
    }
}
