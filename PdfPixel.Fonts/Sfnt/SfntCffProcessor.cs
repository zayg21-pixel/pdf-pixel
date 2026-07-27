using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Cff;
using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Reads and writes the binary form of the SFNT "CFF " table (CFF-flavored OpenType fonts), by
/// delegating to <see cref="CffTypefaceReader"/> and <see cref="CffTypefaceWriter"/>.
/// </summary>
public class SfntCffProcessor
{
    private readonly CffTypefaceReader _reader;
    private readonly CffTypefaceWriter _writer = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SfntCffProcessor"/> class.
    /// </summary>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    public SfntCffProcessor(ILoggerFactory loggerFactory) => _reader = new CffTypefaceReader(loggerFactory);

    /// <summary>
    /// Parses the raw "CFF " table bytes into a <see cref="CffTypeface"/>.
    /// </summary>
    /// <param name="data">The raw "CFF " table bytes.</param>
    public CffTypeface? Read(in ReadOnlyMemory<byte> data) => _reader.Read(data);

    /// <summary>
    /// Serializes a <see cref="CffTypeface"/> back into "CFF " table bytes.
    /// </summary>
    /// <param name="typeface">The typeface to write.</param>
    public byte[] Write(CffTypeface typeface) => _writer.Write(typeface);
}
