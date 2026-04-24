using System;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;

public sealed class LockedContent<T> : IDisposable
{
    private readonly ReaderWriterLockSlim _locker;

    public LockedContent(T content, ReaderWriterLockSlim locker)
    {
        _locker = locker ?? throw new ArgumentNullException(nameof(locker));
        _locker.EnterReadLock();
        Content = content;
    }

    public T Content { get; }

    public bool HasContent => Content != null;

    public void Dispose()
    {
        _locker.ExitReadLock();
    }
}
