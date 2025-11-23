using Microsoft.Extensions.Logging;
using PdfReader.Color.Paint;
using PdfReader.Imaging.Decoding;
using PdfReader.Imaging.Model;
using PdfReader.Imaging.Processing;
using PdfReader.Rendering.State;
using PdfReader.Transparency.Utilities;
using SkiaSharp;
using System;

namespace PdfReader.Rendering.Image;

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
    /// Renders a PDF image onto the specified canvas using the provided graphics state.
    /// </summary>
    /// <param name="canvas">The <see cref="SKCanvas"/> on which the image will be drawn. Must not be <see langword="null"/>.</param>
    /// <param name="pdfImage">The <see cref="PdfImage"/> to be rendered. Must not be <see langword="null"/> and must have positive
    /// dimensions.</param>
    /// <param name="state">The <see cref="PdfGraphicsState"/> that defines the rendering state for the image.</param>
    public void DrawImage(SKCanvas canvas, PdfImage pdfImage, PdfGraphicsState state)
    {
        if (canvas == null)
        {
            return;
        }

        if (pdfImage == null || pdfImage.Width <= 0 || pdfImage.Height <= 0)
        {
            return;
        }

        canvas.Save();
        canvas.Scale(1, -1);
        var destRect = new SKRect(0, -1, 1, 0);

        ImageRenderCore(canvas, pdfImage, state, destRect);

        canvas.Restore();
    }

    private void ImageRenderCore(SKCanvas canvas, PdfImage pdfImage, PdfGraphicsState state, SKRect destRect)
    {
        using var softMaskScope = new SoftMaskDrawingScope(_renderer, canvas, state);
        softMaskScope.BeginDrawContent();

        if (pdfImage.HasImageMask)
        {
            DrawImageMask(canvas, pdfImage, state, destRect);
            return;
        }

        if (pdfImage.SoftMask != null)
        {
            DrawWithSoftMask(canvas, pdfImage, state, destRect);
            return;
        }

        DrawNormalImage(canvas, pdfImage, state, destRect);
    }

    /// <summary>
    /// Draws a normal PDF image (no image mask or soft mask) to the specified canvas.
    /// </summary>
    /// <param name="canvas">Destination canvas.</param>
    /// <param name="pdfImage">Image definition.</param>
    /// <param name="state">Current graphics state.</param>
    /// <param name="destRect">Destination rectangle.</param>
    private void DrawNormalImage(
        SKCanvas canvas,
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

        using var baseImage = decoder.Decode();
        if (baseImage == null)
        {
            _logger.LogWarning(
                "Decoder returned null for image '{ImageName}'. Skipping.",
                pdfImage?.Name);
            return;
        }

        using var imagePaint = PdfPaintFactory.CreateImagePaint(state);
        imagePaint.ColorFilter = ImagePostProcessingFilters.BuildImageFilter(pdfImage, decoder.IsColorConverted);

        var sampling = PdfPaintFactory.GetImageSamplingOptions(pdfImage);
        canvas.DrawImage(baseImage, destRect, sampling, imagePaint);
    }

    /// <summary>
    /// Draws an image mask (stencil mask) to the specified canvas.
    /// Decodes the image mask, applies the decode filter, and fills using the current fill paint.
    /// </summary>
    /// <param name="canvas">Destination canvas.</param>
    /// <param name="pdfImage">The image mask definition.</param>
    /// <param name="state">Current graphics state.</param>
    /// <param name="destRect">Destination rectangle.</param>
    private void DrawImageMask(
        SKCanvas canvas,
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

        using var alphaMask = decoder.Decode();
        if (alphaMask == null)
        {
            _logger.LogWarning(
                "Decoder returned null for image mask '{ImageName}'. Skipping.",
                pdfImage?.Name);
            return;
        }

        var sampling = PdfPaintFactory.GetImageSamplingOptions(pdfImage);
        var layerPaint = PdfPaintFactory.CreateLayerPaint(state);

        // it's important to invert mask/target here, otherwise image would be misaligned
        using var fillPaint = PdfPaintFactory.CreateMaskImageFillPaint(state);

        using var maskPaint = PdfPaintFactory.CreateMaskImagePaint();
        maskPaint.ColorFilter = ImagePostProcessingFilters.BuildImageFilter(pdfImage, decoder.IsColorConverted);

        canvas.SaveLayer(destRect, layerPaint);

        canvas.DrawImage(alphaMask, destRect, sampling, maskPaint);
        canvas.DrawRect(destRect, fillPaint);

        canvas.Restore();
    }

    /// <summary>
    /// Draws a PDF image with a soft mask applied, compositing directly onto the destination canvas using a layer.
    /// If any step fails, logs a warning and does not draw.
    /// </summary>
    /// <param name="canvas">Destination canvas.</param>
    /// <param name="pdfImage">The base image definition (may have SoftMask property).</param>
    /// <param name="state">Current graphics state.</param>
    /// <param name="page">Owning page.</param>
    /// <param name="destRect">Destination rectangle.</param>
    private void DrawWithSoftMask(
        SKCanvas canvas,
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

        using var baseImage = baseDecoder.Decode();
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

        using var maskImage = softMaskDecoder.Decode();
        if (maskImage == null)
        {
            _logger.LogWarning("Decoder returned null for soft mask of image '{ImageName}'. Skipping soft mask drawing.", pdfImage?.Name);
            return;
        }

        using var layerPaint = PdfPaintFactory.CreateLayerPaint(state);
        using var imagePaint = PdfPaintFactory.CreateMaskImagePaint();
        imagePaint.ColorFilter = ImagePostProcessingFilters.BuildImageFilter(pdfImage, baseDecoder.IsColorConverted);

        using var maskPaint = PdfPaintFactory.CreateImageMaskPaint();
        maskPaint.ColorFilter = ImagePostProcessingFilters.BuildImageFilter(pdfImage.SoftMask, softMaskDecoder.IsColorConverted);

        var sampling = PdfPaintFactory.GetImageSamplingOptions(pdfImage);
        var maskSampling = PdfPaintFactory.GetImageSamplingOptions(pdfImage.SoftMask);

        canvas.SaveLayer(destRect, layerPaint);

        canvas.DrawImage(baseImage, destRect, sampling, imagePaint);

        // purpose of separate picture is the same as with mask paint, it allows to set filter effect on the whole picture
        // that eliminates edge effect
        using var maskRecorder = new SKPictureRecorder();
        using var maskCanvas = maskRecorder.BeginRecording(destRect);

        maskCanvas.DrawImage(maskImage, destRect, maskSampling);

        using var maskPicture = maskRecorder.EndRecording();

        canvas.DrawPicture(maskPicture, maskPaint);

        canvas.Restore();
    }
}
