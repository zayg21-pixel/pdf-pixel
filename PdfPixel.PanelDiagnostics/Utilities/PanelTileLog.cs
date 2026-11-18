using PdfPixel.PdfPanel.Rendering;
using System.Diagnostics;

namespace PdfPixel.PanelDiagnostics.Utilities;

/// <summary>
/// Creates the tiles' resources of a panel, which keep no pixels, and logs every change of a page's tiles with the
/// number of resources alive and of tiles written since the last change.
/// </summary>
internal sealed class PanelTileLog : IPdfPanelTileResourceFactory
{
    private readonly Stopwatch _clock;
    private int _liveCount;
    private int _writeCount;

    /// <summary>
    /// Initializes the log with the clock its timestamps are read from.
    /// </summary>
    public PanelTileLog(Stopwatch clock) => _clock = clock;

    /// <inheritdoc />
    public IPdfPanelTileResource Create(int tileSize)
    {
        _liveCount++;

        return new PanelTileResource(this);
    }

    /// <summary>
    /// Logs the page of <paramref name="args"/> and the counts.
    /// </summary>
    public void OnTilesChanged(object? sender, PdfPanelTilesChangedEventArgs args)
    {
        Console.WriteLine($"{_clock.Elapsed.TotalMilliseconds,10:F1} ms  tiles page {args.PageNumber}: {_writeCount} written, {_liveCount} alive");
        _writeCount = 0;
    }

    /// <summary>
    /// Counts a tile written into one of the log's resources.
    /// </summary>
    public void OnTileWritten() => _writeCount++;

    /// <summary>
    /// Counts one of the log's resources disposed.
    /// </summary>
    public void OnTileDisposed() => _liveCount--;
}
