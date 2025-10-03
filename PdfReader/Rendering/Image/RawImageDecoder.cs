using SkiaSharp;
using System;
using System.Runtime.InteropServices;
using PdfReader.Rendering.Image.Raw;
using PdfReader.Rendering.Image.PostProcessing;

namespace PdfReader.Rendering.Image
{
    /// <summary>
    /// Decodes raw PDF image data (after filter decompression) into an SKImage.
    /// Optimized processing pipeline:
    ///  1. Decode: Combined predictor reversal and resampling to 8-bit (single optimized pass)
    ///  2. PostProcess: Applies /Decode mapping, color conversion, masking, and palette handling
    /// </summary>
    public class RawImageDecoder : PdfImageDecoder
    {
        public RawImageDecoder(PdfImage image) : base(image)
        {
        }

        public override SKImage Decode()
        {
            // Validate input parameters
            if (!ValidateImageParameters())
            {
                return null;
            }

            // Combined Step: Decode (UndoPredictor + Resample to 8-bit) in a single optimized pass
            ReadOnlyMemory<byte> sourceData = Image.GetImageData();
            IntPtr decodedBuffer = PdfRawImageUtilities.Decode(Image, sourceData);
            if (decodedBuffer == IntPtr.Zero)
            {
                Console.Error.WriteLine("RawImageDecoder.Decode: decoding failed.");
                return null;
            }

            if (PdfImagePostProcessor.RequiresPostProcess(Image))
            {
                try
                {
                    // PostProcess - Apply decode mapping, color conversion, masking, palette handling
                    return PdfImagePostProcessor.PostProcess(Image, decodedBuffer);
                }
                catch
                {
                    Console.Error.WriteLine("RawImageDecoder.Decode: post processing failed.");
                    return null;
                }
                finally
                {
                    // Always free the decoded buffer
                    Marshal.FreeHGlobal(decodedBuffer);
                }
            }
            else
            {
                try
                {
                    return PdfImagePostProcessor.CreateImage(Image, decodedBuffer);
                }
                catch
                {
                    // only free buffer on exception, otherwise caller takes ownership of SKImage and its pixel memory
                    Marshal.FreeHGlobal(decodedBuffer);
                    return null;
                }
            }
        }
    }
}
