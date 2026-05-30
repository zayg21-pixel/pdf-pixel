using System;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Holds a read-locked reference to content protected by a <see cref="ReaderWriterLockSlim"/>.
/// Dispose to release the read lock.
/// </summary>
public sealed class LockedContent<T> : IDisposable
{
    private readonly ReaderWriterLockSlim _locker;

    /// <summary>
    /// Acquires a read lock on <paramref name="locker"/> and stores <paramref name="content"/>.
    /// </summary>
    public LockedContent(T? content, ReaderWriterLockSlim locker)
    {
        _locker = locker ?? throw new ArgumentNullException(nameof(locker));
        _locker.EnterReadLock();
        Content = content;
    }

    /// <summary>
    /// The locked content value.
    /// </summary>
    public T? Content { get; }

    /// <summary>
    /// Releases the read lock.
    /// </summary>
    public void Dispose() => _locker.ExitReadLock();
}
