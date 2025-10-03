using System;
using System.Runtime.InteropServices;
using SkiaSharp;
using PdfReader.Rendering.Image.Ccitt;
using PdfReader.Rendering.Image.PostProcessing;

namespace PdfReader.Rendering.Image
{
    /// <summary>
    /// CCITT Fax (CCITTFaxDecode) image decoder.
    /// Always delegates pixel interpretation (/Decode, masking, etc.) to the shared post-processing pipeline.
    /// Decoding steps:
    ///  1. Entropy decode CCITT stream into an expanded 8-bit buffer (values 0=black, 255=white) using neutral polarity.
    ///  2. Pack expanded bytes into a 1-bit-per-sample buffer honoring /DecodeParms BlackIs1 (bit meaning).
    ///  3. Invoke <see cref="PdfImagePostProcessor.PostProcess"/> for uniform handling.
    /// </summary>
    internal sealed class CcittImageDecoder : PdfImageDecoder
    {
        private readonly int _k;
        private readonly bool _endOfLine;
        private readonly bool _byteAlign;
        private readonly bool _blackIs1; // Specifies which bit value represents black in the encoded data (filter semantics)
        private readonly bool _endOfBlock;
        private readonly int _columns;
        private readonly int _rows;

        public CcittImageDecoder(PdfImage image) : base(image)
        {
            var parameters = image.DecodeParms != null && image.DecodeParms.Count > 0 ? image.DecodeParms[0] : null;
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
            try
            {
                int width = _columns;
                int height = _rows;
                if (width <= 0 || height <= 0)
                {
                    Console.Error.WriteLine("CcittImageDecoder: invalid dimensions.");
                    return null;
                }

                ReadOnlyMemory<byte> encoded = Image.GetImageData();
                byte[] packed;
                if (_k == 0)
                {
                    packed = CcittG3OneDDecoder.Decode(encoded.Span, width, height, _blackIs1, _endOfLine, _byteAlign);
                }
                else if (_k < 0)
                {
                    packed = CcittG4TwoDDecoder.Decode(encoded.Span, width, height, _blackIs1, _endOfBlock);
                }
                else
                {
                    packed = CcittG32DDecoder.Decode(encoded.Span, width, height, _blackIs1, _k, _endOfLine, _byteAlign);
                }

                if (packed == null)
                {
                    Console.Error.WriteLine("CcittImageDecoder: decode failed.");
                    return null;
                }

                // TODO: use "destination"IntPtr unmanaged'
                IntPtr unmanaged = Marshal.AllocHGlobal(packed.Length);
                Marshal.Copy(packed, 0, unmanaged, packed.Length);

                if (PdfImagePostProcessor.RequiresPostProcess(Image))
                {
                    try
                    {
                        // TODO; let PostProcess to free unmanaged?
                        return PdfImagePostProcessor.PostProcess(Image, unmanaged);
                    }
                    catch
                    {
                        return null;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(unmanaged);
                    }
                }
                else
                {
                    try
                    {
                        return PdfImagePostProcessor.CreateImage(Image, unmanaged);
                    }
                    catch
                    {
                        Marshal.FreeHGlobal(unmanaged);
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("CcittImageDecoder: failure " + ex.Message);
                return null;
            }
        }
    }
}
