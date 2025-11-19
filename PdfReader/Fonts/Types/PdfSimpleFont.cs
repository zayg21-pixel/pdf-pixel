using Microsoft.Extensions.Logging;
using PdfReader.Fonts.Cff;
using PdfReader.Fonts.Mapping;
using PdfReader.Fonts.TrueType;
using PdfReader.Fonts.Type1;
using PdfReader.Models;
using SkiaSharp;
using System;

namespace PdfReader.Fonts.Types;

/// <summary>
/// Simple fonts: Type1, TrueType, MMType1 (excluding Type3)
/// Self-contained fonts with direct character-to-glyph mapping
/// Limited to 256 characters (single-byte encoding).
/// </summary>
public class PdfSimpleFont : PdfSingleByteFont
{
    private readonly ILogger<PdfSimpleFont> _logger;
    private readonly SKTypeface _typeface;
    private readonly IByteCodeToGidMapper _mapper;
    private readonly bool _substituted;

    /// <summary>
    /// Constructor for simple fonts - lightweight operations only
    /// </summary>
    /// <param name="fontObject">PDF dictionary containing the font definition</param>
    public PdfSimpleFont(PdfDictionary fontDictionary) : base(fontDictionary)
    {
        _logger = fontDictionary.Document.LoggerFactory.CreateLogger<PdfSimpleFont>();
        (_typeface, _mapper, _substituted) = GetTypefaceAndMapper();
    }

    public override bool IsEmbedded => FontDescriptor?.HasEmbeddedFont == true;

    internal protected override SKTypeface Typeface => _typeface;

    private (SKTypeface, IByteCodeToGidMapper, bool) GetTypefaceAndMapper()
    {
        try
        {
            switch (FontDescriptor?.FontFileFormat)
            {
                case PdfFontFileFormat.Type1:
                {
                    var cffInfo = Type1ToCffConverter.GetCffFont(FontDescriptor);
                    var typefaceData = CffOpenTypeWrapper.Wrap(FontDescriptor, cffInfo);
                    var typeface = SKTypeface.FromData(SKData.CreateCopy(typefaceData));

                    if (typeface == null)
                    {
                        _logger.LogWarning("Failed to create typeface from embedded Type1 font data for font '{FontName}'", BaseFont);
                        throw new InvalidOperationException("Failed to create typeface from embedded Type1 font data.");
                    }

                    // Per spec, if no encoding is specified, use the font's built-in encoding
                    if (Encoding.BaseEncoding == PdfFontEncoding.Unknown && Encoding.Differences.Count == 0)
                    {
                        Encoding.Update(cffInfo.Encoding, cffInfo.CodeToName);
                    }

                    // If still unknown, default to StandardEncoding
                    if (Encoding.BaseEncoding == PdfFontEncoding.Unknown)
                    {
                        Encoding.Update(PdfFontEncoding.StandardEncoding, default);
                    }

                    var mapper = new CffByteCodeToGidMapper(cffInfo, FontDescriptor.Flags, Encoding);

                    return (typeface, mapper, false);
                }
                case PdfFontFileFormat.Type1C:
                {
                    var cffSidMapper = new CffSidGidMapper(Document.LoggerFactory);
                    var cffBytes = FontDescriptor.FontFileObject.DecodeAsMemory();

                    if (!cffSidMapper.TryParseNameKeyed(cffBytes, out var cffInfo))
                    {
                        _logger.LogWarning("Failed to parse embedded Type1C font data for font '{FontName}'", BaseFont);
                        throw new InvalidOperationException("Failed to parse embedded Type1C font data.");
                    }

                    // Per spec, if no encoding is specified, use the font's built-in encoding
                    if (Encoding.BaseEncoding == PdfFontEncoding.Unknown && Encoding.Differences.Count == 0)
                    {
                        Encoding.Update(cffInfo.Encoding, cffInfo.CodeToName);
                    }

                    // If still unknown, default to StandardEncoding
                    if (Encoding.BaseEncoding == PdfFontEncoding.Unknown)
                    {
                        Encoding.Update(PdfFontEncoding.StandardEncoding, default);
                    }

                    var typefaceData = CffOpenTypeWrapper.Wrap(FontDescriptor, cffInfo);
                    var typeface = SKTypeface.FromData(SKData.CreateCopy(typefaceData));

                    var mapper = new CffByteCodeToGidMapper(cffInfo, FontDescriptor.Flags, Encoding);

                    return (typeface, mapper, false);
                }

                case PdfFontFileFormat.TrueType:
                {
                    var typeface = SKTypeface.FromStream(FontDescriptor.FontFileObject.DecodeAsStream());
                    var snftTables = SntfFontTableParser.GetSntfFontTables(typeface);

                    if (Encoding.BaseEncoding == PdfFontEncoding.Unknown)
                    {
                        Encoding.Update(PdfFontEncoding.StandardEncoding, default);
                    }

                    var mapper = new SntfByteCodeToGidMapper(snftTables, FontDescriptor.Flags, substituted: false, Encoding, ToUnicodeCMap);

                    return (typeface, mapper, false);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading embedded font for font '{FontName}', will attempt substitution", BaseFont);
        }

        var substitutedTypeface = Document.FontSubstitutor.SubstituteTypeface(BaseFont, FontDescriptor);
        var substitutedSnftTables = SntfFontTableParser.GetSntfFontTables(substitutedTypeface);

        // we're substituting, so always use StandardEncoding if none specified
        if (Encoding.BaseEncoding == PdfFontEncoding.Unknown)
        {
            Encoding.Update(PdfFontEncoding.StandardEncoding, default);
        }

        var snftMapper = new SntfByteCodeToGidMapper(substitutedSnftTables, default, substituted: true, Encoding, ToUnicodeCMap);

        return (substitutedTypeface, snftMapper, true);
    }

    /// <summary>
    /// Gets the glyph ID (GID) for the specified character code in a simple font.
    /// Returns 0 if no valid GID is found.
    /// </summary>
    /// <param name="code">The character code to map to a glyph ID.</param>
    /// <returns>The glyph ID (GID) for the character code, or 0 if not found.</returns>
    public override ushort GetGid(PdfCharacterCode code)
    {
        if (code == null)
        {
            return 0;
        }

        if (_mapper == null)
        {
            return 0;
        }

        return _mapper.GetGid((byte)code);
    }

    protected override void Dispose(bool disposing)
    {
        if (_substituted)
        {
            _typeface.Dispose();
        }

        base.Dispose(disposing);
    }
}