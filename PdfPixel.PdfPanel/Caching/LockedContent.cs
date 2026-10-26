using System;
using System.Threading;

namespace PdfPixel.PdfPanel.Caching;

/// <summary>
/// Holds a read-locked reference to content protected by a <see cref="ReaderWriterLockSlim"/>.
/// Dispose to release the read lock.
/// </summary>
internal sealed class LockedContent<T> : IDisposable
{
    private readonly ReaderWriterLockSlim _locker;

    /// <summary>
    /// Stores <paramref name="content"/> read under a read lock already held on <paramref name="locker"/>.
    /// </summary>
    public LockedContent(T? content, ReaderWriterLockSlim locker)
    {
        _locker = locker ?? throw new ArgumentNullException(nameof(locker));
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
