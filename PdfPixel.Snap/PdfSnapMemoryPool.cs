using System;
using System.Buffers;

namespace PdfPixel.Snap;

/// <summary>
/// The memory Snap surfaces draw their layers in, kept for reuse; surfaces that never draw at the same time can share
/// one pool.
/// </summary>
public sealed class PdfSnapMemoryPool
{
    // The longest block kept for reuse; longer ones are allocated and dropped.
    private const int MaximumBlockLength = 1 << 30;

    // Blocks kept per power-of-two size.
    private const int MaximumBlocksPerSize = 8;

    private readonly ArrayPool<byte> _pool = ArrayPool<byte>.Create(MaximumBlockLength, MaximumBlocksPerSize);

    /// <summary>
    /// Returns a block of at least <paramref name="length"/> bytes, returned to the pool when disposed; its bytes are
    /// undefined.
    /// </summary>
    internal SnapMemoryBlock Rent(int length) => new(this, _pool.Rent(length));

    /// <summary>
    /// Takes back <paramref name="block"/>, the bytes of a disposed <see cref="SnapMemoryBlock"/>.
    /// </summary>
    internal void Return(byte[] block) => _pool.Return(block ?? throw new ArgumentNullException(nameof(block)));
}
