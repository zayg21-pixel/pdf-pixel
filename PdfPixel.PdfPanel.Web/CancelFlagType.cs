namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Index of a per-page cancel flag within the SharedArrayBuffer cancel flags array.
/// Each page occupies 2 consecutive bytes: flags[pageIndex * 2 + flagType].
/// </summary>
internal enum CancelFlagType
{
    Parse = 0,
    Content = 1,
}
