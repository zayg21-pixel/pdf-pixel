using PdfPixel.TextExtraction;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

/// <summary>
/// JSON-serializable text character transferred between the web worker and the UI thread.
/// </summary>
public struct WebTextCharacter
{
    public string Text { get; set; }

    public WebRect BoundingBox { get; set; }

    /// <summary>
    /// Converts this instance to a <see cref="PdfCharacter"/>.
    /// </summary>
    public PdfCharacter ToPdfCharacter() => new(Text, BoundingBox.ToPdfRectangle());

    /// <summary>
    /// Creates a <see cref="WebTextCharacter"/> from a <see cref="PdfCharacter"/>.
    /// </summary>
    public static WebTextCharacter FromPdfCharacter(PdfCharacter character) => new()
    {
        Text = character.Text,
        BoundingBox = WebRect.FromPdfRectangle(character.BoundingBox)
    };
}
