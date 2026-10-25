using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Streams;
using System;

namespace PdfPixel.Fonts;

/// <summary>
/// An embedded font program (/FontFile, /FontFile2 or /FontFile3) of a font descriptor, shared by every
/// descriptor that refers to the same stream, together with the typeface it is loaded as.
/// </summary>
public sealed class PdfFontFile : IDisposable
{
    private PdfFontFile(PdfObject fontFileObject, PdfFontFileFormat format)
    {
        Format = format;
        Stream = fontFileObject.Stream;
        ClearTextLength = fontFileObject.Dictionary.GetIntegerOrDefault(PdfTokens.Length1);
        EncryptedLength = fontFileObject.Dictionary.GetIntegerOrDefault(PdfTokens.Length2);
        FixedContentLength = fontFileObject.Dictionary.GetIntegerOrDefault(PdfTokens.Length3);
    }

    /// <summary>
    /// Format of the font program: Type1 for /FontFile, TrueType for /FontFile2, and the /Subtype of the
    /// stream for /FontFile3.
    /// </summary>
    public PdfFontFileFormat Format { get; }

    /// <summary>
    /// The font program stream.
    /// </summary>
    public PdfObjectStream Stream { get; }

    /// <summary>
    /// Type1 font stream segment length for the clear-text portion (/Length1).
    /// </summary>
    public int ClearTextLength { get; }

    /// <summary>
    /// Type1 font stream segment length for the binary portion (/Length2).
    /// </summary>
    public int EncryptedLength { get; }

    /// <summary>
    /// Type1 font stream segment length for the fixed-content trailer (/Length3).
    /// </summary>
    public int FixedContentLength { get; }

    /// <summary>
    /// Typeface the font program is loaded as, or <see langword="null"/> before it is loaded.
    /// </summary>
    public IPdfTypeface? Typeface { get; internal set; }

    /// <summary>
    /// Returns the font program <paramref name="fontDescriptor"/> embeds, using the document's cache if available.
    /// Only one font program is expected; /FontFile2 is preferred over /FontFile3, then /FontFile.
    /// </summary>
    /// <param name="fontDescriptor">The font descriptor dictionary.</param>
    /// <returns>The font program, or <see langword="null"/> when the descriptor embeds none.</returns>
    internal static PdfFontFile? GetFontFile(PdfDictionary fontDescriptor)
    {
        PdfDocumentObjectCache objectCache = fontDescriptor.Document.ObjectCache;

        PdfFontFile? trueTypeFile = objectCache.GetOrCreate(fontDescriptor, PdfTokens.FontFile2Key, FromTrueTypeObject, requiresStream: true);
        if (trueTypeFile != null)
        {
            return trueTypeFile;
        }

        PdfFontFile? subtypeFile = objectCache.GetOrCreate(fontDescriptor, PdfTokens.FontFile3Key, FromSubtypeObject, requiresStream: true);
        if (subtypeFile != null)
        {
            return subtypeFile;
        }

        return objectCache.GetOrCreate(fontDescriptor, PdfTokens.FontFileKey, FromType1Object, requiresStream: true);
    }

    private static PdfFontFile FromTrueTypeObject(PdfObject fontFileObject) => new(fontFileObject, PdfFontFileFormat.TrueType);

    private static PdfFontFile FromSubtypeObject(PdfObject fontFileObject)
        => new(fontFileObject, fontFileObject.Dictionary.GetNameOrDefault(PdfTokens.SubtypeKey).AsEnum<PdfFontFileFormat>());

    private static PdfFontFile FromType1Object(PdfObject fontFileObject) => new(fontFileObject, PdfFontFileFormat.Type1);

    /// <inheritdoc/>
    public void Dispose() => Typeface?.Dispose();
}
