using System;
using System.Threading;

namespace PdfPixel.PdfPanel.Caching;

/// <summary>
/// Holds a write-locked reference to content protected by a <see cref="ReaderWriterLockSlim"/>, for changing the
/// content in place. Dispose to release the write lock.
/// </summary>
internal sealed class WriteLockedContent<T> : IDisposable
{
    private readonly ReaderWriterLockSlim _locker;

    /// <summary>
    /// Stores <paramref name="content"/> under the write lock already held on <paramref name="locker"/>.
    /// </summary>
    public WriteLockedContent(T? content, ReaderWriterLockSlim locker)
    {
        _locker = locker ?? throw new ArgumentNullException(nameof(locker));
        Content = content;
    }

    /// <summary>
    /// The locked content value.
    /// </summary>
    public T? Content { get; }

    /// <summary>
    /// Releases the write lock.
    /// </summary>
    public void Dispose() => _locker.ExitWriteLock();
}
