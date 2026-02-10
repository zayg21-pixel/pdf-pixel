using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PdfPixel.Streams
{
    /// <summary>
    /// Provides a read-only window over a seekable underlying <see cref="Stream"/>.
    /// The wrapper restricts all read and seek operations to a fixed subrange defined by
    /// an absolute starting offset and a length. Attempts to access outside the range fail.
    /// </summary>
    public sealed class SubrangeReadOnlyStream : Stream
    {
        private readonly Stream _innerStream;
        private readonly long _subrangeOffset;
        private readonly long _subrangeLength;
        private readonly bool _leaveOpen;
        private long _position; // Position relative to the subrange start.
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubrangeReadOnlyStream"/> class.
        /// </summary>
        /// <param name="innerStream">Seekable source stream.</param>
        /// <param name="offset">Absolute start offset of the subrange within the source stream.</param>
        /// <param name="length">Length of the subrange in bytes.</param>
        /// <param name="leaveOpen">If true, does not dispose the inner stream when this wrapper is disposed.</param>
        public SubrangeReadOnlyStream(Stream innerStream, long offset, long length, bool leaveOpen = true)
        {
            if (innerStream == null)
            {
                throw new ArgumentNullException(nameof(innerStream));
            }
            if (!innerStream.CanSeek)
            {
                throw new ArgumentException("Inner stream must be seekable.", nameof(innerStream));
            }
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            if (innerStream.Length - offset < length)
            {
                throw new ArgumentException("Specified offset and length exceed inner stream bounds.");
            }

            _innerStream = innerStream;
            _subrangeOffset = offset;
            _subrangeLength = length;
            _leaveOpen = leaveOpen;
            _position = 0;
        }

        /// <summary>
        /// Gets the total length of the subrange window.
        /// </summary>
        public override long Length
        {
            get { return _subrangeLength; }
        }

        /// <summary>
        /// Gets or sets the current position within the subrange (0..Length).
        /// </summary>
        public override long Position
        {
            get { return _position; }
            set { Seek(value, SeekOrigin.Begin); }
        }

        /// <summary>
        /// Indicates whether the stream supports reading.
        /// </summary>
        public override bool CanRead
        {
            get { return !_disposed && _innerStream.CanRead; }
        }

        /// <summary>
        /// Indicates whether the stream supports seeking.
        /// </summary>
        public override bool CanSeek
        {
            get { return !_disposed && _innerStream.CanSeek; }
        }

        /// <summary>
        /// Indicates whether the stream supports writing (always false).
        /// </summary>
        public override bool CanWrite
        {
            get { return false; }
        }

        /// <summary>
        /// Not supported; this is a read-only stream.
        /// </summary>
        public override void Flush()
        {
            // No-op: read-only.
        }

        /// <summary>
        /// Reads up to <paramref name="count"/> bytes from the subrange starting at the current position.
        /// </summary>
        /// <param name="buffer">Destination buffer.</param>
        /// <param name="offset">Offset in destination buffer.</param>
        /// <param name="count">Maximum number of bytes to read.</param>
        /// <returns>Number of bytes actually read;0 indicates end of subrange.</returns>
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SubrangeReadOnlyStream));
            }
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException("Invalid buffer offset/count.");
            }
            if (count == 0)
            {
                return 0;
            }
            if (_position >= _subrangeLength)
            {
                return 0; // EOF of subrange.
            }

            long remaining = _subrangeLength - _position;
            if (count > remaining)
            {
                count = (int)remaining;
            }

            // Align inner stream position with absolute offset.
            long absolute = _subrangeOffset + _position;
            if (_innerStream.Position != absolute)
            {
                _innerStream.Position = absolute;
            }

            int read = _innerStream.Read(buffer, offset, count);
            _position += read;
            return read;
        }

        /// <summary>
        /// Asynchronously reads bytes from the subrange.
        /// </summary>
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SubrangeReadOnlyStream));
            }
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new ArgumentOutOfRangeException("Invalid buffer offset/count.");
            }
            if (count == 0 || _position >= _subrangeLength)
            {
                return Task.FromResult(0);
            }

            long remaining = _subrangeLength - _position;
            if (count > remaining)
            {
                count = (int)remaining;
            }

            long absolute = _subrangeOffset + _position;
            if (_innerStream.Position != absolute)
            {
                _innerStream.Position = absolute;
            }

            return InternalReadAsync(buffer, offset, count, cancellationToken);
        }

        private async Task<int> InternalReadAsync(byte[] buffer, int offset, int count, CancellationToken ct)
        {
            int read = await _innerStream.ReadAsync(buffer, offset, count, ct).ConfigureAwait(false);
            _position += read;
            return read;
        }

        /// <summary>
        /// Sets the position within the subrange according to the specified origin.
        /// </summary>
        /// <param name="offset">Offset relative to origin.</param>
        /// <param name="origin">Reference origin.</param>
        /// <returns>New position within the subrange.</returns>
        public override long Seek(long offset, SeekOrigin origin)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SubrangeReadOnlyStream));
            }

            long newPos;
            switch (origin)
            {
                case SeekOrigin.Begin:
                {
                    newPos = offset;
                    break;
                }
                case SeekOrigin.Current:
                {
                    newPos = _position + offset;
                    break;
                }
                case SeekOrigin.End:
                {
                    newPos = _subrangeLength + offset;
                    break;
                }
                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(origin));
                }
            }

            if (newPos < 0)
            {
                throw new IOException("Cannot seek before subrange start.");
            }
            if (newPos > _subrangeLength)
            {
                throw new IOException("Cannot seek beyond subrange end.");
            }

            _position = newPos;
            return _position;
        }

        /// <summary>
        /// Setting length is not supported for a fixed read-only subrange.
        /// </summary>
        public override void SetLength(long value)
        {
            throw new NotSupportedException("SubrangeReadOnlyStream is fixed-length and read-only.");
        }

        /// <summary>
        /// Writing is not supported.
        /// </summary>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException("SubrangeReadOnlyStream is read-only.");
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                if (!_leaveOpen)
                {
                    _innerStream.Dispose();
                }
                _disposed = true;
            }
            base.Dispose(disposing);
        }
    }
}
