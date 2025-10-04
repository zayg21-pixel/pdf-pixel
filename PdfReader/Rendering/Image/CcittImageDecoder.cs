using Microsoft.Extensions.Logging;
using PdfReader.Rendering.Image.Ccitt;
using PdfReader.Rendering.Image.Processing;
using SkiaSharp;
using System;
using System.Runtime.InteropServices;

namespace PdfReader.Rendering.Image
{
    /// <summary>
    /// CCITT Fax (CCITTFaxDecode) image decoder.
    /// Decodes the compressed bitstream to a 1-bpc packed buffer (respecting /DecodeParms) and
    /// delegates interpretation (/Decode array, masking, color conversion) to <see cref="PdfImageProcessor"/>.
    /// Failure cases log and return null (decoder is permissive: upstream may decide to continue page rendering).
    /// </summary>
    internal sealed class CcittImageDecoder : PdfImageDecoder
    {
        private readonly int _k;
        private readonly bool _endOfLine;
        private readonly bool _byteAlign;
        private readonly bool _blackIs1; // filter-level black polarity (bit meaning in encoded data)
        private readonly bool _endOfBlock;
        private readonly int _columns;
        private readonly int _rows;

        public CcittImageDecoder(PdfImage image, ILoggerFactory loggerFactory) : base(image, loggerFactory)
        {
            if (loggerFactory == null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            var parameters = image.DecodeParms != null && image.DecodeParms.Count > 0 ? image.DecodeParms[0] : null;
            _k = parameters?.K ?? 0;
            _endOfLine = parameters?.EndOfLine ?? false;
            _byteAlign = parameters?.EncodedByteAlign ?? false;
            _blackIs1 = parameters?.BlackIs1 ?? false;
            _endOfBlock = parameters?.EndOfBlock ?? false;
            _columns = parameters?.Columns ?? image.Width;
            _rows = parameters?.Rows ?? image.Height;
        }

        public override unsafe SKImage Decode()
        {
            try
            {
                int width = _columns;
                int height = _rows;
                if (width <= 0 || height <= 0)
                {
                    Logger.LogError("Invalid CCITT dimensions {Width}x{Height}.", width, height);
                    return null;
                }

                ReadOnlyMemory<byte> encoded = Image.GetImageData();
                var unmanaged = CcittRaster.AllocateBuffer(width, height, _blackIs1, out int byteCount);
                var buffer = new Span<byte>(unmanaged.ToPointer(), byteCount);

                try
                {
                    if (_k == 0)
                    {
                        CcittG3OneDDecoder.Decode(encoded.Span, buffer, width, height, _blackIs1, _endOfLine, _byteAlign);
                    }
                    else if (_k < 0)
                    {
                        CcittG4TwoDDecoder.Decode(encoded.Span, buffer, width, height, _blackIs1, _endOfBlock);
                    }
                    else
                    {
                        CcittG32DDecoder.Decode(encoded.Span, buffer, width, height, _blackIs1, _k, _endOfLine, _byteAlign);
                    }

                    return Processor.CreateImage(buffer);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "CCITT entropy decode failed.");
                    return null;
                }
                finally
                {
                    Marshal.FreeHGlobal(unmanaged);
                }

            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unexpected CCITT decode failure.");
                return null;
            }
        }
    }
}
