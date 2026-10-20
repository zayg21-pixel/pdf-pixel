using PdfPixel.Actions.Model;
using PdfPixel.Annotations.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Navigation.Model;
using PdfPixel.PdfPanel.Annotations;
using PdfPixel.PdfPanel.ContentProvider;
using PdfPixel.PdfPanel.Extensions;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Actions;

/// <summary>
/// Actions of the panel. Go-to and named actions navigate the panel, set-OCG-state actions change its layers, and
/// the actions the host performs are raised as events during <see cref="PdfPanelContext.Synchronize"/>.
/// </summary>
public sealed class PdfPanelActions
{
    private readonly PdfPanelContext _context;
    private readonly PdfPanelDocument _panelDocument;
    private readonly PdfPanelAnnotations _annotations;
    private readonly PdfPanelLayers _layers;
    private bool _openActionPerformed;
    private int? _currentPageNumber;
    private PdfDestination? _requestedDestination;
    private PdfOutlineItem? _requestedOutlineItem;
    private int? _targetPageNumber;
    private PdfPoint? _targetLocation;

    /// <summary>
    /// Initializes the actions of <paramref name="panelDocument"/> performed in <paramref name="context"/>.
    /// </summary>
    internal PdfPanelActions(PdfPanelContext context, PdfPanelDocument panelDocument, PdfPanelAnnotations annotations, PdfPanelLayers layers)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _panelDocument = panelDocument ?? throw new ArgumentNullException(nameof(panelDocument));
        _annotations = annotations ?? throw new ArgumentNullException(nameof(annotations));
        _layers = layers ?? throw new ArgumentNullException(nameof(layers));
    }

    /// <summary>
    /// Raised during <see cref="PdfPanelContext.Synchronize"/> when an action the panel does not perform is performed.
    /// </summary>
    public event EventHandler<PdfPanelActionEventArgs>? ActionRequested;

    /// <summary>
    /// Whether a performed action changed how annotations are drawn since <see cref="ClearAnnotationUpdate"/>.
    /// </summary>
    internal bool IsAnnotationUpdateRequested { get; private set; }

    /// <summary>
    /// Scrolls the panel to a destination of the panel's document, applying its zoom, on the next
    /// <see cref="PdfPanelContext.Synchronize"/> once the panel has a size; it follows the document's open action.
    /// </summary>
    /// <param name="destination">The destination to navigate to, resolved by the document's resolver.</param>
    public void ScrollToDestination(PdfDestination destination)
        => _requestedDestination = destination ?? throw new ArgumentNullException(nameof(destination));

    /// <summary>
    /// Scrolls the panel to a page on the next <see cref="PdfPanelContext.Synchronize"/>, once the pages are laid out.
    /// </summary>
    /// <param name="pageNumber">1-based number of the page to scroll to.</param>
    public void ScrollToPage(int pageNumber)
    {
        _targetPageNumber = pageNumber;
        _targetLocation = null;
    }

    /// <summary>
    /// Performs the action of an outline item of the panel's document, or navigates to its destination when it has
    /// no action, on the next <see cref="PdfPanelContext.Synchronize"/> once the panel has a size.
    /// </summary>
    /// <param name="item">The activated outline item.</param>
    public void ActivateOutlineItem(PdfOutlineItem item)
        => _requestedOutlineItem = item ?? throw new ArgumentNullException(nameof(item));

    /// <summary>
    /// Clears <see cref="IsAnnotationUpdateRequested"/>.
    /// </summary>
    internal void ClearAnnotationUpdate() => IsAnnotationUpdateRequested = false;

    /// <summary>
    /// Performs the pending actions and navigation. Navigation sets the scale and records the target, which
    /// <see cref="ApplyNavigation"/> scrolls to when the panel lays out the pages at that scale.
    /// </summary>
    internal void Synchronize()
    {
        bool isInitialized = _context.PanelWidth > 0 && _context.PanelHeight > 0;
        if (!isInitialized)
        {
            return;
        }

        SynchronizeDocument();
        SynchronizeAnnotation();
        UpdateNavigationLayout();

        SynchronizePage();
        UpdateNavigationLayout();
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
    /// Performs the document's open action once, then the requested destination and outline item.
    /// </summary>
    private void SynchronizeDocument()
    {
        if (!_openActionPerformed)
        {
            _openActionPerformed = true;
            IPdfDocument document = _panelDocument.Document;
            PerformAction(document.OpenAction?.Destination, document.OpenAction?.Action, PdfPanelActionSource.DocumentOpen, document);
        }

        if (_requestedDestination != null)
        {
            NavigateTo(_requestedDestination);
            _requestedDestination = null;
        }

        if (_requestedOutlineItem != null)
        {
            PerformAction(_requestedOutlineItem.Destination, _requestedOutlineItem.Action, PdfPanelActionSource.Outline, _requestedOutlineItem);
            _requestedOutlineItem = null;
        }
    }

    /// <summary>
    /// Performs the action of the clicked link annotation, or navigates to its destination, or performs the action of
    /// the clicked file attachment annotation.
    /// </summary>
    private void SynchronizeAnnotation()
    {
        PdfPageAnnotation? clickedAnnotation = _annotations.ClickedAnnotation?.PageAnnotation;

        switch (clickedAnnotation?.Content)
        {
            case PdfLinkAnnotation link:
            {
                PerformAction(link.Destination, link.Action, PdfPanelActionSource.LinkAnnotationClick, clickedAnnotation);
                break;
            }
            case PdfFileAttachmentAnnotation fileAttachment:
            {
                PerformAction(null, fileAttachment.Action, PdfPanelActionSource.FileAttachmentAnnotationClick, clickedAnnotation);
                break;
            }
        }
    }

    /// <summary>
    /// Performs the close action of the previous current page and the open action of the new one when the current
    /// page has changed since the last call.
    /// </summary>
    private void SynchronizePage()
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
            IPdfPage closedPage = _panelDocument.Document.Pages[_currentPageNumber.Value - 1];
            PerformAction(null, closedPage.AdditionalActions?.PageClose, PdfPanelActionSource.PageClose, closedPage);
        }

        _currentPageNumber = currentPageNumber;

        IPdfPage openedPage = _panelDocument.Document.Pages[currentPageNumber - 1];
        PerformAction(null, openedPage.AdditionalActions?.PageOpen, PdfPanelActionSource.PageOpen, openedPage);
    }

    /// <summary>
    /// Lays out the pages when a navigation target is pending, which scrolls to it.
    /// </summary>
    private void UpdateNavigationLayout()
    {
        if (_targetPageNumber != null)
        {
            _context.UpdateLayout();
        }
    }

    /// <summary>
    /// Performs an action and the actions chained after it, in order, or navigates to the destination when there is
    /// no action.
    /// </summary>
    private void PerformAction(PdfDestination? destination, PdfAction? action, PdfPanelActionSource source, object sourceObject)
    {
        if (action == null)
        {
            if (destination != null)
            {
                NavigateTo(destination);
            }

            return;
        }

        PerformAction(action, source, sourceObject, []);
    }

    /// <summary>
    /// Performs an action and the actions chained after it, in order. An action already performed in the chain is not
    /// performed again.
    /// </summary>
    private void PerformAction(PdfAction action, PdfPanelActionSource source, object sourceObject, HashSet<PdfAction> performedActions)
    {
        if (!performedActions.Add(action))
        {
            return;
        }

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
                PerformNamedAction(namedAction, source, sourceObject);
                break;
            }
            case PdfSetOcgStateAction setOcgStateAction:
            {
                _layers.Configuration?.Apply(setOcgStateAction);
                break;
            }
            case PdfHideAction hideAction:
            {
                PerformHideAction(hideAction);
                break;
            }
            default:
            {
                RaiseActionRequested(action, source, sourceObject);
                break;
            }
        }

        IReadOnlyList<PdfAction>? nextActions;
        lock (_panelDocument.DocumentLocker)
        {
            nextActions = action.GetNext();
        }

        if (nextActions == null)
        {
            return;
        }

        foreach (PdfAction nextAction in nextActions)
        {
            PerformAction(nextAction, source, sourceObject, performedActions);
        }
    }

    private void PerformHideAction(PdfHideAction action)
    {
        lock (_panelDocument.DocumentLocker)
        {
            foreach (PdfAnnotationBase annotation in action.GetAnnotations())
            {
                annotation.IsHidden = action.Hide;
            }
        }

        IsAnnotationUpdateRequested = true;
    }

    private void RaiseActionRequested(PdfAction action, PdfPanelActionSource source, object sourceObject)
        => ActionRequested?.Invoke(this, new PdfPanelActionEventArgs(action, source, sourceObject, _panelDocument.DocumentLocker));

    private void PerformNamedAction(PdfNamedAction action, PdfPanelActionSource source, object sourceObject)
    {
        if (action.Name == PdfNamedActionName.Raw)
        {
            RaiseActionRequested(action, source, sourceObject);
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
        PdfNavigationTarget? target;

        lock (_panelDocument.DocumentLocker)
        {
            target = _panelDocument.Document.Destinations.Resolve(destination);
        }

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
    /// A coordinate the destination leaves null retains its current value; Fit and FitB
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
