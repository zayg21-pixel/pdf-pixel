using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// User interface graphics of a page: one picture per <see cref="PdfPanelGraphicsLayer"/> and their composite.
/// </summary>
internal sealed class PdfPanelPageGraphics : IDisposable
{
#if NETSTANDARD2_0
    private static readonly int LayerCount = Enum.GetValues(typeof(PdfPanelGraphicsLayer)).Length;
#else
    private static readonly int LayerCount = Enum.GetValues<PdfPanelGraphicsLayer>().Length;
#endif

    private readonly SKPicture?[] _layers = new SKPicture?[LayerCount];
    private readonly bool[] _updatedLayers = new bool[LayerCount];

    /// <summary>
    /// Composite of all layers, or <see langword="null"/> if every layer is empty.
    /// </summary>
    public SKPicture? Picture { get; private set; }

    /// <summary>
    /// Whether any layer was updated since the last <see cref="Compose"/>.
    /// </summary>
    public bool IsUpdated => Array.IndexOf(_updatedLayers, true) >= 0;

    /// <summary>
    /// Whether every layer is empty.
    /// </summary>
    public bool IsEmpty => Array.TrueForAll(_layers, IsLayerEmpty);

    /// <summary>
    /// Replaces the picture of <paramref name="layer"/>, disposing the previous one.
    /// </summary>
    public void Update(PdfPanelGraphicsLayer layer, SKPicture? picture)
    {
        var layerIndex = (int)layer;

        _layers[layerIndex]?.Dispose();
        _layers[layerIndex] = picture;
        _updatedLayers[layerIndex] = true;
    }

    /// <summary>
    /// Records <see cref="Picture"/> from the layers within <paramref name="bounds"/> and clears the updated state.
    /// </summary>
    public void Compose(SKRect bounds)
    {
        Picture?.Dispose();
        Picture = null;
        Array.Clear(_updatedLayers, 0, _updatedLayers.Length);

        if (IsEmpty)
        {
            return;
        }

        using SKPictureRecorder recorder = new();
        SKCanvas canvas = recorder.BeginRecording(bounds);

        foreach (SKPicture? layer in _layers)
        {
            if (layer != null)
            {
                canvas.DrawPicture(layer);
            }
        }

        Picture = recorder.EndRecording();
    }

    private static bool IsLayerEmpty(SKPicture? layer) => layer == null;

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (SKPicture? layer in _layers)
        {
            layer?.Dispose();
        }

        Picture?.Dispose();
    }
}
