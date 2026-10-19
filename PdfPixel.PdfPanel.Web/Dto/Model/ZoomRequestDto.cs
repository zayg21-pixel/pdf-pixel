namespace PdfPixel.PdfPanel.Web.Dto.Model;

/// <summary>
/// Zoom request <c>{ scale, step, centerX, centerY }</c>: to <c>scale</c> when set, otherwise one step by the sign of <c>step</c>.
/// </summary>
internal sealed class ZoomRequestDto
{
    public float? Scale { get; set; }

    public int Step { get; set; }

    public float CenterX { get; set; }

    public float CenterY { get; set; }
}
