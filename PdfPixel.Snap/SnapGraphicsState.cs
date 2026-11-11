using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Snap.Clipping;
using PdfPixel.Snap.Text;
using PdfPixel.Snap.Transparency;
using System;

namespace PdfPixel.Snap;

/// <summary>
/// The drawing state of a surface that a save keeps for its restore.
/// </summary>
internal sealed class SnapGraphicsState : IDisposable
{
    private bool _ownsSoftMask;

    /// <summary>
    /// Initializes the state of drawing into <paramref name="target"/> under the identity matrix, with no clip, tint or
    /// soft mask, adding content to the content tree, within <paramref name="visibleRegion"/>.
    /// </summary>
    public SnapGraphicsState(SnapLayer target, PdfRectangle? visibleRegion)
        : this(target, visibleRegion, localSaveDepth: null, setAsideTextObject: null)
    {
    }

    private SnapGraphicsState(SnapLayer target, PdfRectangle? visibleRegion, int? localSaveDepth, SnapTextObject? setAsideTextObject)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        VisibleRegion = visibleRegion;
        LocalSaveDepth = localSaveDepth;
        SetAsideTextObject = setAsideTextObject;
    }

    public PdfMatrix DeviceMatrix { get; set; } = PdfMatrix.Identity;

    public SnapClip? Clip { get; set; }

    public SnapLayer Target { get; set; }

    /// <summary>
    /// Gets or sets the paint whose color replaces the colors of an uncolored pattern cell or a colorless Type 3 glyph.
    /// </summary>
    public PdfPaint? TintPaint { get; set; }

    public SnapSoftMask? SoftMask { get; private set; }

    /// <summary>
    /// Gets or sets the part of the page visible in device space, or <see langword="null"/> for the full page.
    /// </summary>
    public PdfRectangle? VisibleRegion { get; set; }

    /// <summary>
    /// Gets how many saves deep this state is in the local drawing it belongs to, or <see langword="null"/> outside
    /// local drawing.
    /// </summary>
    public int? LocalSaveDepth { get; }

    /// <summary>
    /// Gets whether this state belongs to a local drawing, whose content is not added to the content tree.
    /// </summary>
    public bool IsLocal => LocalSaveDepth != null;

    /// <summary>
    /// Gets the text object set aside when the local drawing this state belongs to began.
    /// </summary>
    public SnapTextObject? SetAsideTextObject { get; }

    /// <summary>
    /// Returns the state a local drawing into <paramref name="target"/> starts in: under <paramref name="deviceMatrix"/>
    /// and <paramref name="visibleRegion"/>, with no clip, tint or soft mask, adding nothing to the content tree, with
    /// <paramref name="setAsideTextObject"/> kept for its end.
    /// </summary>
    public static SnapGraphicsState CreateLocal(SnapLayer target, in PdfMatrix deviceMatrix, PdfRectangle? visibleRegion, SnapTextObject? setAsideTextObject)
    {
        SnapGraphicsState state = new(target, visibleRegion, localSaveDepth: 0, setAsideTextObject) { DeviceMatrix = deviceMatrix };

        return state;
    }

    /// <summary>
    /// Sets <paramref name="softMask"/>, owned by this state, disposing the soft mask this state owned.
    /// </summary>
    public void SetSoftMask(SnapSoftMask? softMask)
    {
        DisposeOwnedSoftMask();
        SoftMask = softMask;
        _ownsSoftMask = softMask != null;
    }

    /// <summary>
    /// Creates the state that continues after a save, one save deeper, sharing the soft mask without owning it.
    /// </summary>
    public SnapGraphicsState Clone()
    {
        SnapGraphicsState copy = new(Target, VisibleRegion, LocalSaveDepth + 1, SetAsideTextObject)
        {
            DeviceMatrix = DeviceMatrix,
            Clip = Clip,
            TintPaint = TintPaint,
            SoftMask = SoftMask
        };

        return copy;
    }

    private void DisposeOwnedSoftMask()
    {
        if (_ownsSoftMask && SoftMask != null)
        {
            SoftMask.Dispose();
        }

        _ownsSoftMask = false;
    }

    /// <inheritdoc />
    public void Dispose() => DisposeOwnedSoftMask();
}
