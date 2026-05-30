using PdfPixel.Commands;
using SkiaSharp;
using System;
using System.Linq;

namespace PdfPixel.PdfPanel.ContentProvider;

/// <summary>
/// Represents page cache content item. Main content or annotation content.
/// </summary>
public sealed class PdfPageCacheEntryItem : IDisposable
{
    private bool _disposed;

    /// <summary>
    /// Represents page content as set of commands.
    /// </summary>
    public ContentLocker<PdfCommandRecorder> ContentCommandRecording { get; } = new();

    /// <summary>
    /// Cached full-content picture.
    /// </summary>
    public ContentLocker<SKPicture> ContentPicture { get; } = new();

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
    public void UpdateContentCommandRecording(PdfCommandRecorder? commandRecording)
    {
        ThrowIfDisposed();
        ContentCommandRecording.SetContent(commandRecording);

        if (commandRecording != null)
        {
            IsScaleDependant = commandRecording.Commands.Count > 0 && commandRecording.Commands.Any(x => x.IsScaleDependent);
        }
        else
        {
            IsScaleDependant = false;
        }
    }

    /// <summary>
    /// Replace the content picture. Disposes the previous picture if present.
    /// </summary>
    public void UpdateContentPicture(SKPicture? picture, float scale)
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
