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
public class PdfImage : PdfXObject
{
    internal PdfImage(PdfObject imageXObject, in PdfReference reference, bool isInlineImage)
        : base(imageXObject, reference, PdfXObjectSubtype.Image)
    {
        PdfDictionary dictionary = imageXObject.Dictionary;
        IsInlineImage = isInlineImage;
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
            PdfImage? maskImage = GetImage(dictionary, PdfTokens.MaskKey);

            // Only an /ImageMask true stream is a stencil mask; any other stream leaves
            // the base image unmasked.
            if (maskImage != null && maskImage.HasImageMask)
            {
                StencilMask = maskImage;
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

        SoftMask = GetImage(dictionary, PdfTokens.SoftMaskKey);
    }

    /// <summary>
    /// True for an inline image (BI/ID/EI), whose <see cref="PdfXObject.Reference"/> names object 0.
    /// </summary>
    public bool IsInlineImage { get; }

    /// <summary>
    /// Gets the self-contained stream source for this image.
    /// </summary>
    public PdfObjectStream Stream { get; }

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
    /// The image held by <paramref name="key"/> of <paramref name="owner"/>, resolved from the
    /// document's XObject cache when available, or <see langword="null"/> when absent or not an image.
    /// An entry without /Subtype is taken as an image.
    /// </summary>
    internal static PdfImage? GetImage(PdfDictionary owner, in PdfString key)
        => owner.Document.ObjectCache.GetOrCreate<PdfXObject>(owner, key, FromImageEntryObject, requiresStream: true) as PdfImage;

    /// <summary>
    /// Creates an image from an inline image object (BI/ID/EI).
    /// </summary>
    /// <param name="inlineObject">The inline image object.</param>
    internal static PdfImage FromInlineObject(PdfObject inlineObject) => new(inlineObject, new PdfReference(0), isInlineImage: true);

    private static PdfXObject? FromImageEntryObject(PdfObject imageObject)
    {
        if (imageObject.Reference == null || imageObject.Dictionary.HasKey(PdfTokens.SubtypeKey))
        {
            return FromObject(imageObject);
        }

        return new PdfImage(imageObject, imageObject.Reference.Value, isInlineImage: false);
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
