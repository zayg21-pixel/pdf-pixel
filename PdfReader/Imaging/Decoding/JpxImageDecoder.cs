using Microsoft.Extensions.Logging;
using PdfReader.Imaging.Model;
using PdfReader.Rendering.State;
using SkiaSharp;

namespace PdfReader.Imaging.Decoding;

/// <summary>
/// Provides functionality for decoding images in the JPEG 2000 (JPX) format.
/// </summary>
/// <remarks>Use this class to read and decode JPX image files, which are commonly used for high-quality image
/// storage and transmission. This class is typically used in applications that require support for the JPEG 2000
/// standard.</remarks>
public class JpxImageDecoder : PdfImageDecoder
{
    public JpxImageDecoder(PdfImage image, ILoggerFactory loggerFactory)
        : base(image, loggerFactory)
    {
    }

    public override SKImage Decode(PdfGraphicsState state, SKCanvas canvas)
    {
        return null;
        //WELL, fat chance.
        //var data = Image.GetImageDataStream();
        //var j2KImage = CoreJ2K.J2kImage.FromStream(data, new CoreJ2K.Configuration.J2KDecoderConfiguration { UseColorSpace = false });
        //using var bitmap = j2KImage.As<SKBitmap>();
        //return SKImage.FromBitmap(bitmap);
    }
}
