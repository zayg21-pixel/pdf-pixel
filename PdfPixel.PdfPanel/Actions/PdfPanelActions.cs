using PdfPixel.Actions.Model;
using PdfPixel.Annotations.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Navigation.Model;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.Extensions;
using System;

namespace PdfPixel.PdfPanel.Actions;

/// <summary>
/// Actions of the panel: the document's open action, the open and close actions of the current page and the
/// actions of activated links. Go-to and named actions navigate the panel, set-OCG-state actions change its
/// layers, and the actions the host performs are raised as events during <see cref="PdfPanelContext.Synchronize"/>.
/// </summary>
public sealed class PdfPanelActions
{
    private readonly PdfPanelContext _context;
    private readonly IPdfDocument _document;
    private readonly PdfPanelAnnotations _annotations;
    private readonly PdfPanelLayers _layers;
    private bool _openActionPerformed;
    private int? _currentPageNumber;
    private PdfDestination? _requestedDestination;
    private int? _targetPageNumber;
    private PdfPoint? _targetLocation;

    /// <summary>
    /// Initializes the actions of <paramref name="document"/> performed in <paramref name="context"/>.
    /// </summary>
    internal PdfPanelActions(PdfPanelContext context, IPdfDocument document, PdfPanelAnnotations annotations, PdfPanelLayers layers)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _annotations = annotations ?? throw new ArgumentNullException(nameof(annotations));
        _layers = layers ?? throw new ArgumentNullException(nameof(layers));
    }

    /// <summary>
    /// Raised during <see cref="PdfPanelContext.Synchronize"/> when a URI action is performed: by an activated
    /// link, a chained action, a page's open or close action or the document's open action.
    /// </summary>
    public event EventHandler<PdfPanelActionEventArgs<PdfUriAction>>? UriRequested;

    /// <summary>
    /// Raised during <see cref="PdfPanelContext.Synchronize"/> when a remote go-to action is performed: by an
    /// activated link, a chained action, a page's open or close action or the document's open action.
    /// </summary>
    public event EventHandler<PdfPanelActionEventArgs<PdfGoToRemoteAction>>? RemoteDocumentRequested;

    /// <summary>
    /// Raised during <see cref="PdfPanelContext.Synchronize"/> when a named action not defined by ISO 32000, such
    /// as a viewer's <c>Print</c>, is performed: by an activated link, a chained action, a page's open or close
    /// action or the document's open action.
    /// </summary>
    public event EventHandler<PdfPanelActionEventArgs<PdfNamedAction>>? NamedActionRequested;

    /// <summary>
    /// Scrolls the panel to a destination of the panel's document, applying its zoom, on the next
    /// <see cref="PdfPanelContext.Synchronize"/> once the panel has a size; it follows the document's open action.
    /// </summary>
    /// <param name="destination">The destination to navigate to, resolved by the document's resolver.</param>
    public void ScrollToDestination(PdfDestination destination)
        => _requestedDestination = destination ?? throw new ArgumentNullException(nameof(destination));

    /// <summary>
    /// Performs the document's open action the first time the panel has a size, then a requested destination and
    /// the action or destination of the link activated by the pointer, then the close and open actions of the
    /// pages when the current page has changed. Navigation sets the scale and records the target, which
    /// <see cref="ApplyNavigation"/> scrolls to when the panel lays out the pages at that scale.
    /// </summary>
    internal void Synchronize()
    {
        bool hasSize = _context.PanelWidth > 0 && _context.PanelHeight > 0;

        if (!_openActionPerformed && hasSize)
        {
            _openActionPerformed = true;
            PdfOpenAction? openAction = _document.OpenAction;

            if (openAction?.Destination != null)
            {
                NavigateTo(openAction.Destination);
            }
            else if (openAction?.Action != null)
            {
                PerformAction(openAction.Action, PdfPanelActionSource.DocumentOpen);
            }
        }

        if (_requestedDestination != null && hasSize)
        {
            NavigateTo(_requestedDestination);
            _requestedDestination = null;
        }

        if (_annotations.ClickedAnnotation?.PageAnnotation?.Content is PdfLinkAnnotation link)
        {
            if (link.Action != null)
            {
                PerformAction(link.Action, PdfPanelActionSource.Annotation);
            }
            else if (link.Destination != null)
            {
                NavigateTo(link.Destination);
            }
        }

        if (_targetPageNumber != null)
        {
            _context.UpdateLayout();
        }

        if (!hasSize)
        {
            return;
        }

        SynchronizeCurrentPage();

        if (_targetPageNumber != null)
        {
            _context.UpdateLayout();
        }
    }

    /// <summary>
    /// Scrolls to the recorded navigation target; the pages must be laid out at the current scale.
    /// </summary>
    internal void ApplyNavigation()
    {
        if (_targetPageNumber == null)
        {
            return;
        }

        int pageNumber = _targetPageNumber.Value;
        PdfPoint? location = _targetLocation;
        _targetPageNumber = null;
        _targetLocation = null;

        if (!_context.Pages.TryGetPage(pageNumber, out PdfPanelPage? page) || page == null)
        {
            return;
        }

        if (location == null)
        {
            _context.ScrollToPage(pageNumber);
            return;
        }

        PdfPoint pageLocation = page.FromPdfPoint(location.Value);
        PdfMatrix pageToExtent = page.PanelToPageMatrix(_context.Scale, 0, 0).Invert();
        PdfPoint extentLocation = pageToExtent.MapPoint(pageLocation);

        _context.HorizontalOffset = extentLocation.X;
        _context.VerticalOffset = extentLocation.Y;
    }

    /// <summary>
    /// Performs the close action of the previous current page and the open action of the new one when the current
    /// page has changed since the last call.
    /// </summary>
    private void SynchronizeCurrentPage()
    {
        if (_context.Pages.Count == 0)
        {
            return;
        }

        int currentPageNumber = _context.GetCurrentPage();
        if (currentPageNumber == _currentPageNumber)
        {
            return;
        }

        if (_currentPageNumber != null)
        {
            PdfAction? pageClose = _document.Pages[_currentPageNumber.Value - 1].AdditionalActions?.PageClose;
            if (pageClose != null)
            {
                PerformAction(pageClose, PdfPanelActionSource.PageClose);
            }
        }

        _currentPageNumber = currentPageNumber;

        PdfAction? pageOpen = _document.Pages[currentPageNumber - 1].AdditionalActions?.PageOpen;
        if (pageOpen != null)
        {
            PerformAction(pageOpen, PdfPanelActionSource.PageOpen);
        }
    }

    /// <summary>
    /// Performs an action and the actions chained after it (/Next), in order.
    /// </summary>
    private void PerformAction(PdfAction action, PdfPanelActionSource source)
    {
        switch (action)
        {
            case PdfGoToAction goToAction:
            {
                if (goToAction.Destination != null)
                {
                    NavigateTo(goToAction.Destination);
                }

                break;
            }
            case PdfNamedAction namedAction:
            {
                PerformNamedAction(namedAction, source);
                break;
            }
            case PdfSetOcgStateAction setOcgStateAction:
            {
                _layers.Configuration?.Apply(setOcgStateAction);
                break;
            }
            case PdfUriAction uriAction:
            {
                UriRequested?.Invoke(this, new PdfPanelActionEventArgs<PdfUriAction>(uriAction, source));
                break;
            }
            case PdfGoToRemoteAction goToRemoteAction:
            {
                RemoteDocumentRequested?.Invoke(this, new PdfPanelActionEventArgs<PdfGoToRemoteAction>(goToRemoteAction, source));
                break;
            }
        }

        if (action.Next == null)
        {
            return;
        }

        foreach (PdfAction nextAction in action.Next)
        {
            PerformAction(nextAction, source);
        }
    }

    private void PerformNamedAction(PdfNamedAction action, PdfPanelActionSource source)
    {
        if (action.Name == PdfNamedActionName.Raw)
        {
            NamedActionRequested?.Invoke(this, new PdfPanelActionEventArgs<PdfNamedAction>(action, source));
            return;
        }

        int pageCount = _context.Pages.Count;
        if (pageCount == 0)
        {
            return;
        }

        int? pageNumber = action.Name switch
        {
            PdfNamedActionName.NextPage => Math.Min(_context.GetCurrentPage() + 1, pageCount),
            PdfNamedActionName.PrevPage => Math.Max(_context.GetCurrentPage() - 1, 1),
            PdfNamedActionName.FirstPage => 1,
            PdfNamedActionName.LastPage => pageCount,
            _ => null
        };

        if (pageNumber != null)
        {
            _targetPageNumber = pageNumber;
            _targetLocation = null;
        }
    }

    /// <summary>
    /// Resolves a destination of the document, applies its zoom and records the page and point to scroll to.
    /// </summary>
    private void NavigateTo(PdfDestination destination)
    {
        PdfNavigationTarget? target = _document.Destinations.Resolve(destination);
        if (target == null)
        {
            return;
        }

        if (!_context.Pages.TryGetPage(target.Value.PageIndex + 1, out PdfPanelPage? targetPage) || targetPage == null)
        {
            return;
        }

        PdfPoint? currentLocation = GetCurrentLocation();
        float? fitZoom = ComputeFitZoom(targetPage, target.Value);

        if (fitZoom > 0)
        {
            _context.Scale = fitZoom.Value;
        }
        else if (target.Value.Zoom > 0)
        {
            _context.Scale = target.Value.Zoom.Value;
        }

        _targetPageNumber = targetPage.PageNumber;
        _targetLocation = GetDestinationLocation(target.Value, targetPage.Info.CropBox, currentLocation);
    }

    private float? ComputeFitZoom(PdfPanelPage page, in PdfNavigationTarget target)
    {
        PdfSize pageSize = page.GetRotatedSize();
        float panelWidth = _context.PanelWidth;
        float panelHeight = _context.PanelHeight;

        return target.FitType switch
        {
            PdfDestinationFitType.Fit or PdfDestinationFitType.FitB =>
                Math.Min(panelWidth / pageSize.Width, panelHeight / pageSize.Height),
            PdfDestinationFitType.FitH or PdfDestinationFitType.FitBH =>
                panelWidth / pageSize.Width,
            PdfDestinationFitType.FitV or PdfDestinationFitType.FitBV =>
                panelHeight / pageSize.Height,
            PdfDestinationFitType.FitR when target.Left.HasValue
                && target.Bottom.HasValue
                && target.Right.HasValue
                && target.Top.HasValue
                && target.Left.Value != target.Right.Value
                && target.Bottom.Value != target.Top.Value =>
                Math.Min(panelWidth / Math.Abs(target.Right.Value - target.Left.Value), panelHeight / Math.Abs(target.Top.Value - target.Bottom.Value)),
            _ => null
        };
    }

    /// <summary>
    /// Point of the target page, in PDF coordinates, that the destination scrolls to the panel's top-left corner.
    /// A coordinate the destination leaves null retains its current value (ISO 32000-2 Table 149); Fit and FitB
    /// scroll to the page itself.
    /// </summary>
    /// <param name="target">The resolved destination.</param>
    /// <param name="cropBox">Crop box of the target page.</param>
    /// <param name="currentLocation">Point at the panel's top-left corner before navigating, in PDF coordinates
    /// of the page shown there, or null when no page is shown.</param>
    private static PdfPoint? GetDestinationLocation(in PdfNavigationTarget target, in PdfRectangle cropBox, PdfPoint? currentLocation)
    {
        float pageLeft = cropBox.Left;
        float pageTop = cropBox.Top + cropBox.Height;
        float currentLeft = currentLocation?.X ?? pageLeft;
        float currentTop = currentLocation?.Y ?? pageTop;

        switch (target.FitType)
        {
            case PdfDestinationFitType.XYZ:
                return new PdfPoint(target.Left ?? currentLeft, target.Top ?? currentTop);

            case PdfDestinationFitType.FitH:
            case PdfDestinationFitType.FitBH:
                return new PdfPoint(pageLeft, target.Top ?? currentTop);

            case PdfDestinationFitType.FitV:
            case PdfDestinationFitType.FitBV:
                return new PdfPoint(target.Left ?? currentLeft, pageTop);

            case PdfDestinationFitType.FitR:
            {
                if (target.Left.HasValue && target.Bottom.HasValue && target.Right.HasValue && target.Top.HasValue)
                {
                    return new PdfPoint(Math.Min(target.Left.Value, target.Right.Value), Math.Max(target.Bottom.Value, target.Top.Value));
                }

                return null;
            }
            default:
                return null;
        }
    }

    /// <summary>
    /// Point at the panel's top-left corner, in PDF coordinates of the page shown there, or null when the panel
    /// shows no page.
    /// </summary>
    private PdfPoint? GetCurrentLocation()
    {
        if (!_context.Pages.TryGetPage(_context.GetCurrentPage(), out PdfPanelPage? currentPage) || currentPage == null)
        {
            return null;
        }

        PdfRectangle cropBox = currentPage.Info.CropBox;
        PdfPoint pageLocation = currentPage.PanelToPageMatrix(_context).MapPoint(new PdfPoint(0, 0));

        return new PdfPoint(pageLocation.X + cropBox.Left, cropBox.Height + cropBox.Top - pageLocation.Y);
    }
}
