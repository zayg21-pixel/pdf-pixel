namespace PdfPixel.Navigation.Model;

/// <summary>
/// What a viewer navigates to: a page of the document and how it is displayed, resolved from a
/// <see cref="PdfDestination"/>. Coordinates are in the page's default user space, null when not given.
/// </summary>
public readonly struct PdfNavigationTarget
{
    internal PdfNavigationTarget(int pageIndex, PdfExplicitDestination destination)
    {
        PageIndex = pageIndex;
        FitType = destination.FitType;
        Zoom = destination.Zoom;
        Left = destination.Left;
        Bottom = destination.Bottom;
        Right = destination.Right;
        Top = destination.Top;
    }

    /// <summary>
    /// Index of the target page in the document's pages, the first page being 0.
    /// </summary>
    public int PageIndex { get; }

    /// <summary>
    /// How the target page is displayed.
    /// </summary>
    public PdfDestinationFitType FitType { get; }

    /// <summary>
    /// Zoom factor for the XYZ fit type. Null retains the current zoom.
    /// </summary>
    public float? Zoom { get; }

    /// <summary>
    /// Left coordinate for the XYZ, FitV, FitBV and FitR fit types, or null when not given.
    /// </summary>
    public float? Left { get; }

    /// <summary>
    /// Bottom coordinate for the FitR fit type, or null when not given.
    /// </summary>
    public float? Bottom { get; }

    /// <summary>
    /// Right coordinate for the FitR fit type, or null when not given.
    /// </summary>
    public float? Right { get; }

    /// <summary>
    /// Top coordinate for the XYZ, FitH, FitBH and FitR fit types, or null when not given.
    /// </summary>
    public float? Top { get; }
}
