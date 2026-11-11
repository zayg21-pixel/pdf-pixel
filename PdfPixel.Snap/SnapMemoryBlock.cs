using System;

namespace PdfPixel.Snap;

/// <summary>
/// A block rented from a <see cref="PdfSnapMemoryPool"/>, returned to it when disposed.
/// </summary>
internal sealed class SnapMemoryBlock : IDisposable
{
    private readonly PdfSnapMemoryPool _pool;
    private byte[]? _array;

    public SnapMemoryBlock(PdfSnapMemoryPool pool, byte[] array)
    {
        _pool = pool ?? throw new ArgumentNullException(nameof(pool));
        _array = array ?? throw new ArgumentNullException(nameof(array));
    }

    /// <summary>
    /// Gets the rented bytes, at least as many as were asked for; undefined until written.
    /// </summary>
    public byte[] Array => _array ?? throw new ObjectDisposedException(nameof(SnapMemoryBlock));

    /// <inheritdoc />
    public void Dispose()
    {
        if (_array != null)
        {
            _pool.Return(_array);
            _array = null;
        }
    }
}
