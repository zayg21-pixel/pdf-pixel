using PdfPixel.Imaging.Jpg.Model;
using System;

namespace PdfPixel.Imaging.Jpg.Color;

internal class JpgIccProfileReader
{
    /// <summary>
    /// Reassembles ICC profile bytes from APP2 ICC_PROFILE segments gathered in the <see cref="JpgHeader"/>.
    /// Returns false (with null out parameter) if segments are missing, inconsistent, invalid, or if length overflows.
    /// This method is defensive and non-throwing for malformed inputs (except catastrophic runtime failures such as OOM).
    /// </summary>
    /// <param name="header">Parsed JPEG header.</param>
    /// <param name="profileBytes">Complete ICC profile byte array or null on failure.</param>
    public static bool TryAssembleIccProfile(JpgHeader header, out byte[] profileBytes)
    {
        if (header == null)
        {
            profileBytes = null;
            return false;
        }

        if (!header.HasIccProfile || header.IccProfileSegments == null || header.IccProfileSegments.Count == 0)
        {
            profileBytes = null;
            return false;
        }

        // Determine declared total segment count from first segment.
        int declaredTotal = header.IccProfileSegments[0].TotalSegments;
        if (declaredTotal <= 0 || declaredTotal > 255)
        {
            profileBytes = null;
            return false;
        }

        // Prepare storage for segment payloads in 1-based order (JPEG ICC spec sequences start at 1).
        var segmentPayloads = new byte[declaredTotal][];
        int totalLength = 0;

        for (int segmentIndex = 0; segmentIndex < header.IccProfileSegments.Count; segmentIndex++)
        {
            IccSegmentInfo segment = header.IccProfileSegments[segmentIndex];
            if (segment == null)
            {
                profileBytes = null;
                return false;
            }

            if (segment.TotalSegments != declaredTotal)
            {
                // Inconsistent total count across segments.
                profileBytes = null;
                return false;
            }

            int sequenceNumber = segment.SequenceNumber; // 1-based
            if (sequenceNumber <= 0 || sequenceNumber > declaredTotal)
            {
                profileBytes = null;
                return false;
            }

            if (segmentPayloads[sequenceNumber - 1] != null)
            {
                // Duplicate sequence number.
                profileBytes = null;
                return false;
            }

            byte[] data = segment.Data;
            if (data == null || data.Length == 0)
            {
                profileBytes = null;
                return false;
            }

            // Check for integer overflow before accumulation.
            if (data.Length > int.MaxValue - totalLength)
            {
                profileBytes = null;
                return false;
            }

            segmentPayloads[sequenceNumber - 1] = data;
            totalLength += data.Length;
        }

        // Verify that all segments were present.
        for (int i = 0; i < declaredTotal; i++)
        {
            if (segmentPayloads[i] == null)
            {
                profileBytes = null;
                return false;
            }
        }

        // Reassemble contiguously.
        var assembled = new byte[totalLength];
        int writeOffset = 0;
        for (int i = 0; i < segmentPayloads.Length; i++)
        {
            byte[] part = segmentPayloads[i];
            Buffer.BlockCopy(part, 0, assembled, writeOffset, part.Length);
            writeOffset += part.Length;
        }

        profileBytes = assembled;
        return true;
    }
}
