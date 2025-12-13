using System;
using System.IO;
using System.Text;
using System.Linq;

namespace PdfReader.Imaging.Jpg.Color
{
    /// <summary>
    /// Provides methods to update or insert ICC profiles in JPEG byte arrays.
    /// </summary>
    public class JpgIccProfileUpdater
    {
        private static readonly byte[] IccProfileHeader = Encoding.ASCII.GetBytes("ICC_PROFILE\0");

        /// <summary>
        /// Updates or inserts an ICC profile in the given JPEG byte array.
        /// Removes any existing ICC profile (APP2 ICC_PROFILE) segments and inserts the provided profile.
        /// </summary>
        /// <param name="sourceBytes">The JPEG file as a byte array.</param>
        /// <param name="iccProfileBytes">The ICC profile to insert (may be null or empty to remove profile).</param>
        /// <returns>The JPEG file with the updated ICC profile.</returns>
        /// <exception cref="ArgumentException">Thrown if the JPEG is invalid or does not start with SOI marker.</exception>
        public static ReadOnlyMemory<byte> UpdateIccProfile(ReadOnlyMemory<byte> sourceBytes, byte[] iccProfileBytes)
        {
            // JPEG marker constants
            const ushort APP2 = 0xFFE2;
            // The JPEG segment length includes the two length bytes. ICC APP2 payload has 14 bytes of header (12 for "ICC_PROFILE\0" + 2 for sequence info).
            // Therefore, the maximum ICC data per segment is: 65535 (max length) - 2 (length field itself) - 14 (ICC header) = 65519.
            const int MaxSegmentData = 65519;
            byte[] src = sourceBytes.ToArray();
            if (src.Length < 2 || src[0] != 0xFF || src[1] != 0xD8)
            {
                throw new ArgumentException("Input is not a valid JPEG (missing SOI)");
            }

            // Parse and copy, skipping all APP2 ICC_PROFILE segments
            var output = new MemoryStream(src.Length + (iccProfileBytes?.Length ?? 0));
            int pos = 0;
            // Write SOI
            output.WriteByte(src[pos++]);
            output.WriteByte(src[pos++]);

            // Insert ICC profile after SOI and before first non-APP marker (usually after APP0/APP1)
            bool iccInserted = false;
            while (pos + 4 <= src.Length)
            {
                if (src[pos] != 0xFF)
                {
                    // Not a marker, copy rest and break
                    output.Write(src, pos, src.Length - pos);
                    break;
                }
                byte marker = src[pos + 1];
                if (marker == 0xD9) // EOI
                {
                    output.Write(src, pos, src.Length - pos);
                    break;
                }
                if (marker == 0xDA) // SOS: start of scan, image data follows
                {
                    // Insert ICC before scan data if not already inserted
                    if (!iccInserted && iccProfileBytes != null && iccProfileBytes.Length > 0)
                    {
                        WriteIccProfileSegments(output, iccProfileBytes, MaxSegmentData);
                        iccInserted = true;
                    }
                    output.Write(src, pos, src.Length - pos);
                    break;
                }
                // Read segment length
                if (pos + 4 > src.Length)
                {
                    // Truncated
                    output.Write(src, pos, src.Length - pos);
                    break;
                }
                ushort segMarker = (ushort)(src[pos] << 8 | src[pos + 1]);
                int segLen = src[pos + 2] << 8 | src[pos + 3];
                if (segLen < 2 || pos + 2 + segLen > src.Length)
                {
                    // Invalid segment, copy rest
                    output.Write(src, pos, src.Length - pos);
                    break;
                }
                bool isIccProfile = false;
                if (segMarker == APP2 && segLen >= 16)
                {
                    // Compare segment header to ICC_PROFILE\0
                    var segmentHeader = new ReadOnlySpan<byte>(src, pos + 4, IccProfileHeader.Length);
                    isIccProfile = segmentHeader.SequenceEqual(IccProfileHeader);
                }
                if (!isIccProfile)
                {
                    // Copy this segment
                    output.Write(src, pos, 2 + segLen);
                }
                // Insert ICC profile after last APPn marker if not already inserted
                if (!iccInserted && (marker < 0xE0 || marker > 0xEF) && iccProfileBytes != null && iccProfileBytes.Length > 0)
                {
                    WriteIccProfileSegments(output, iccProfileBytes, MaxSegmentData);
                    iccInserted = true;
                }
                pos += 2 + segLen;
            }
            // If ICC not inserted and we reached the end, insert now (handles JPEGs with no APP markers)
            if (!iccInserted && iccProfileBytes != null && iccProfileBytes.Length > 0)
            {
                WriteIccProfileSegments(output, iccProfileBytes, MaxSegmentData);
            }
            return output.ToArray();
        }

        /// <summary>
        /// Writes ICC profile bytes as one or more APP2 segments to the output stream.
        /// </summary>
        private static void WriteIccProfileSegments(Stream output, byte[] iccProfileBytes, int maxSegmentData)
        {
            if (iccProfileBytes == null || iccProfileBytes.Length == 0)
            {
                return;
            }
            int totalSegments = (iccProfileBytes.Length + maxSegmentData - 1) / maxSegmentData;
            for (int i = 0; i < totalSegments; i++)
            {
                int offset = i * maxSegmentData;
                int count = Math.Min(maxSegmentData, iccProfileBytes.Length - offset);
                // APP2 marker
                output.WriteByte(0xFF);
                output.WriteByte(0xE2);
                // Length: 2 (length field itself) + 14 (ICC header including sequence) + count
                int segLen = 2 + 14 + count;
                output.WriteByte((byte)(segLen >> 8));
                output.WriteByte((byte)(segLen & 0xFF));
                // ICC_PROFILE\0
                output.Write(IccProfileHeader, 0, IccProfileHeader.Length);

                // Sequence number (1-based), total segments
                output.WriteByte((byte)(i + 1));
                output.WriteByte((byte)totalSegments);
                output.Write(iccProfileBytes, offset, count);
            }
        }
    }
}
