using System;
using System.IO;

namespace PdfReader.Streams
{
    /// <summary>
    /// Base stream that routes array-based Read() into a Span-based abstract Read for lower overhead.
    /// Derived classes implement Read(Span&lt;byte&gt;) and can ignore the array overload.
    /// </summary>
    internal abstract class ContentStream : Stream
    {
        /// <summary>
        /// Read data into the provided destination span. Returns the number of bytes written, or 0 on EOF.
        /// </summary>
        public abstract int Read(Span<byte> buffer);

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException("Invalid offset/count.");
            }

            if (count == 0)
            {
                return 0;
            }

            return Read(buffer.AsSpan(offset, count));
        }
    }
}
