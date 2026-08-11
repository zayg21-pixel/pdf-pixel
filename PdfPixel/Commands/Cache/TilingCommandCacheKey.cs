using PdfPixel.Color;
using PdfPixel.Geometry;
using System;

namespace PdfPixel.Commands.Cache;

/// <summary>
/// Identifies a <see cref="TilingCommandCacheEntry"/> built for one <see cref="DrawTilingCommand"/>.
/// Two keys are equal when they were captured for the same recorded cell, the same tint color, the
/// same cell geometry, and the same device matrix, which together decide every pixel of the recorded
/// picture. The tint color belongs to the identity because an uncolored pattern bakes the fill color
/// in effect at its use into the picture, so the same cell drawn in two colors needs two entries.
/// </summary>
internal sealed class TilingCommandCacheKey : ICommandCacheKey
{
    private readonly PdfCommandRecorder _recorder;
    private readonly PdfColor? _tintColor;
    private readonly PdfRectangle _bbox;
    private readonly float _xStep;
    private readonly float _yStep;
    private readonly PdfMatrix _deviceMatrix;
    private readonly bool _repeating;

    public TilingCommandCacheKey(DrawTilingCommand command, in PdfMatrix deviceMatrix, bool repeating)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        _recorder = command.RecordingCommand.Recorder;
        _tintColor = command.RecordingCommand.Modifier?.Color;
        _bbox = command.BBox;
        _xStep = command.XStep;
        _yStep = command.YStep;
        _deviceMatrix = deviceMatrix;
        _repeating = repeating;
    }

    public bool Equals(ICommandCacheKey? other)
    {
        return other is TilingCommandCacheKey key
            && ReferenceEquals(_recorder, key._recorder)
            && Nullable.Equals(_tintColor, key._tintColor)
            && _bbox.Equals(key._bbox)
            && _xStep.Equals(key._xStep)
            && _yStep.Equals(key._yStep)
            && _deviceMatrix.Equals(key._deviceMatrix)
            && _repeating == key._repeating;
    }

    public override bool Equals(object? obj) => Equals(obj as ICommandCacheKey);

    public override int GetHashCode() => HashCode.Combine(_recorder, _tintColor, _bbox, _xStep, _yStep, _deviceMatrix, _repeating);
}
