namespace PdfPixel.Snap.Painting;

/// <summary>
/// The geometry of an element: draws its shape, within the draw limits and the clip, handing each row to a painter
/// of the element's source.
/// </summary>
internal interface ISnapGeometry
{
    /// <summary>
    /// Draws the shape, handing each row to <paramref name="painter"/>.
    /// </summary>
    void Draw<TPainter>(TPainter painter)
        where TPainter : ISnapRowPainter;
}
