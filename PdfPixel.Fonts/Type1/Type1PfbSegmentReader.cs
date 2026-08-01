using System;
using System.IO;

namespace PdfPixel.Fonts.Type1;

/// <summary>
/// Reassembles a PFB-wrapped Type1 font program into the same contiguous (cleartext header, eexec-encrypted
/// body) layout a PFA-style embedding already has. A PDF's <c>FontFile</c> stream is required to be PFA
/// (spec section 9.9), but some producers embed PFB instead; this recovers the segment boundaries PFB
/// stores explicitly, which PFA instead leaves to <c>Length1</c>/<c>Length2</c>.
/// </summary>
internal static class Type1PfbSegmentReader
{
    private const byte SegmentMarker = 0x80;
    private const byte SegmentTypeAscii = 1;
    private const byte SegmentTypeBinary = 2;
    private const byte SegmentTypeEnd = 3;
    private const int SegmentHeaderLength = 6;

    /// <summary>
    /// Concatenates every ASCII (type 1) segment into a header buffer and every binary (type 2) segment
    /// into an encrypted-body buffer, then returns them joined as one buffer alongside each part's length --
    /// the same layout a PFA embedding's <c>Length1</c>/<c>Length2</c> describe.
    /// </summary>
    public static Type1RawFontProgram ExtractSegments(in ReadOnlyMemory<byte> pfbData)
    {
        ReadOnlySpan<byte> span = pfbData.Span;
        using MemoryStream header = new();
        using MemoryStream encrypted = new();

        int position = 0;
        while (position + SegmentHeaderLength <= span.Length && span[position] == SegmentMarker)
        {
            byte segmentType = span[position + 1];
            if (segmentType == SegmentTypeEnd)
            {
                break;
            }

            int length = span[position + 2] | (span[position + 3] << 8) | (span[position + 4] << 16) | (span[position + 5] << 24);
            position += SegmentHeaderLength;

            if (length < 0 || position + length > span.Length)
            {
                break;
            }

            byte[] segmentBytes = span.Slice(position, length).ToArray();
            if (segmentType == SegmentTypeAscii)
            {
                header.Write(segmentBytes, 0, segmentBytes.Length);
            }
            else if (segmentType == SegmentTypeBinary)
            {
                encrypted.Write(segmentBytes, 0, segmentBytes.Length);
            }

            position += length;
        }

        byte[] headerBytes = header.ToArray();
        byte[] encryptedBytes = encrypted.ToArray();

        var combined = new byte[headerBytes.Length + encryptedBytes.Length];
        headerBytes.CopyTo(combined, 0);
        encryptedBytes.CopyTo(combined, headerBytes.Length);

        return new Type1RawFontProgram(combined, headerBytes.Length, encryptedBytes.Length, length3: 0);
    }
}
