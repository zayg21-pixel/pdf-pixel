namespace PdfPixel.PdfPanel.Web.Rendering;

/// <summary>
/// Hands out the ids of the canvases in the JS canvas store, unique across every panel.
/// </summary>
internal static class WebCanvasIds
{
    private static int _lastId;

    /// <summary>
    /// Returns an id no canvas had before.
    /// </summary>
    public static int Next() => ++_lastId;
}
