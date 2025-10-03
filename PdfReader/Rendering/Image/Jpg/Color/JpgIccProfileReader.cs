using PdfReader.Rendering.Image.Jpg.Model;
using System;

namespace PdfReader.Rendering.Image.Jpg.Color
{
    internal class JpgIccProfileReader
    {
        /// <summary>
        /// Reassembles ICC profile bytes from APP2 ICC_PROFILE segments gathered in the <see cref="JpgHeader"/>.
        /// Returns null if segments are missing, inconsistent, or invalid.
        /// </summary>
        /// <param name="header">Parsed JPEG header.</param>
        /// <returns>Complete ICC profile byte array or null if invalid/incomplete.</returns>
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

            // Determine expected total segment count and validate consistency.
            int declaredTotal = header.IccProfileSegments[0].TotalSegments;
            if (declaredTotal <= 0 || declaredTotal > 255)
            {
                profileBytes = null;
                return false;
            }

            // Track seen segments and compute aggregate length.
            var segmentData = new byte[declaredTotal][];
            int totalLength = 0;
            for (int i = 0; i < header.IccProfileSegments.Count; i++)
            {
                IccSegmentInfo segment = header.IccProfileSegments[i];
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

                int seq = segment.SequenceNumber; // JPEG ICC segments are 1-based.
                if (seq <= 0 || seq > declaredTotal)
                {
                    profileBytes = null;
                    return false;
                }

                if (segmentData[seq - 1] != null)
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

                segmentData[seq - 1] = data;
                totalLength += data.Length;
            }

            // Ensure all segments are present.
            for (int i = 0; i < declaredTotal; i++)
            {
                if (segmentData[i] == null)
                {
                    profileBytes = null;
                    return false;
                }
            }

            // Reassemble into single contiguous byte array.
            try
            {
                byte[] profile = new byte[totalLength];
                int offset = 0;
                for (int i = 0; i < segmentData.Length; i++)
                {
                    byte[] part = segmentData[i];
                    Buffer.BlockCopy(part, 0, profile, offset, part.Length);
                    offset += part.Length;
                }

                profileBytes = profile;
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("JpgIccProfileReader.TryAssembleIccProfile: reassembly failed: " + ex.Message);
                profileBytes = null;
                return false;
            }
        }
    }
}
