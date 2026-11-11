using PdfPixel.Commands;
using System;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// User interface graphics of a page: one recording per <see cref="PdfPanelGraphicsLayer"/> and their composite.
/// </summary>
internal sealed class PdfPanelPageGraphics
{
#if NETSTANDARD2_0
    private static readonly int LayerCount = Enum.GetValues(typeof(PdfPanelGraphicsLayer)).Length;
#else
    private static readonly int LayerCount = Enum.GetValues<PdfPanelGraphicsLayer>().Length;
#endif

    private readonly PdfCommandRecorder?[] _layers = new PdfCommandRecorder?[LayerCount];
    private readonly bool[] _updatedLayers = new bool[LayerCount];

    /// <summary>
    /// Composite of all layers, in layer order, or <see langword="null"/> if every layer is empty.
    /// </summary>
    public PdfCommandRecorder? Recording { get; private set; }

    /// <summary>
    /// Whether any layer was updated since the last <see cref="Compose"/>.
    /// </summary>
    public bool IsUpdated => Array.IndexOf(_updatedLayers, true) >= 0;

    /// <summary>
    /// Whether every layer is empty.
    /// </summary>
    public bool IsEmpty => Array.TrueForAll(_layers, IsLayerEmpty);

    /// <summary>
    /// Replaces the recording of <paramref name="layer"/>.
    /// </summary>
    public void Update(PdfPanelGraphicsLayer layer, PdfCommandRecorder? recording)
    {
        var layerIndex = (int)layer;

        _layers[layerIndex] = recording;
        _updatedLayers[layerIndex] = true;
    }

    /// <summary>
    /// Records <see cref="Recording"/> from the layers and clears the updated state.
    /// </summary>
    public void Compose()
    {
        Recording = null;
        Array.Clear(_updatedLayers, 0, _updatedLayers.Length);

        if (IsEmpty)
        {
            return;
        }

        PdfCommandRecorder recorder = new();

        foreach (PdfCommandRecorder? layer in _layers)
        {
            if (layer != null)
            {
                recorder.Process(new DrawRecordingCommand(layer));
            }
        }

        Recording = recorder;
    }

    private static bool IsLayerEmpty(PdfCommandRecorder? layer) => layer == null;
}
