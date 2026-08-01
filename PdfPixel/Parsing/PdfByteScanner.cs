using System;
using System.IO;

namespace PdfPixel.Parsing;

/// <summary>
/// Shared low-level byte-sequence scanning used to locate PDF structural keywords
/// (xref, startxref, trailer) directly against the document stream.
/// </summary>
internal static class PdfByteScanner
{
    /// <summary>
    /// Determines whether the specified byte sequence occurs at the given absolute file position.
    /// </summary>
    /// <param name="stream">The document stream to read from.</param>
    /// <param name="position">Absolute byte offset in the stream.</param>
    /// <param name="sequence">Sequence to compare.</param>
    public static bool MatchesAt(Stream stream, long position, in ReadOnlySpan<byte> sequence)
    {
        if (sequence.Length == 0)
        {
            return true;
        }

        if (position < 0 || position + sequence.Length > stream.Length)
        {
            return false;
        }

        stream.Position = position;

        var buffer = new byte[sequence.Length];
        int bytesRead = stream.Read(buffer, 0, buffer.Length);
        if (bytesRead != buffer.Length)
        {
            return false;
        }

        return new ReadOnlySpan<byte>(buffer).SequenceEqual(sequence);
    }

    /// <summary>
    /// Scans backward from the end of the stream for the last occurrence of the specified byte sequence.
    /// </summary>
    /// <param name="stream">The document stream to read from.</param>
    /// <param name="sequence">Sequence to search for.</param>
    /// <returns>The absolute file position of the match, or -1 if not found.</returns>
    public static long LocateLast(Stream stream, in ReadOnlySpan<byte> sequence)
    {
        for (long scanIndex = stream.Length - sequence.Length; scanIndex >= 0; scanIndex--)
        {
            if (MatchesAt(stream, scanIndex, sequence))
            {
                return scanIndex;
            }
        }

        return -1;
    }
}
