using Microsoft.Extensions.Logging;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Model;
using PdfPixel.Models;
using System;

namespace PdfPixel.Imaging.Processing;

/// <summary>
/// Produces the alpha plane rows of an image's soft mask, resampled to the sample grid of the
/// image the mask applies to.
/// </summary>
internal sealed class SoftMaskAlphaRowSource
{
    private readonly PdfImageDecoder _decoder;
    private readonly ILoggerFactory _loggerFactory;

    private PdfDecodedImage? _decodedMask;
    private byte[]? _targetRow;
    private int _targetWidth;
    private int _targetHeight;
    private int _maskSampleStride;
    private float _horizontalScale;
    private float _verticalScale;
    private int _bufferedSourceRow = -1;

    public SoftMaskAlphaRowSource(PdfImageDecoder decoder, ILoggerFactory loggerFactory)
    {
        _decoder = decoder ?? throw new ArgumentNullException(nameof(decoder));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    /// <summary>
    /// Decodes the mask and prepares it to serve rows of <paramref name="targetSize"/> pixels.
    /// </summary>
    /// <param name="targetSize">Sample grid the rows are produced for.</param>
    /// <param name="contentLocker">Lock object used to serialize access to the compressed mask data.</param>
    /// <param name="observer">Observer notified as decoding progresses.</param>
    public void Initialize(in PdfIntegerSize targetSize, object contentLocker, IPdfExecutionObserver? observer)
    {
        _targetWidth = targetSize.Width;
        _targetHeight = targetSize.Height;
        _bufferedSourceRow = -1;

        // A transformation covering the target extent asks the decoder for the largest reduction
        // that still fills it; a mask no larger than the target reports no reduction at all.
        PdfMatrix targetExtent = PdfMatrix.CreateScale(targetSize.Width, targetSize.Height);
        PdfImageRowDecodingParameters maskParameters = _decoder.Initialize(null, contentLocker, targetExtent, observer);

        PdfImageRowProcessor processor = new(maskParameters, _loggerFactory.CreateLogger<PdfImageRowProcessor>());

        PdfImageRowTarget maskTarget = processor.CreateTarget(0, maskParameters.Width, maskParameters.Height, maskParameters.DownscaledSize);
        PdfImageRowTarget?[] targets = [maskTarget];

        var rowBuffer = new byte[maskParameters.RowBytes];

        for (int rowIndex = 0; rowIndex < maskParameters.Height; rowIndex++)
        {
            if (!_decoder.TryReadNextRow(rowBuffer, observer))
            {
                break;
            }

            processor.DecodeRow(rowIndex, rowBuffer, default, targets, observer);
        }

        _decodedMask = maskTarget.Image;
        _targetRow = new byte[_targetWidth];
        _maskSampleStride = (_decodedMask.ColorFormat == PdfImageColorFormat.Gray) ? 1 : 4;
        _horizontalScale = (float)_decodedMask.Width / _targetWidth;
        _verticalScale = (float)_decodedMask.Height / _targetHeight;
        _decoder.Cleanup();
    }

    /// <summary>
    /// Returns the alpha values for <paramref name="rowIndex"/> of the target grid, one byte per pixel.
    /// </summary>
    public ReadOnlySpan<byte> GetRow(int rowIndex)
    {
        if (_decodedMask == null || _targetRow == null)
        {
            return default;
        }

        int sourceRow = (_decodedMask.Height == _targetHeight)
            ? rowIndex
            : Math.Min(_decodedMask.Height - 1, (int)(rowIndex * _verticalScale));

        if (sourceRow != _bufferedSourceRow)
        {
            FillTargetRow(sourceRow);
            _bufferedSourceRow = sourceRow;
        }

        return _targetRow;
    }

    public void Cleanup()
    {
        _decodedMask = null;
        _targetRow = null;
        _bufferedSourceRow = -1;
        _decoder.Cleanup();
    }

    private void FillTargetRow(int sourceRow)
    {
        if (_decodedMask == null || _targetRow == null)
        {
            return;
        }

        ReadOnlySpan<byte> source = _decodedMask.GetRow(sourceRow);
        Span<byte> destination = _targetRow;
        bool matchesTargetWidth = _decodedMask.Width == _targetWidth;

        if (matchesTargetWidth && _maskSampleStride == 1)
        {
            source.Slice(0, _targetWidth).CopyTo(destination);
            return;
        }

        if (!matchesTargetWidth)
        {
            int maximumSourceColumn = _decodedMask.Width - 1;

            for (int x = 0; x < _targetWidth; x++)
            {
                int sourceColumn = Math.Min(maximumSourceColumn, (int)(x * _horizontalScale));
                destination[x] = source[sourceColumn * _maskSampleStride];
            }

            return;
        }

        for (int x = 0; x < _targetWidth; x++)
        {
            destination[x] = source[x * _maskSampleStride];
        }
    }
}
