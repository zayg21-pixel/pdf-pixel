using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Cff;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Fonts.TrueType;
using PdfPixel.Fonts.Type1;
using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
using System;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Handles simple (single-byte) fonts including Type 1, Type 1C, and TrueType variants.
/// Loads the embedded font data, builds the appropriate glyph-ID mapper, and resolves encoding.
/// </summary>
public class PdfSimpleFont : PdfSingleByteFont
{
    private readonly ILogger<PdfSimpleFont> _logger;
    private readonly SKTypeface _typeface;
    private readonly IByteCodeToGidMapper _mapper;

    internal PdfSimpleFont(PdfObject fontObject)
        : base(fontObject)
    {
        _logger = fontObject.Document.LoggerFactory.CreateLogger<PdfSimpleFont>();
        (_typeface, _mapper) = GetTypefaceAndMapper();
    }

    /// <summary>
    /// Returns the advance width for the specified character code.
    /// Uses the font metrics table first, and falls back to the glyph-ID mapper when the metrics entry is zero.
    /// </summary>
    /// <param name="code">The character code to retrieve the width for.</param>
    /// <returns>The advance width in user space units.</returns>
    public override float GetWidth(PdfCharacterCode code)
    {
        float width = base.GetWidth(code);

        // TODO: [MEDIUM] we need to use same fallaback for CID fonts
        if (width == 0 && _mapper != null)
        {
            width = (float)_mapper.GetWidth((byte)(code));
        }

        return width;
    }

    /// <summary>
    /// The embedded or substituted SkiaSharp typeface for this simple font.
    /// </summary>
    protected internal override SKTypeface Typeface => _typeface;

    private (SKTypeface, IByteCodeToGidMapper) GetTypefaceAndMapper()
    {
        try
        {
            switch (FontDescriptor?.FontFileFormat)
            {
                case PdfFontFileFormat.Type1:
                {
                    CffInfo? cffInfo = Type1ToCffConverter.GetCffFont(FontDescriptor);
                    byte[]? typefaceData = CffOpenTypeWrapper.Wrap(FontDescriptor, cffInfo);
                    SKTypeface typeface = SKTypeface.FromData(SKData.CreateCopy(typefaceData));

                    if (typeface == null)
                    {
                        _logger.LogWarning("Failed to create typeface from embedded Type1 font data for font '{FontName}'", BaseFont);
                        throw new InvalidOperationException("Failed to create typeface from embedded Type1 font data.");
                    }

                    if (cffInfo == null)
                    {
                        _logger.LogWarning("Failed to get CFF font info for font '{FontName}'", BaseFont);
                        throw new InvalidOperationException("Failed to get CFF font info for font.");
                    }

                    if (Encoding.BaseEncoding == PdfFontEncoding.Unknown && Encoding.Differences.Count == 0)
                    {
                        Encoding.Update(cffInfo.Encoding, cffInfo.CodeToName);
                    }

                    if (Encoding.BaseEncoding == PdfFontEncoding.Unknown)
                    {
                        PdfFontEncoding encoding = SingleByteEncodings.GetEncodingByName(BaseFont) ?? PdfFontEncoding.StandardEncoding;
                        Encoding.Update(encoding, default);
                    }

                    CffByteCodeToGidMapper mapper = new(cffInfo, FontDescriptor.Flags, Encoding);

                    return (typeface, mapper);
                }
                case PdfFontFileFormat.Type1C:
                {
                    CffSidGidMapper cffSidMapper = new(Document.LoggerFactory);
                    ReadOnlyMemory<byte> cffBytes = FontDescriptor.FontFileObject?.DecodeAsMemory() ?? ReadOnlyMemory<byte>.Empty;

                    if (!cffSidMapper.TryParseNameKeyed(cffBytes, out CffInfo? cffInfo) || cffInfo == null)
                    {
                        _logger.LogWarning("Failed to parse embedded Type1C font data for font '{FontName}'", BaseFont);
                        throw new InvalidOperationException("Failed to parse embedded Type1C font data.");
                    }

                    if (Encoding.BaseEncoding == PdfFontEncoding.Unknown && Encoding.Differences.Count == 0)
                    {
                        Encoding.Update(cffInfo.Encoding, cffInfo.CodeToName);
                    }

                    if (Encoding.BaseEncoding == PdfFontEncoding.Unknown)
                    {
                        Encoding.Update(PdfFontEncoding.StandardEncoding, default);
                    }

                    byte[]? typefaceData = CffOpenTypeWrapper.Wrap(FontDescriptor, cffInfo);
                    SKTypeface typeface = SKTypeface.FromData(SKData.CreateCopy(typefaceData));

                    CffByteCodeToGidMapper mapper = new(cffInfo, FontDescriptor.Flags, Encoding);

                    return (typeface, mapper);
                }
                case PdfFontFileFormat.TrueType:
                {
                    SKTypeface typeface = SKTypeface.FromStream(FontDescriptor.FontFileObject?.DecodeAsStream());
                    SfntFontTables sfntTables = SfntFontTableParser.GetSfntFontTables(typeface);

                    if (Encoding.BaseEncoding == PdfFontEncoding.Unknown)
                    {
                        Encoding.Update(PdfFontEncoding.WinAnsiEncoding, default);
                    }

                    SfntByteCodeToGidMapper mapper = new(sfntTables, FontDescriptor.Flags, substituted: false, Encoding, ToUnicodeCMap);

                    return (typeface, mapper);
                }
            }
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading embedded font for font '{FontName}', will attempt substitution", BaseFont);
        }
#pragma warning restore CA1031

        if (Encoding.BaseEncoding == PdfFontEncoding.Unknown)
        {
            PdfFontEncoding encoding = SingleByteEncodings.GetEncodingByName(BaseFont) ?? PdfFontEncoding.StandardEncoding;
            Encoding.Update(encoding, default);
        }

        return default;
    }

    /// <summary>
    /// Returns the glyph ID (GID) for the specified character code by consulting the byte-code-to-GID mapper.
    /// Returns 0 when no mapper is available or the code is <see langword="null"/>.
    /// </summary>
    /// <param name="code">The character code to map to a glyph ID.</param>
    /// <returns>The glyph ID, or 0 if not found.</returns>
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

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        _typeface?.Dispose();
        base.Dispose(disposing);
    }
}
