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

public class PdfSimpleFont : PdfSingleByteFont
{
    private readonly ILogger<PdfSimpleFont> _logger;
    private readonly SKTypeface _typeface;
    private readonly IByteCodeToGidMapper _mapper;

    public PdfSimpleFont(PdfObject fontObject) : base(fontObject)
    {
        _logger = fontObject.Document.LoggerFactory.CreateLogger<PdfSimpleFont>();
        (_typeface, _mapper) = GetTypefaceAndMapper();
    }

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

    internal protected override SKTypeface Typeface => _typeface;

    private (SKTypeface, IByteCodeToGidMapper) GetTypefaceAndMapper()
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

                    if (Encoding.BaseEncoding == PdfFontEncoding.Unknown && Encoding.Differences.Count == 0)
                    {
                        Encoding.Update(cffInfo.Encoding, cffInfo.CodeToName);
                    }

                    if (Encoding.BaseEncoding == PdfFontEncoding.Unknown)
                    {
                        var encoding = SingleByteEncodings.GetEncodingByName(BaseFont) ?? PdfFontEncoding.StandardEncoding;
                        Encoding.Update(encoding, default);
                    }

                    var mapper = new CffByteCodeToGidMapper(cffInfo, FontDescriptor.Flags, Encoding);

                    return (typeface, mapper);
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

                    if (Encoding.BaseEncoding == PdfFontEncoding.Unknown && Encoding.Differences.Count == 0)
                    {
                        Encoding.Update(cffInfo.Encoding, cffInfo.CodeToName);
                    }

                    if (Encoding.BaseEncoding == PdfFontEncoding.Unknown)
                    {
                        Encoding.Update(PdfFontEncoding.StandardEncoding, default);
                    }

                    var typefaceData = CffOpenTypeWrapper.Wrap(FontDescriptor, cffInfo);
                    var typeface = SKTypeface.FromData(SKData.CreateCopy(typefaceData));

                    var mapper = new CffByteCodeToGidMapper(cffInfo, FontDescriptor.Flags, Encoding);

                    return (typeface, mapper);
                }

                case PdfFontFileFormat.TrueType:
                {
                    var typeface = SKTypeface.FromStream(FontDescriptor.FontFileObject.DecodeAsStream());
                    var sfntTables = SfntFontTableParser.GetSfntFontTables(typeface);

                    if (Encoding.BaseEncoding == PdfFontEncoding.Unknown)
                    {
                        Encoding.Update(PdfFontEncoding.WinAnsiEncoding, default);
                    }

                    var mapper = new SfntByteCodeToGidMapper(sfntTables, FontDescriptor.Flags, substituted: false, Encoding, ToUnicodeCMap);

                    return (typeface, mapper);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error loading embedded font for font '{FontName}', will attempt substitution", BaseFont);
        }

        if (Encoding.BaseEncoding == PdfFontEncoding.Unknown)
        {
            var encoding = SingleByteEncodings.GetEncodingByName(BaseFont) ?? PdfFontEncoding.StandardEncoding;
            Encoding.Update(encoding, default);
        }

        return default;
    }

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
        _typeface?.Dispose();
        base.Dispose(disposing);
    }
}