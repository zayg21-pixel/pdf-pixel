using PdfPixel.Color.ColorSpace;
using PdfPixel.Models;
using PdfPixel.Streams;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.Imaging.Model;

/// <summary>
/// Represents a PDF Image XObject with all its properties and data.
/// </summary>
public class PdfImage
{
    private PdfImage(PdfObject imageXObject)
    {
        PdfDictionary dictionary = imageXObject.Dictionary;
        SourceReference = imageXObject.Reference;
        Stream = imageXObject.Stream;

        int bitsPerComponent = dictionary.GetIntegerOrDefault(PdfTokens.BitsPerComponentKey);
        if (bitsPerComponent == 0)
        {
            // allowed for 1 bit images.
            bitsPerComponent = 1;
        }

        BitsPerComponent = bitsPerComponent;
        Width = dictionary.GetIntegerOrDefault(PdfTokens.WidthKey);
        Height = dictionary.GetIntegerOrDefault(PdfTokens.HeightKey);
        ColorSpaceReference = PdfColorSpaceReference.FromDictionary(dictionary, PdfTokens.ColorSpaceKey);
        HasImageMask = dictionary.GetBooleanOrDefault(PdfTokens.ImageMaskKey);
        Interpolate = dictionary.GetBooleanOrDefault(PdfTokens.InterpolateKey);
        float[]? decodeArray = dictionary.GetArray(PdfTokens.DecodeKey)?.GetFloatArray();
        Decode = (decodeArray != null) ? PdfRange.FromArray(decodeArray) : null;

        PdfArray? maskArray = dictionary.GetArray(PdfTokens.MaskKey);
        if (maskArray != null)
        {
            MaskArray = maskArray.GetIntegerArray();
        }
        else
        {
            PdfObject? maskObject = dictionary.GetObject(PdfTokens.MaskKey);
            if (maskObject?.HasStream == true)
            {
                PdfImage maskImage = GetImage(maskObject);

                // Only an /ImageMask true stream is a stencil mask; any other stream leaves
                // the base image unmasked.
                if (maskImage.HasImageMask)
                {
                    StencilMask = maskImage;
                }
            }
        }

        MatteArray = dictionary.GetArray(PdfTokens.MatteKey)?.GetFloatArray();
        SoftMaskInData = MapSoftMaskInData(dictionary.GetIntegerOrDefault(PdfTokens.SoftMaskInDataKey));
        RenderingIntent = dictionary.GetNameOrDefault(PdfTokens.IntentKey).AsEnum<PdfRenderingIntent>();

        var type = PdfImageType.Raw;
        List<PdfFilterType> filters = imageXObject.Stream.Filters;
        if (filters.Count > 0)
        {
            type = MapImageType(filters[filters.Count - 1]);
        }

        Type = type;

        PdfDictionary? decodeParmsDict = dictionary.GetDictionary(PdfTokens.DecodeParmsKey);
        if (decodeParmsDict != null)
        {
            DecodeParms = PdfDecodeParameters.FromDictionary(decodeParmsDict);
        }
        else
        {
            PdfArray? decodeParmsArray = dictionary.GetArray(PdfTokens.DecodeParmsKey);
            if (decodeParmsArray != null)
            {
                // image DecodeParms corresponds to the last entry in the array
                PdfDictionary? imageDecodeParamsDictionary = decodeParmsArray.GetDictionary(decodeParmsArray.Count - 1);

                if (imageDecodeParamsDictionary != null)
                {
                    DecodeParms = PdfDecodeParameters.FromDictionary(imageDecodeParamsDictionary);
                }
            }
        }

        PdfObject? softMaskObject = dictionary.GetObject(PdfTokens.SoftMaskKey);
        if (softMaskObject != null)
        {
            SoftMask = GetImage(softMaskObject);
        }
    }

    /// <summary>
    /// Source object reference.
    /// </summary>
    public PdfReference SourceReference { get; }

    /// <summary>
    /// Gets the self-contained stream source for this image.
    /// </summary>
    public PdfObjectStream Stream { get; }

    /// <summary>
    /// Returns the raw image data decoded from the PDF stream after reversing the /Filter chain.
    /// </summary>
    public ReadOnlyMemory<byte> GetImageData(IPdfExecutionObserver? observer) => Stream.DecodeAsMemory(observer);

    /// <summary>
    /// Retrieves the image data as a stream (for large images).
    /// </summary>
    public Stream GetImageDataStream() => Stream.DecodeAsStream();

    /// <summary>
    /// Image width in pixels (/Width).
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Image height in pixels (/Height).
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Number of bits per color component (/BitsPerComponent).
    /// Valid values are typically 1, 2, 4, 8, or 16 depending on the color space.
    /// </summary>
    public int BitsPerComponent { get; }

    /// <summary>
    /// Colour space (/ColorSpace) as written, resolved on demand.
    /// </summary>
    internal PdfColorSpaceReference ColorSpaceReference { get; }

    /// <summary>
    /// Simplified image type classification derived from /Filter (e.g., JPEG, JPEG2000, CCITT, JBIG2, Raw).
    /// </summary>
    public PdfImageType Type { get; }

    /// <summary>
    /// Parsed /DecodeParms entries (single dictionary or first image-related entry when array) used by certain filters and predictors.
    /// </summary>
    public PdfDecodeParameters? DecodeParms { get; }

    /// <summary>
    /// True when explicit image masking is enabled (/ImageMask true).
    /// </summary>
    public bool HasImageMask { get; }

    /// <summary>
    /// Color key mask array (/Mask array) flattened to integer sample codes. Null when /Mask is not an array.
    /// Values are in the raw sample value domain for each component prior to any /Decode mapping.
    /// </summary>
    public int[]? MaskArray { get; }

    /// <summary>
    /// Per-component decode mapping (/Decode), one entry per component.
    /// </summary>
    public PdfRange[]? Decode { get; }

    /// <summary>
    /// Indicates whether interpolation should be applied when scaling the image (/Interpolate).
    /// </summary>
    public bool Interpolate { get; }

    /// <summary>
    /// Strongly-typed rendering intent parsed from /Intent. Defaults to RelativeColorimetric when not specified.
    /// </summary>
    public PdfRenderingIntent RenderingIntent { get; }

    /// <summary>
    /// Raw /Matte array from the image dictionary, if present.
    /// </summary>
    public float[]? MatteArray { get; }

    /// <summary>
    /// Where a JPEG 2000 image's opacity comes from (/SMaskInData). Defaults to
    /// <see cref="PdfImageSoftMaskInData.None"/> when the entry is absent or out of range.
    /// </summary>
    public PdfImageSoftMaskInData SoftMaskInData { get; }

    /// <summary>
    /// The soft mask image associated with this image, if any.
    /// </summary>
    public PdfImage? SoftMask { get; }

    /// <summary>
    /// External stencil mask image (/Mask referencing an image XObject).
    /// Where the mask sample is 1 the corresponding image sample is painted; where 0 it is masked out.
    /// Null when /Mask is not an image XObject reference.
    /// </summary>
    public PdfImage? StencilMask { get; }

    /// <summary>
    /// Returns a parsed PdfImage instance for the given image XObject, using the document's cache if available.
    /// </summary>
    /// <param name="pdfObject">PDF image XObject.</param>
    /// <returns>PdfImage instance, resolved from cache or newly parsed.</returns>
    public static PdfImage GetImage(PdfObject pdfObject)
    {
        if (pdfObject == null)
        {
            throw new ArgumentNullException(nameof(pdfObject));
        }

        if (pdfObject.Reference.IsValid)
        {
            Dictionary<PdfReference, PdfImage> cache = pdfObject.Document.ObjectCache.Images;
            if (cache.TryGetValue(pdfObject.Reference, out PdfImage? cachedImage))
            {
                return cachedImage;
            }
        }

        PdfImage image = new(pdfObject);

        if (pdfObject.Reference.IsValid)
        {
            Dictionary<PdfReference, PdfImage> cache = pdfObject.Document.ObjectCache.Images;
            cache[pdfObject.Reference] = image;
        }

        return image;
    }

    private static PdfImageSoftMaskInData MapSoftMaskInData(int value)
    {
        return value switch
        {
            1 => PdfImageSoftMaskInData.Alpha,
            2 => PdfImageSoftMaskInData.PremultipliedAlpha,
            _ => PdfImageSoftMaskInData.None
        };
    }

    private static PdfImageType MapImageType(PdfFilterType filterType)
    {
        return filterType switch
        {
            PdfFilterType.DCTDecode => PdfImageType.JPEG,
            PdfFilterType.JPXDecode => PdfImageType.JPEG2000,
            PdfFilterType.CCITTFaxDecode => PdfImageType.CCITT,
            PdfFilterType.JBIG2Decode => PdfImageType.JBIG2,
            _ => PdfImageType.Raw
        };
    }
}
