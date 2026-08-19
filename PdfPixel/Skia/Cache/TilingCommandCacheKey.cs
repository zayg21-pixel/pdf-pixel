using PdfPixel.Color;
using PdfPixel.Commands.Cache;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using System;

namespace PdfPixel.Skia.Cache;

/// <summary>
/// Identifies a <see cref="TilingCommandCacheEntry"/> built for one <see cref="DrawTilingCommand"/>.
/// </summary>
internal sealed class TilingCommandCacheKey : ICommandCacheKey
{
    private readonly PdfCommandRecorder _recorder;
    private readonly PdfColor? _tintColor;
    private readonly PdfMatrix _deviceMatrix;
    private readonly bool _repeating;

    public TilingCommandCacheKey(DrawTilingCommand command, in PdfMatrix deviceMatrix, bool repeating)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        _recorder = command.RecordingCommand.Recorder;
        _tintColor = command.RecordingCommand.TintColor;
        _deviceMatrix = deviceMatrix;
        _repeating = repeating;
    }

    public bool Equals(ICommandCacheKey? other)
    {
        return other is TilingCommandCacheKey key
            && ReferenceEquals(_recorder, key._recorder)
            && Nullable.Equals(_tintColor, key._tintColor)
            && _deviceMatrix.Equals(key._deviceMatrix)
            && _repeating == key._repeating;
    }

    public override bool Equals(object? obj) => Equals(obj as ICommandCacheKey);

    public override int GetHashCode() => HashCode.Combine(_recorder, _tintColor, _deviceMatrix, _repeating);
}
