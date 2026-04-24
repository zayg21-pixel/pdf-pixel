using System;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;

public sealed class ContentLocker<T> : IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
    private T _content;
    private bool _disposed;

    public void SetContent(T content)
    {
        _lock.EnterWriteLock();
        try
        {
            CheckIfDisposed();

            if (_content is IDisposable disposable)
            {
                disposable.Dispose();
            }

            _content = content;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool HasContent => _content != null;

    public LockedContent<T> GetContent()
    {
        CheckIfDisposed();
        return new LockedContent<T>(_content, _lock);
    }

    private void CheckIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(ContentLocker<T>));
        }
    }

    public void Dispose()
    {
        if (_content is IDisposable disposable)
        {
            disposable.Dispose();
        }
        _disposed = true;

        _lock.Dispose();
    }
}
