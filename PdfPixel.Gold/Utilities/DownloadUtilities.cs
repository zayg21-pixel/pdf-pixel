namespace PdfPixel.Gold.Utilities;

/// <summary>
/// Downloads corpus files over HTTP.
/// </summary>
internal static class DownloadUtilities
{
    /// <summary>
    /// Client every download goes through, identifying itself as PdfPixel.Gold.
    /// </summary>
    public static HttpClient Client { get; } = CreateClient();

    private static HttpClient CreateClient()
    {
        HttpClient client = new();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PdfPixel.Gold");

        return client;
    }
}
