using PdfPixel.Commands;
using SkiaSharp;
using System;
using System.Linq;
using System.Threading;

namespace PdfPixel.PdfPanel.ContentProvider;

public sealed class LockedContent<T> : IDisposable
{
    private readonly ReaderWriterLockSlim _locker;

    public LockedContent(T content, ReaderWriterLockSlim locker)
    {
        _locker = locker ?? throw new ArgumentNullException(nameof(locker));
        _locker.EnterUpgradeableReadLock();
        Content = content;
    }

    public T Content { get; }

    public bool HasContent => Content != null;

    public void Dispose()
    {
        _locker.ExitUpgradeableReadLock();
    }
}

public sealed class ContentLocker<T> : IDisposable
{
    private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();
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

/// <summary>
/// Represents page cache content item. Main content or annotation content.
/// </summary>
public class PdfPageCacheEntryItem : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Represents page content as set of commands.
    /// </summary>
    public ContentLocker<PdfCommandRecorder> ContentCommandRecording { get; } = new ContentLocker<PdfCommandRecorder>();

    /// <summary>
    /// Cached full-content picture.
    /// </summary>
    public ContentLocker<SKPicture> ContentPicture { get; } = new ContentLocker<SKPicture>();

    /// <summary>
    /// True if content depends on scale and requires generation if scale is updated.
    /// </summary>
    public bool IsScaleDependant { get; private set; }

    /// <summary>
    /// Scale used to generate the cached content.
    /// </summary>
    public float Scale { get; private set; } = 1;

    /// <summary>
    /// Replace page content with a new command recording. Disposes the previous recording if present.
    /// </summary>
    /// <param name="commandRecording">New command recording. May be null.</param>
    public void UpdateContentCommandRecording(PdfCommandRecorder commandRecording)
    {
        ThrowIfDisposed();
        ContentCommandRecording.SetContent(commandRecording);

        if (commandRecording != null)
        {
            IsScaleDependant = commandRecording.Commands.Count > 0 && commandRecording.Commands.Any(x => x.IsScaleDependant);
        }
        else
        {
            IsScaleDependant = false;
        }
    }

    /// <summary>
    /// Replace the content picture. Disposes the previous picture if present.
    /// </summary>
    public void UpdateContentPicture(SKPicture picture, float scale)
    {
        ThrowIfDisposed();
        ContentPicture.SetContent(picture);
        Scale = scale;
    }

    /// <summary>
    /// Clears cache entry.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();

        ContentCommandRecording.SetContent(default);
        ContentPicture.SetContent(default);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PdfPageCacheEntryItem));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Clear();
        _disposed = true;
    }
}

/// <summary>
/// Lightweight container for cached page rendering artifacts.
/// </summary>
public sealed class PdfPageCacheEntry : IDisposable
{
    private bool _disposed;

    public PdfPageCacheEntry(int pageNumber, PdfPanelPageInfo pageInfo)
    {
        if (pageNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        PageNumber = pageNumber;
        PageInfo = pageInfo;
        Content = new PdfPageCacheEntryItem();
        AnnotationContent = new PdfPageCacheEntryItem();
    }

    /// <summary>
    /// 1-based page number this cache entry represents.
    /// </summary>
    public int PageNumber { get; }

    /// <summary>
    /// General information about the page.
    /// </summary>
    public PdfPanelPageInfo PageInfo { get;}

    /// <summary>
    /// Main page content.
    /// </summary>
    public PdfPageCacheEntryItem Content { get; }

    /// <summary>
    /// Page annotation content.
    /// </summary>
    public PdfPageCacheEntryItem AnnotationContent { get; }

    /// <summary>
    /// Clears cache entry.
    /// </summary>
    public void Clear()
    {
        ThrowIfDisposed();

        Content.Clear();
        AnnotationContent.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(PdfPageCacheEntry));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Clear();
        _disposed = true;
    }
}
