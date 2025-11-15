using PdfReader.Color.ColorSpace;
using PdfReader.Models;
using PdfReader.Streams;
using PdfReader.Text;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfReader.Imaging.Model;

/// <summary>
/// Represents a PDF Image XObject with all its properties and data.
/// Parsed values are populated in FromXObject to keep this data object immutable from outside.
/// </summary>
public class PdfImage
{
    /// <summary>
    /// Gets the underlying <see cref="PdfObject"/> that serves as the source for this instance.
    /// </summary>
    public PdfObject SourceObject { get; internal set; }

    /// <summary>
    /// Returns the raw image data decoded from the PDF stream after reversing the /Filter chain.
    /// </summary>
    /// <returns></returns>
    public ReadOnlyMemory<byte> GetImageData() => SourceObject.DecodeAsMemory();

    /// <summary>
    /// Retrieves the image data as a stream (for large images).
    /// </summary>
    public Stream GetImageDataStream() => SourceObject.DecodeAsStream();

    /// <summary>
    /// Image width in pixels (/Width).
    /// </summary>
    public int Width { get; internal set; }

    /// <summary>
    /// Image height in pixels (/Height).
    /// </summary>
    public int Height { get; internal set; }

    /// <summary>
    /// Number of bits per color component (/BitsPerComponent).
    /// Valid values are typically 1, 2, 4, 8, or 16 depending on the color space.
    /// </summary>
    public int BitsPerComponent { get; internal set; }

    /// <summary>
    /// Image is decoded from a soft mask (/Image XObject with /Subtype /Image and /SMask key in the parent image).
    /// </summary>
    public bool IsSoftMask { get; internal set; }

    /// <summary>
    /// Color space (/ColorSpace). Resolved to a strongly-typed converter for sample interpretation.
    /// </summary>
    public PdfColorSpaceConverter ColorSpaceConverter { get; internal set; }

    /// <summary>
    /// Debug-friendly name for this image (resource name in /XObject dictionary when available).
    /// </summary>
    public PdfString Name { get; internal set; }

    /// <summary>
    /// Simplified image type classification derived from /Filter (e.g., JPEG, JPEG2000, CCITT, JBIG2, Raw).
    /// </summary>
    public PdfImageType Type { get; internal set; } = PdfImageType.Raw;

    /// <summary>
    /// Parsed /DecodeParms entries (single dictionary or first image-related entry when array) used by certain filters and predictors.
    /// </summary>
    public PdfDecodeParameters DecodeParms { get; internal set; }

    /// <summary>
    /// True when explicit image masking is enabled (/ImageMask true).
    /// </summary>
    public bool HasImageMask { get; internal set; }

    /// <summary>
    /// Color key mask array (/Mask array) flattened to integer sample codes. Null when /Mask is not an array.
    /// Values are in the raw sample value domain for each component prior to any /Decode mapping.
    /// </summary>
    public int[] MaskArray { get; internal set; }

    /// <summary>
    /// Per-component decode mapping array (/Decode) as floats: [d0, d1] per component.
    /// </summary>
    public float[] DecodeArray { get; internal set; }

    /// <summary>
    /// Indicates whether interpolation should be applied when scaling the image (/Interpolate).
    /// </summary>
    public bool Interpolate { get; internal set; }

    /// <summary>
    /// Strongly-typed rendering intent parsed from /Intent. Defaults to RelativeColorimetric when not specified.
    /// </summary>
    public PdfRenderingIntent RenderingIntent { get; internal set; } = PdfRenderingIntent.RelativeColorimetric;

    /// <summary>
    /// Raw /Matte array from the image dictionary, if present.
    /// </summary>
    public float[] MatteArray { get; internal set; }

    /// <summary>
    /// The soft mask image associated with this image, if any.
    /// </summary>
    public PdfImage SoftMask { get; internal set; }

    /// <summary>
    /// Update the image color space converter when the actual component count extracted from a decoded
    /// image stream (e.g. JPEG SOF) does not match the current converter's component count.
    /// This is a defensive fix-up for malformed PDFs where /ColorSpace is inconsistent with the encoded data.
    /// Only device color spaces (Gray, RGB, CMYK) are auto-corrected. Non-device (Cal*, ICCBased, Indexed, etc.)
    /// converters are preserved to avoid discarding profile or calibration data – a mismatch in those cases is
    /// logged by callers but not overridden here.
    /// </summary>
    /// <param name="componentCount">The component count discovered in the encoded image (1,3,4 are supported).</param>
    public void UpdateColorSpace(int componentCount)
    {
        if (componentCount <= 0)
        {
            return;
        }

        var current = ColorSpaceConverter;
        if (current != null && current.Components == componentCount)
        {
            return; // Already consistent.
        }

        // Only auto-fix for standard device component counts.
        switch (componentCount)
        {
            case 1:
            {
                if (current == null || !(current is DeviceGrayConverter))
                {
                    ColorSpaceConverter = DeviceGrayConverter.Instance;
                }
                break;
            }
            case 3:
            {
                // Replace only if null or clearly wrong (different component size or a different device set).
                if (current == null || current.Components != 3 || !(current is DeviceRgbConverter))
                {
                    ColorSpaceConverter = DeviceRgbConverter.Instance;
                }
                break;
            }
            case 4:
            {
                if (current == null || current.Components != 4 || !(current is DeviceCmykConverter))
                {
                    ColorSpaceConverter = DeviceCmykConverter.Instance;
                }
                break;
            }
            default:
            {
                // Unsupported component count for auto-correction – leave as-is.
                break;
            }
        }
    }

    /// <summary>
    /// Explicitly replace the current <see cref="ColorSpaceConverter"/> with the provided converter.
    /// This helper is used when a higher-level parser (e.g., embedded ICC profile detection) determines
    /// a more accurate color space than the originally declared one. The method validates the argument
    /// and avoids unnecessary assignment when the instance is already identical.
    /// </summary>
    /// <param name="colorSpace">The new color space converter to install. Ignored if null.</param>
    public void UpdateColorSpace(PdfColorSpaceConverter colorSpace)
    {
        if (colorSpace == null)
        {
            return;
        }

        if (ReferenceEquals(ColorSpaceConverter, colorSpace))
        {
            return; // No change needed.
        }

        ColorSpaceConverter = colorSpace;
    }

    /// <summary>
    /// Create a PdfImage from XObject data.
    /// </summary>
    public static PdfImage FromXObject(PdfObject imageXObject, PdfPage page, PdfString name, bool isSoftMask)
    {
        int bitsPerComponent = imageXObject.Dictionary.GetIntegerOrDefault(PdfTokens.BitsPerComponentKey);
        int defaultComponents = GetDefaultComponents(bitsPerComponent);

        var image = new PdfImage
        {
            SourceObject = imageXObject,
            Width = imageXObject.Dictionary.GetIntegerOrDefault(PdfTokens.WidthKey),
            Height = imageXObject.Dictionary.GetIntegerOrDefault(PdfTokens.HeightKey),
            BitsPerComponent = bitsPerComponent,
            IsSoftMask = isSoftMask,
            ColorSpaceConverter = page.Cache.ColorSpace.ResolveByValue(imageXObject.Dictionary.GetValue(PdfTokens.ColorSpaceKey), defaultComponents),
            Name = name
        };

        image.HasImageMask = imageXObject.Dictionary.GetBooleanOrDefault(PdfTokens.ImageMaskKey);
        image.Interpolate = imageXObject.Dictionary.GetBooleanOrDefault(PdfTokens.InterpolateKey);

        image.DecodeArray = imageXObject.Dictionary.GetArray(PdfTokens.DecodeKey)?.GetFloatArray();
        image.MaskArray = imageXObject.Dictionary.GetArray(PdfTokens.MaskKey)?.GetIntegerArray();

        // Parse /Matte as raw float array (for dematting at render time)
        image.MatteArray = imageXObject.Dictionary.GetArray(PdfTokens.MatteKey)?.GetFloatArray();

        // Parse /Intent and set RenderingIntent
        image.RenderingIntent = imageXObject.Dictionary.GetName(PdfTokens.IntentKey).AsEnum<PdfRenderingIntent>();

        // Type from Filter
        List<PdfFilterType> filters = PdfStreamDecoder.GetFilters(imageXObject);

        if (filters.Count > 0)
        {
            image.Type = MapImageType(filters[filters.Count - 1]);
        }

        var decodeParmsDict = imageXObject.Dictionary.GetDictionary(PdfTokens.DecodeParmsKey);
        if (decodeParmsDict != null)
        {
            image.DecodeParms = PdfDecodeParameters.FromDictionary(decodeParmsDict);
        }
        else
        {
            var decodeParmsArray = imageXObject.Dictionary.GetArray(PdfTokens.DecodeParmsKey);
            if (decodeParmsArray != null)
            {
                // image DecodeParms corresponds to the last entry in the array
                var imageDecodeParamsDictionary = decodeParmsArray.GetDictionary(decodeParmsArray.Count - 1);

                if (imageDecodeParamsDictionary != null)
                {
                    image.DecodeParms = PdfDecodeParameters.FromDictionary(imageDecodeParamsDictionary);
                }
            }
        }

        // Parse /SMask as a soft mask image if present
        var softMaskObject = imageXObject.Dictionary.GetObject(PdfTokens.SoftMaskKey);
        if (softMaskObject != null)
        {
            image.SoftMask = FromXObject(softMaskObject, page, name, isSoftMask: true);
        }

        return image;
    }

    private static int GetDefaultComponents(int bitsPerComponent)
    {
        return bitsPerComponent switch
        {
            1 => 1,
            _ => 3,
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
            _ => PdfImageType.Raw,
        };
    }
}