using Microsoft.Extensions.Logging;
using PdfReader.Rendering.Image.Raw;
using SkiaSharp;
using System;
using System.Runtime.InteropServices;

namespace PdfReader.Rendering.Image
{
    /// <summary>
    /// Decodes a raw PDF image (an image whose stream has already had its /Filter chain decoded).
    /// A "raw" image in this context is any /Image XObject that is not handled by a specialized
    /// codec (e.g. JPEG, CCITT, JBIG2, JPEG2000) and therefore consists of packed sample data that may:
    ///  * Use a PNG/TIFF predictor (/DecodeParms.Predictor) requiring per‑row reconstruction.
    ///  * Pack sub‑byte samples (1/2/4 bpc) that must be expanded to an 8‑bit working domain.
    ///  * Contain multi‑component pixels in an arbitrary device / calibrated / indexed color space.
    ///
    /// Responsibilities of this decoder:
    ///  1. Undo predictor (if any) and normalize packed samples to an 8‑bit per component buffer.
    ///  2. Hand the normalized buffer to <see cref="PdfImageProcessor"/> for /Decode mapping, masking,
    ///     palette expansion, color space conversion, and final SKImage creation.
    ///
    /// The method returns null on failure (errors are logged) so that page rendering can continue.
    /// </summary>
    public class RawImageDecoder : PdfImageDecoder
    {
        public RawImageDecoder(PdfImage image, ILoggerFactory loggerFactory) : base(image, loggerFactory)
        {
        }

        /// <summary>
        /// Decode the raw image stream into an <see cref="SKImage"/> or return null if decoding fails.
        /// </summary>
        public override unsafe SKImage Decode()
        {
            if (!ValidateImageParameters())
            {
                return null;
            }

            ReadOnlyMemory<byte> sourceData = Image.GetImageData();
            IntPtr decodedBuffer = IntPtr.Zero;
            try
            {
                decodedBuffer = PdfRawImageUtilities.Decode(Image, sourceData);
                if (decodedBuffer == IntPtr.Zero)
                {
                    Logger.LogError("RawImage decode produced a null buffer (Name={Name}).", Image.Name);
                    return null;
                }

                return Processor.CreateImage(new ReadOnlySpan<byte>(decodedBuffer.ToPointer(), sourceData.Length));
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "RawImage decode failed (Name={Name}).", Image.Name);
                return null;
            }
            finally
            {
                Marshal.FreeHGlobal(decodedBuffer);
            }
        }
    }
}
