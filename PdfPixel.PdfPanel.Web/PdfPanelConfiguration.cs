using PdfPixel.Color;
 using PdfPixel.Geometry;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Panel values set through the <c>settings</c> object of the JS configuration, or <see langword="null"/> where none was set.
/// </summary>
internal sealed class PdfPanelConfiguration
{
    /// <summary>
    /// Gets or sets the minimum allowed zoom scale factor.
    /// </summary>
    public float? MinScale { get; set; }

    /// <summary>
    /// Gets or sets the maximum allowed zoom scale factor.
    /// </summary>
    public float? MaxScale { get; set; }

    /// <summary>
    /// Gets or sets the proportional scale change of a single zoom step.
    /// </summary>
    public float? ZoomStep { get; set; }

    /// <summary>
    /// Gets or sets the gap between pages.
    /// </summary>
    public float? PageGap { get; set; }

    /// <summary>
    /// Gets or sets the left padding of the layout.
    /// </summary>
    public float? PaddingLeft { get; set; }

    /// <summary>
    /// Gets or sets the top padding of the layout.
    /// </summary>
    public float? PaddingTop { get; set; }

    /// <summary>
    /// Gets or sets the right padding of the layout.
    /// </summary>
    public float? PaddingRight { get; set; }

    /// <summary>
    /// Gets or sets the bottom padding of the layout.
    /// </summary>
    public float? PaddingBottom { get; set; }

    /// <summary>
    /// Gets or sets the background color drawn behind the pages.
    /// </summary>
    public PdfColor? BackgroundColor { get; set; }

    /// <summary>
    /// Gets or sets the corner radius for page rendering in unscaled page space.
    /// </summary>
    public float? PageCornerRadius { get; set; }

    /// <summary>
    /// Gets or sets whether an animated placeholder is drawn over pages that have no decoded content yet.
    /// </summary>
    public bool? ShowPageLoadingAnimation { get; set; }

    /// <summary>
    /// Gets or sets whether the words of every page are extracted, not only of the pages that are rendered.
    /// </summary>
    public bool? ExtractText { get; set; }

    /// <summary>
    /// Gets or sets the color the selected text is highlighted with.
    /// </summary>
    public PdfColor? SelectionColor { get; set; }

    /// <summary>
    /// Gets or sets whether letter case must match.
    /// </summary>
    public bool? MatchCase { get; set; }

    /// <summary>
    /// Gets or sets whether a match must start and end on word boundaries.
    /// </summary>
    public bool? WholeWord { get; set; }

    /// <summary>
    /// Gets or sets whether diacritics must match.
    /// </summary>
    public bool? MatchDiacritics { get; set; }

    /// <summary>
    /// Gets or sets the color the search matches are highlighted with.
    /// </summary>
    public PdfColor? MatchColor { get; set; }

    /// <summary>
    /// Gets or sets the color the current search match is highlighted with.
    /// </summary>
    public PdfColor? CurrentMatchColor { get; set; }

    /// <summary>
    /// Sets the values that are not <see langword="null"/> on <paramref name="context"/>.
    /// </summary>
    public void Apply(PdfPanelContext context)
    {
        if (MinScale != null)
        {
            context.MinScale = MinScale.Value;
        }

        if (MaxScale != null)
        {
            context.MaxScale = MaxScale.Value;
        }

        if (ZoomStep != null)
        {
            context.ZoomStep = ZoomStep.Value;
        }

        if (PageGap != null)
        {
            context.Layout.PageGap = PageGap.Value;
        }

        PdfRectangle padding = context.Layout.Padding;
        context.Layout.Padding = new PdfRectangle(
            PaddingLeft ?? padding.Left,
            PaddingTop ?? padding.Top,
            PaddingRight ?? padding.Right,
            PaddingBottom ?? padding.Bottom);

        if (BackgroundColor != null)
        {
            context.Renderer.BackgroundColor = BackgroundColor.Value;
        }

        if (PageCornerRadius != null)
        {
            context.Renderer.PageCornerRadius = PageCornerRadius.Value;
        }

        if (ShowPageLoadingAnimation != null)
        {
            context.Renderer.ShowPageLoadingAnimation = ShowPageLoadingAnimation.Value;
        }

        if (ExtractText != null)
        {
            context.Text.ExtractText = ExtractText.Value;
        }

        if (SelectionColor != null)
        {
            context.Text.SelectionColor = SelectionColor.Value;
        }

        if (MatchCase != null)
        {
            context.Search.MatchCase = MatchCase.Value;
        }

        if (WholeWord != null)
        {
            context.Search.WholeWord = WholeWord.Value;
        }

        if (MatchDiacritics != null)
        {
            context.Search.MatchDiacritics = MatchDiacritics.Value;
        }

        if (MatchColor != null)
        {
            context.Search.MatchColor = MatchColor.Value;
        }

        if (CurrentMatchColor != null)
        {
            context.Search.CurrentMatchColor = CurrentMatchColor.Value;
        }
    }
}
