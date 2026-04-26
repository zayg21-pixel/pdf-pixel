using System;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Provides access to content of type T with thread safety.
/// The content can be updated, and when accessed, it is locked to prevent concurrent modifications.
/// </summary>
/// <typeparam name="T">Content type.</typeparam>
public sealed class ContentLocker<T> : IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
    private T _content;
    private bool _disposed;

    /// <summary>
    /// Indicates whether the content is currently set.
    /// </summary>
    public bool HasContent => _content != null;

    /// <summary>
    /// Sets the content. If there is existing content that implements IDisposable, it will be disposed before setting the new content.
    /// Must not be called while the content is locked for reading, as it will acquire a write lock internally.
    /// </summary>
    /// <param name="content">The new content to set.</param>
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

    /// <summary>
    /// Gets the content wrapped in a <see cref="LockedContent{T}"/> which holds a read lock on the content.
    /// The caller must dispose the <see cref="LockedContent{T}"/> to release the lock.
    /// </summary>
    /// <returns>The locked content.</returns>
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

    /// <inheritdoc />
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
