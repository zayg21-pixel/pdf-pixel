using PdfReader.Rendering.Color;
using SkiaSharp;
using System;

namespace PdfReader.Rendering.Image
{
    public abstract class PdfImageDecoder
    {
        public PdfImageDecoder(PdfImage image)
        {
            Image = image ?? throw new ArgumentNullException(nameof(image));
        }

        public PdfImage Image { get; }

        public virtual bool HasDecodeParams => Image.DecodeParms != null && Image.DecodeParms.Count > 0 && (Image.DecodeParms[0].Predictor ?? 1) > 1;

        /// <summary>
        /// Factory: create an appropriate image decoder for the given <see cref="PdfImage"/> based on its <see cref="PdfImage.Type"/>.
        /// Returns null for unsupported encodings.
        /// </summary>
        /// <param name="pdfImage">The image descriptor to decode.</param>
        /// <returns>A concrete <see cref="PdfImageDecoder"/> instance, or null if unsupported.</returns>
        public static PdfImageDecoder GetDecoder(PdfImage pdfImage)
        {
            if (pdfImage == null)
            {
                Console.Error.WriteLine("PdfImageDecoder.GetDecoder: pdfImage is null. Returning null.");
                return null;
            }

            switch (pdfImage.Type)
            {
                case PdfImageType.Raw:
                    return new RawImageDecoder(pdfImage);

                case PdfImageType.JPEG:
                    return new JpegImageDecoder(pdfImage);

                case PdfImageType.JPEG2000:
                    Console.Error.WriteLine("PdfImageDecoder.GetDecoder: JPEG2000 not implemented in new pipeline. Returning null.");
                    return null;

                case PdfImageType.CCITT:
                    return new CcittImageDecoder(pdfImage);

                case PdfImageType.JBIG2:
                    Console.Error.WriteLine("PdfImageDecoder.GetDecoder: JBIG2 not implemented in new pipeline. Returning null.");
                    return null;

                default:
                    Console.Error.WriteLine($"PdfImageDecoder.GetDecoder: Unknown image type {pdfImage.Type}. Returning null.");
                    return null;
            }
        }

        public abstract SKImage Decode();

        /// <summary>
        /// Validate image parameters and return key values needed for processing.
        /// </summary>
        protected bool ValidateImageParameters()
        {
            var width = Image.Width;
            var height = Image.Height;
            var bitsPerComponent = Image.BitsPerComponent;
            var converter = Image.ColorSpaceConverter;

            if (width <= 0 || height <= 0 || bitsPerComponent <= 0)
            {
                Console.Error.WriteLine("RawImageDecoder.Decode: invalid image dimensions or bits per component.");
                return false;
            }

            if (converter == null)
            {
                Console.Error.WriteLine("RawImageDecoder.Decode: missing color space converter.");
                return false;
            }

            if (Image.HasImageMask && bitsPerComponent != 1)
            {
                Console.Error.WriteLine("RawImageDecoder.Decode: /ImageMask must have BitsPerComponent=1.");
                return false;
            }

            if (converter is IndexedConverter && bitsPerComponent == 16)
            {
                Console.Error.WriteLine("RawImageDecoder.Decode: 16 bpc not allowed for Indexed images.");
                return false;
            }

            if (bitsPerComponent != 1 && bitsPerComponent != 2 && bitsPerComponent != 4 &&
                bitsPerComponent != 8 && bitsPerComponent != 16)
            {
                Console.Error.WriteLine("RawImageDecoder.Decode: unsupported bitsPerComponent value.");
                return false;
            }

            return true;
        }
    }
}
