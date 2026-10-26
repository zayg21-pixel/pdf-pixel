using PdfPixel.Jpx.Parsing;

using System;
using System.IO;

namespace PdfPixel.Jpx;

public sealed partial class JpxHeader
{
    /// <summary>
    /// Parses JPEG 2000 header from the provided data.
    /// Automatically detects JP2 wrapper vs raw codestream format.
    /// </summary>
    public static JpxHeader Parse(in ReadOnlySpan<byte> data)
    {
        if (data.Length < 8)
        {
            throw new InvalidDataException("Insufficient data for JPEG 2000 header.");
        }

        JpxHeader header = new();

        // Check if this is a JP2 file (with box structure) or raw codestream
        if (JpxHeaderParser.IsJp2Format(data))
        {
            JpxHeaderParser.ParseJp2Format(data, header);
        }
        else
        {
            header.IsRawCodestream = true;
            JpxHeaderParser.ParseRawCodestream(data, 0, header);
        }

        return header;
    }
}
