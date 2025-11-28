using Microsoft.Extensions.Logging;
using PdfReader.Imaging.Ccitt;
using PdfReader.Imaging.Model;
using PdfReader.Imaging.Processing;
using SkiaSharp;
using System;

namespace PdfReader.Imaging.Decoding;

/// <summary>
/// CCITT Fax (CCITTFaxDecode) image decoder.
/// Streams CCITT rows through <see cref="CcittRowDecoder"/> and hands each packed 1-bit row
/// to <see cref="PdfImageRowProcessor"/> for /Decode, masking and color conversion.
/// No legacy full-buffer fallback is retained.
/// </summary>
internal sealed class CcittImageDecoder : PdfImageDecoder
{
    private readonly int _k;
    private readonly bool _endOfLine;
    private readonly bool _byteAlign;
    private readonly bool _blackIs1;
    private readonly bool _endOfBlock;
    private readonly int _columns;
    private readonly int _rows;

    public CcittImageDecoder(PdfImage image, ILoggerFactory loggerFactory) : base(image, loggerFactory)
    {
        if (loggerFactory == null)
        {
            throw new ArgumentNullException(nameof(loggerFactory));
        }

        var parameters = image.DecodeParms;
        _k = parameters?.K ?? 0;
        _endOfLine = parameters?.EndOfLine ?? false;
        _byteAlign = parameters?.EncodedByteAlign ?? false;
        _blackIs1 = parameters?.BlackIs1 ?? false;
        _endOfBlock = parameters?.EndOfBlock ?? false;
        _columns = parameters?.Columns ?? image.Width;
        _rows = parameters?.Rows ?? image.Height;
    }

    public override SKImage Decode()
    {
        int width = _columns;
        int height = _rows;
        if (width <= 0 || height <= 0)
        {
            Logger.LogError("Invalid CCITT dimensions {Width}x{Height}.", width, height);
            return null;
        }

        try
        {
            ReadOnlyMemory<byte> encoded = Image.GetImageData();
            if (encoded.IsEmpty)
            {
                Logger.LogError("CCITT image data empty (Name={Name}).", Image.Name);
                return null;
            }

            // Initialize row processor (8-bit pipeline; will read packed 1-bit samples per row).
            using var rowProcessor = new PdfImageRowProcessor(Image, LoggerFactory.CreateLogger<PdfImageRowProcessor>());
            rowProcessor.InitializeBuffer();

            // Row decoder (produces packed 1-bit rows, MSB-first).
            var rowDecoder = new CcittRowDecoder(
                encoded.Span,
                width,
                height,
                _blackIs1,
                _k,
                _endOfLine,
                _byteAlign,
                _endOfBlock);

            int packedRowBytes = rowDecoder.RowStride;
            byte[] rowBuffer = new byte[packedRowBytes];

            int rowIndex = 0;
            while (rowDecoder.DecodeNextRow(rowBuffer))
            {
                rowProcessor.WriteRow(rowIndex, rowBuffer);
                rowIndex++;
            }

            if (rowIndex != height)
            {
                Logger.LogWarning("CCITT row decoder ended early (Decoded={Decoded} Expected={Expected}) (Name={Name}).", rowIndex, height, Image.Name);
            }

            return rowProcessor.GetDecoded();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "CCITT streaming decode failed (Name={Name}).", Image.Name);
            return null;
        }
    }
}
