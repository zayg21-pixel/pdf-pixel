using Microsoft.Extensions.Logging;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using System;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// Produces the alpha plane rows of an image's soft mask, resampled to the output grid the image
/// the mask applies to is produced on.
/// </summary>
internal sealed class SoftMaskAlphaRowSource : IAlphaRowSource
{
    private readonly PdfImageDecoder _decoder;
    private readonly ILoggerFactory _loggerFactory;

    private PdfDecodedImage? _decodedMask;
    private byte[]? _unpackedRow;
    private int _unpackedRowIndex = -1;

    public SoftMaskAlphaRowSource(PdfImageDecoder decoder, ILoggerFactory loggerFactory)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>
    /// Sample grid the mask itself carries, which is what the image it applies to is produced on
    /// when the mask is the finer of the two.
    /// </summary>
    public PdfIntegerSize SampleSize => new(_decoder.Image.Width, _decoder.Image.Height);

    /// <summary>
    /// Decodes the mask and prepares it to serve rows of <paramref name="targetSize"/> pixels.
    /// </summary>
    /// <param name="targetSize">Output grid the rows are produced for.</param>
    /// <param name="contentLocker">Lock object used to serialize access to the compressed mask data.</param>
    /// <param name="observer">Observer notified as decoding progresses.</param>
    public void Initialize(in PdfIntegerSize targetSize, object contentLocker, IPdfExecutionObserver? observer)
    {
        _unpackedRowIndex = -1;

        // A transformation covering the target extent asks the decoder for the largest reduction
        // that still fills it; a mask no larger than the target reports no reduction at all.
        PdfMatrix targetExtent = PdfMatrix.CreateScale(targetSize.Width, targetSize.Height);
        PdfImageRowDecodingParameters maskParameters = _decoder.Initialize(null, contentLocker, targetExtent, observer);

        PdfImageRowProcessor processor = new(maskParameters, targetSize, _loggerFactory.CreateLogger<PdfImageRowProcessor>());

        // The target is created on the grid the rows are asked for, so the resampler that brings the
        // mask onto it is the one the row pipeline picks, in whichever direction the two grids differ.
        PdfImageRowTarget maskTarget = processor.CreateTarget(
            new PdfIntegerRectangle(0, 0, maskParameters.Width, maskParameters.Height),
            new PdfIntegerRectangle(0, 0, targetSize.Width, targetSize.Height));
        PdfImageRowTarget?[] targets = [maskTarget];

        var rowBuffer = new byte[maskParameters.RowBytes];

        for (int rowIndex = 0; rowIndex < maskParameters.Height; rowIndex++)
        {
            if (!_decoder.TryReadNextRow(rowBuffer, observer))
            {
                break;
            }

            processor.DecodeRow(rowIndex, rowBuffer, alphaSource: null, targets, observer);
        }

        _decodedMask = maskTarget.Image;

        // A mask that had to reach a colour space arrives as packed pixels, and only then is a row
        // of its own needed to lay the one channel that carries the coverage out on its own.
        _unpackedRow = (_decodedMask.ColorFormat == PdfImageColorFormat.Rgba) ? new byte[targetSize.Width] : null;
        _decoder.Cleanup();
    }

    /// <inheritdoc />
    public ReadOnlySpan<byte> GetRow(int outputRowIndex)
    {
        if (_decodedMask == null || outputRowIndex >= _decodedMask.Height)
        {
            return default;
        }

        ReadOnlySpan<byte> maskRow = _decodedMask.GetRow(outputRowIndex);

        if (_unpackedRow == null)
        {
            return maskRow;
        }

        if (outputRowIndex != _unpackedRowIndex)
        {
            UnpackRow(maskRow, _unpackedRow);
            _unpackedRowIndex = outputRowIndex;
        }

        return _unpackedRow;
    }

    public void Cleanup()
    {
        _decodedMask = null;
        _unpackedRow = null;
        _unpackedRowIndex = -1;
        _decoder.Cleanup();
    }

    private static void UnpackRow(in ReadOnlySpan<byte> maskRow, in Span<byte> destination)
    {
        for (int x = 0; x < destination.Length; x++)
        {
            destination[x] = maskRow[x * 4];
        }
    }
}
