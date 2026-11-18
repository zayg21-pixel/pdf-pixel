using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Thumbnails;

/// <summary>
/// The thumbnails a host requests from <see cref="PdfPanelThumbnails"/>: given the pages it wants, it requests the ones
/// it newly wants and cancels the ones it no longer wants.
/// </summary>
public sealed class PdfPanelThumbnailRequests
{
    private readonly HashSet<int> _requestedPageNumbers = [];

    /// <summary>
    /// Makes <paramref name="wantedPageNumbers"/> the requested thumbnails of <paramref name="thumbnails"/>.
    /// </summary>
    /// <param name="thumbnails">The thumbnails the requests are made of.</param>
    /// <param name="wantedPageNumbers">The 1-based numbers of the pages whose thumbnails are wanted.</param>
    public void Update(PdfPanelThumbnails thumbnails, IEnumerable<int> wantedPageNumbers)
    {
        if (thumbnails == null)
        {
            throw new ArgumentNullException(nameof(thumbnails));
        }

        if (wantedPageNumbers == null)
        {
            throw new ArgumentNullException(nameof(wantedPageNumbers));
        }

        HashSet<int> wanted = [.. wantedPageNumbers];

        foreach (int pageNumber in _requestedPageNumbers)
        {
            if (!wanted.Contains(pageNumber))
            {
                thumbnails.Cancel(pageNumber);
            }
        }

        foreach (int pageNumber in wanted)
        {
            if (!_requestedPageNumbers.Contains(pageNumber))
            {
                thumbnails.Request(pageNumber);
            }
        }

        _requestedPageNumbers.Clear();
        _requestedPageNumbers.UnionWith(wanted);
    }

    /// <summary>
    /// Forgets the requests, made of thumbnails that were disposed with their context.
    /// </summary>
    public void Clear() => _requestedPageNumbers.Clear();
}
