using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Thumbnails;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace PdfPixel.PdfPanel.Wpf.Drawing;

/// <summary>
/// The visuals of a visible page and what its drawing was made with: the page's shadow in host units under the page
/// itself, drawn in page content coordinates.
/// </summary>
internal sealed class WpfPageVisual
{
    private const double ShadowBlurRadius = 9;
    private const double ShadowOpacity = 0.6;

    private static readonly DropShadowEffect ShadowEffect = CreateShadowEffect();

    public WpfPageVisual(in PdfPanelFramePage framePage)
    {
        FramePage = framePage;
        Shadow.Effect = ShadowEffect;
        RenderOptions.SetBitmapScalingMode(Content, BitmapScalingMode.Linear);
        Root.Children.Add(Shadow);
        Root.Children.Add(Content);
    }

    /// <summary>
    /// Gets the visual holding the shadow and the content of the page.
    /// </summary>
    public ContainerVisual Root { get; } = new();

    /// <summary>
    /// Gets the visual the page's outline is drawn into, in host units, casting the page's shadow.
    /// </summary>
    public DrawingVisual Shadow { get; } = new();

    /// <summary>
    /// Gets the visual the page is drawn into, in page content coordinates.
    /// </summary>
    public DrawingVisual Content { get; } = new();

    /// <summary>
    /// Gets or sets where the page is drawn on the panel.
    /// </summary>
    public PdfPanelFramePage FramePage { get; set; }

    /// <summary>
    /// Gets or sets the scale the drawing was made at, or <see langword="null"/> before the page is drawn.
    /// </summary>
    public float? DrawnScale { get; set; }

    /// <summary>
    /// Gets or sets the page corner radius the drawing was made with.
    /// </summary>
    public float DrawnCornerRadius { get; set; }

    /// <summary>
    /// Gets or sets the placeholder mode the drawing was made with.
    /// </summary>
    public PdfPanelThumbnailPlaceholderMode DrawnPlaceholderMode { get; set; }

    private static DropShadowEffect CreateShadowEffect()
    {
        DropShadowEffect effect = new()
        {
            Color = Colors.Gray,
            BlurRadius = ShadowBlurRadius,
            ShadowDepth = 0,
            Opacity = ShadowOpacity
        };
        effect.Freeze();

        return effect;
    }
}
