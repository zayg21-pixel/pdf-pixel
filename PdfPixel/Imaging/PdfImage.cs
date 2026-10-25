using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands.Image;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Encoding;
using PdfPixel.Imaging.Processing;
using PdfPixel.Measures;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Rendering.Operators;
using PdfPixel.Resources;
using PdfPixel.Streams;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.Imaging;

/// <summary>
/// Represents a PDF Image XObject with all its properties and data.
/// </summary>
public class PdfImage : PdfXObject
{
    private const int DefaultInlineBitsPerComponent = 8;

    private static readonly PdfNameValue DefaultInlineColorSpace = new(PdfColorSpaceType.DeviceGray.AsPdfString());

    private readonly PdfDictionary? _dictionary;
    private List<PdfPointData>? _pointData;

    internal PdfImage(PdfObject imageXObject, in PdfReference reference)
        : this(
            imageXObject.Document,
            imageXObject.Dictionary,
            reference,
            imageXObject.Stream,
            imageXObject.Dictionary.GetIntegerOrDefault(PdfTokens.WidthKey),
            imageXObject.Dictionary.GetIntegerOrDefault(PdfTokens.HeightKey),
            imageXObject.Dictionary.GetIntegerOrDefault(PdfTokens.BitsPerComponentKey),
            PdfColorSpaceReference.FromDictionary(imageXObject.Dictionary, PdfTokens.ColorSpaceKey),
            imageXObject.Dictionary.GetBooleanOrDefault(PdfTokens.ImageMaskKey),
            imageXObject.Dictionary.GetBooleanOrDefault(PdfTokens.InterpolateKey),
            imageXObject.Dictionary.GetArray(PdfTokens.DecodeKey),
            imageXObject.Dictionary.GetName(PdfTokens.IntentKey))
    {
    }

    /// <summary>
    /// Initializes a new <see cref="PdfImage"/> from the entries read from its dictionary, or from the operand stack for an
    /// inline image, which has no <paramref name="dictionary"/> and so none of the entries only an image XObject may have.
    /// </summary>
    private PdfImage(
        IPdfDocumentInternal document,
        PdfDictionary? dictionary,
        in PdfReference reference,
        PdfObjectStream stream,
        int width,
        int height,
        int bitsPerComponent,
        in PdfColorSpaceReference colorSpaceReference,
        bool hasImageMask,
        bool interpolate,
        PdfArray? decode,
        PdfString? intent)
        : base(document, dictionary, reference, PdfXObjectSubtype.Image)
    {
        _dictionary = dictionary;
        IsInlineImage = dictionary == null;
        Stream = stream;
        Width = width;
        Height = height;

        // allowed for 1 bit images.
        BitsPerComponent = (bitsPerComponent == 0) ? 1 : bitsPerComponent;
        ColorSpaceReference = colorSpaceReference;
        HasImageMask = hasImageMask;
        Interpolate = interpolate;
        float[]? decodeArray = decode?.GetFloatArray();
        Decode = (decodeArray != null) ? PdfRange.FromArray(decodeArray) : null;
        RenderingIntent = (intent != null) ? intent.Value.AsEnum<PdfRenderingIntent>() : PdfRenderingIntent.RelativeColorimetric;

        var type = PdfImageType.Raw;
        IReadOnlyList<PdfStreamFilter> filters = stream.Filters;
        if (filters.Count > 0)
        {
            PdfStreamFilter imageFilter = filters[filters.Count - 1];
            type = MapImageType(imageFilter.Filter);
            DecodeParameters = imageFilter.DecodeParameters;
        }

        Type = type;

        if (dictionary == null)
        {
            return;
        }

        PdfArray? maskArray = dictionary.GetArray(PdfTokens.MaskKey);
        if (maskArray != null)
        {
            ColorKeyMask = maskArray.GetIntegerArray();
        }
        else
        {
            PdfImage? maskImage = GetImage(dictionary, PdfTokens.MaskKey);

            // Only an /ImageMask true stream is a stencil mask; any other stream leaves
            // the base image unmasked.
            if (maskImage != null && maskImage.HasImageMask)
            {
                Mask = maskImage;
            }
        }

        Matte = dictionary.GetArray(PdfTokens.MatteKey)?.GetFloatArray();
        SoftMaskInData = MapSoftMaskInData(dictionary.GetIntegerOrDefault(PdfTokens.SoftMaskInDataKey));
        SoftMask = GetImage(dictionary, PdfTokens.SoftMaskKey);
        Measure = PdfMeasure.GetMeasure(dictionary, PdfTokens.MeasureKey);
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
    /// Color space (/ColorSpace) as written, resolved on demand.
    /// </summary>
    internal PdfColorSpaceReference ColorSpaceReference { get; }

    /// <summary>
    /// Simplified image type classification derived from /Filter (e.g., JPEG, JPEG2000, CCITT, JBIG2, Raw).
    /// </summary>
    public PdfImageType Type { get; }

    /// <summary>
    /// Parsed /DecodeParms entry of the last filter in the chain, the one that decodes the image samples,
    /// or null when that filter has none.
    /// </summary>
    public PdfDecodeParameters? DecodeParameters { get; }

    /// <summary>
    /// True when explicit image masking is enabled (/ImageMask true).
    /// </summary>
    public bool HasImageMask { get; }

    /// <summary>
    /// Color key mask array (/Mask array) flattened to integer sample codes. Null when /Mask is not an array.
    /// Values are in the raw sample value domain for each component prior to any /Decode mapping.
    /// </summary>
    public int[]? ColorKeyMask { get; }

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
    public float[]? Matte { get; }

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
    public PdfImage? Mask { get; }

    /// <summary>
    /// Scale and units that apply to the image (/Measure), or <see langword="null"/> when absent.
    /// </summary>
    public PdfMeasure? Measure { get; }

    /// <summary>
    /// Returns the extended geospatial data of the image (/PtData). Empty when absent.
    /// </summary>
    public IReadOnlyList<PdfPointData> GetPointData()
    {
        if (_pointData == null)
        {
            _pointData = (_dictionary != null) ? PdfPointData.FromOwner(_dictionary) : [];
        }

        return _pointData;
    }

    /// <summary>
    /// Decodes the whole image to 8-bit pixels at its stored size, or reduced to fit within
    /// <see cref="PdfImageDecodingParameters.MaximumSize"/>, with device color spaces resolved through the
    /// document's output intent.
    /// </summary>
    /// <param name="parameters">Masks to apply, color conversion options and the size to fit within.</param>
    /// <param name="observer">Observer notified as decoding progresses, which can cancel it.</param>
    /// <returns>
    /// The decoded pixels, laid out as <see cref="PdfDecodedImage.ColorFormat"/> and
    /// <see cref="PdfDecodedImage.AlphaType"/> describe.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> or <paramref name="observer"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The image data produced no pixels.</exception>
    public PdfDecodedImage DecodePixels(PdfImageDecodingParameters parameters, IPdfExecutionObserver observer)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        if (observer == null)
        {
            throw new ArgumentNullException(nameof(observer));
        }

        ImageDecodingContext context = new(this, Document.ContentHost, parameters);
        PdfImageDecoder? decoder = PdfImageDecoder.GetDecoder(this, context, Document.LoggerFactory);

        if (decoder == null)
        {
            throw new ArgumentException($"Decoder for image {Type} is not defined.");
        }

        PdfTileInfo tileInfo = new(new PdfIntegerSize(Width, Height), new PdfIntegerSize(context.DefaultTileSize, context.DefaultTileSize));
        PdfImageTileDecodingPass pass = new(decoder, tileInfo, Document.LoggerFactory);
        MaskAlphaRowSource? alphaRowSource = MaskAlphaRowSource.Create(decoder, tileInfo, Document.LoggerFactory);

        PdfImage? mask = decoder.AppliedMask;
        PdfIntegerSize sampleSize = new(
            Math.Max(Width, mask?.Width ?? 0),
            Math.Max(Height, mask?.Height ?? 0));

        PdfMatrix decodingMatrix = GetDecodingMatrix(sampleSize, parameters.MaximumSize);
        PdfIntegerSize outputSize = PdfImageCommandUtilities.GetScaledSize(decodingMatrix, sampleSize) ?? sampleSize;

        pass.Begin(decodingMatrix, outputSize, tileIndexesToDecode: null, observer);
        alphaRowSource?.Initialize(outputSize, tileIndexesToDecode: null, observer);

        PdfImageTile[]? tiles = pass.DecodeNextTiles(alphaRowSource, observer);

        pass.End();
        alphaRowSource?.Cleanup();

        if (tiles == null)
        {
            throw new InvalidOperationException($"Image {Reference} produced no pixels.");
        }

        return tiles[0].Image ?? throw new InvalidOperationException($"Image {Reference} produced no pixels.");
    }

    /// <summary>
    /// Decodes the whole image as <see cref="DecodePixels"/> does and encodes the pixels as an 8-bit PNG file
    /// with unpremultiplied alpha.
    /// </summary>
    /// <param name="parameters">Masks to apply, color conversion options and the size to fit within.</param>
    /// <param name="observer">Observer notified as decoding progresses, which can cancel it.</param>
    /// <returns>The complete PNG file.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="parameters"/> or <paramref name="observer"/> is null.</exception>
    /// <exception cref="InvalidOperationException">The image data produced no pixels.</exception>
    public byte[] DecodePng(PdfImageDecodingParameters parameters, IPdfExecutionObserver observer)
    {
        PdfDecodedImage decoded = DecodePixels(parameters, observer);

        return PdfDecodedImagePngEncoder.Encode(decoded);
    }

    /// <summary>
    /// The image held by <paramref name="key"/> of <paramref name="owner"/>, resolved from the
    /// document's XObject cache when available, or <see langword="null"/> when absent or not an image.
    /// An entry without /Subtype is taken as an image.
    /// </summary>
    internal static PdfImage? GetImage(PdfDictionary owner, in PdfString key)
        => owner.Document.ObjectCache.GetOrCreate<PdfXObject>(owner, key, FromImageEntryObject, requiresStream: true) as PdfImage;

    /// <summary>
    /// Reads the inline image whose ID operator was just read from the content stream <paramref name="context"/> reads. Its
    /// entries are read from the <paramref name="operands"/> written between its BI and ID operators, under their full or
    /// abbreviated keys, and its data from the context up to its EI operator; an image that is not a stencil mask and declares
    /// no /BitsPerComponent or /ColorSpace is read as 8-bit DeviceGray. When the entries and data repeat an inline image the
    /// document read before, that image is returned; otherwise the document keeps the image under its
    /// <see cref="InlineImageKey"/>. Either way the parser is told, through <see cref="PdfContentStreamCursor.SkipPositions"/>,
    /// to resume at the EI operator.
    /// </summary>
    /// <param name="context">The content stream the image is read from.</param>
    /// <param name="operands">The entries written between the BI and ID operators.</param>
    /// <returns>
    /// The inline image as the document keeps it, or null when the end of its data is not found, in which case the rest of
    /// the stream is skipped.
    /// </returns>
    internal static PdfImage? FromInline(PdfParseContext context, PdfOperandStack operands)
    {
        if (operands == null)
        {
            throw new ArgumentNullException(nameof(operands));
        }

        PdfContentStreamCursor cursor = context.Cursor ?? throw new InvalidOperationException("Inline images are read only from a content stream context.");
        IPdfDocumentInternal document = context.Document ?? throw new InvalidOperationException("Inline images are read only from a content stream context.");
        PdfImageCache imageCache = document.ObjectCache.Images;
        int imageStart = cursor.InlineImageStart;
        int dataStart = cursor.InlineImageDataStart;

        int? width = null;
        int? height = null;
        int? bitsPerComponent = null;
        bool? imageMask = null;
        bool? interpolate = null;
        PdfValue? colorSpace = null;
        PdfValue? decode = null;
        PdfValue? filter = null;
        PdfValue? decodeParms = null;
        PdfValue? intent = null;
        ReadOnlySpan<PdfContentStreamValue> entries = operands.AsSpan();

        for (int index = 0; index + 1 < entries.Length; index += 2)
        {
            PdfString? key = entries[index].GetPdfValue(context).AsName();

            if (key == null)
            {
                break;
            }

            PdfValue? value = entries[index + 1].GetPdfValue(context);

            switch (key.Value.AsEnum<PdfInlineImageProperty>())
            {
                case PdfInlineImageProperty.Width:
                {
                    width ??= value.AsInteger();
                    break;
                }
                case PdfInlineImageProperty.Height:
                {
                    height ??= value.AsInteger();
                    break;
                }
                case PdfInlineImageProperty.BitsPerComponent:
                {
                    bitsPerComponent ??= value.AsInteger();
                    break;
                }
                case PdfInlineImageProperty.ColorSpace:
                {
                    colorSpace ??= value;
                    break;
                }
                case PdfInlineImageProperty.Decode:
                {
                    decode ??= value;
                    break;
                }
                case PdfInlineImageProperty.DecodeParms:
                {
                    decodeParms ??= value;
                    break;
                }
                case PdfInlineImageProperty.Filter:
                {
                    filter ??= value;
                    break;
                }
                case PdfInlineImageProperty.ImageMask:
                {
                    imageMask ??= value.AsBoolean();
                    break;
                }
                case PdfInlineImageProperty.Interpolate:
                {
                    interpolate ??= value.AsBoolean();
                    break;
                }
                case PdfInlineImageProperty.Intent:
                {
                    intent ??= value;
                    break;
                }
            }
        }

        bool hasImageMask = imageMask ?? false;
        int resolvedBitsPerComponent = bitsPerComponent ?? (hasImageMask ? 1 : DefaultInlineBitsPerComponent);

        List<PdfStreamFilter> filters = PdfStreamDecoder.GetStreamFilters(filter, decodeParms);
        PdfFilterType? firstFilter = (filters.Count > 0) ? filters[0].Filter : null;
        int? rawDataLength = (firstFilter == null)
            ? ComputeRawDataLength(width ?? 0, height ?? 0, hasImageMask ? 1 : resolvedBitsPerComponent, (hasImageMask || colorSpace == null) ? 1 : GetComponentCount(colorSpace))
            : null;

        ReadOnlySpan<byte> content = context.Slice(dataStart, context.Length - dataStart);

        if (!PdfInlineImageDataScanner.TryFindData(content, firstFilter, rawDataLength, out int dataOffset, out int dataEnd, out int operatorStart))
        {
            cursor.SkipPositions = content.Length;

            return null;
        }

        byte[] data = content.Slice(dataOffset, dataEnd - dataOffset).ToArray();
        InlineImageKey inlineImageKey = new(context.Slice(imageStart, dataStart - imageStart), data);
        PdfImage? image = imageCache.GetInlineImage(inlineImageKey);

        if (image == null)
        {
            PdfValue? resolvedColorSpace = colorSpace ?? (hasImageMask ? null : DefaultInlineColorSpace);

            image = new PdfImage(
                document,
                null,
                new PdfReference(0),
                PdfObjectStream.FromInlineData(document, filters, data),
                width ?? 0,
                height ?? 0,
                resolvedBitsPerComponent,
                (resolvedColorSpace != null) ? PdfColorSpaceReference.FromValue(resolvedColorSpace) : default,
                hasImageMask,
                interpolate ?? false,
                decode.AsArray(),
                intent.AsName());

            imageCache.AddInlineImage(inlineImageKey, image);
        }

        cursor.SkipPositions = operatorStart;

        return image;
    }

    private static PdfXObject? FromImageEntryObject(PdfObject imageObject)
    {
        if (imageObject.Reference == null || imageObject.Dictionary.HasKey(PdfTokens.SubtypeKey))
        {
            return FromObject(imageObject);
        }

        return new PdfImage(imageObject, imageObject.Reference.Value);
    }

    /// <summary>
    /// Returns the length of unfiltered inline image data of the given size and sample layout,
    /// or null when the size or layout is not known.
    /// </summary>
    private static int? ComputeRawDataLength(int width, int height, int bitsPerComponent, int? components)
    {
        if (width <= 0 || height <= 0 || bitsPerComponent <= 0 || components == null || components.Value <= 0)
        {
            return null;
        }

        long bitsPerRow = (long)width * bitsPerComponent * components.Value;
        long rowBytes = (bitsPerRow + 7) / 8;
        long totalBytes = rowBytes * height;

        return (totalBytes > 0 && totalBytes <= int.MaxValue) ? (int)totalBytes : null;
    }

    /// <summary>
    /// Returns the number of components of the device or indexed color space an inline image names,
    /// or null for any other color space.
    /// </summary>
    private static int? GetComponentCount(PdfValue colorSpace)
    {
        PdfString? colorSpaceName = colorSpace.AsName();
        if (colorSpaceName == null)
        {
            return null;
        }

        switch (colorSpaceName.Value.AsEnum<PdfColorSpaceType>())
        {
            case PdfColorSpaceType.DeviceGray:
                return 1;

            case PdfColorSpaceType.DeviceRGB:
                return 3;

            case PdfColorSpaceType.DeviceCMYK:
                return 4;

            case PdfColorSpaceType.Indexed:
                return 1;

            default:
                return null;
        }
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

    /// <summary>
    /// Maps the unit square onto <paramref name="sampleSize"/>, reduced to fit within <paramref name="maximumSize"/>
    /// at its aspect ratio.
    /// </summary>
    private static PdfMatrix GetDecodingMatrix(in PdfIntegerSize sampleSize, PdfIntegerSize? maximumSize)
    {
        if (maximumSize != null)
        {
            float scale = Math.Min(
                (float)maximumSize.Value.Width / sampleSize.Width,
                (float)maximumSize.Value.Height / sampleSize.Height);

            if (scale < 1)
            {
                return PdfMatrix.CreateScale(
                    Math.Max(1, (int)(sampleSize.Width * scale)),
                    Math.Max(1, (int)(sampleSize.Height * scale)));
            }
        }

        return PdfMatrix.CreateScale(sampleSize.Width, sampleSize.Height);
    }
}
