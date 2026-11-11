using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Commands.Cache;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Snap.Color;

/// <summary>
/// The color converters of one document under one set of device profiles, built on first use.
/// </summary>
internal sealed class SnapColorConverterCache : ICommandCacheItem
{
    private readonly Dictionary<SnapColorConverterKey, SnapColorConverter> _converters = [];
    private readonly PdfDeviceProfiles _deviceProfiles;

    private SnapColorConverterCache(PdfDeviceProfiles deviceProfiles) => _deviceProfiles = deviceProfiles;

    /// <summary>
    /// Returns the converter cache for <paramref name="deviceProfiles"/> stored in <paramref name="cache"/>, creating it
    /// on first use.
    /// </summary>
    public static SnapColorConverterCache Get(CommandCache cache, PdfDeviceProfiles deviceProfiles)
    {
        CacheKey key = new(deviceProfiles);

        if (cache.GetEntry(key) is SnapColorConverterCache existing)
        {
            return existing;
        }

        SnapColorConverterCache created = new(deviceProfiles);
        cache.StoreEntry(key, created);

        return created;
    }

    /// <summary>
    /// Returns the converter for the colors of <paramref name="paint"/> into <paramref name="targetColorSpace"/>.
    /// </summary>
    public SnapColorConverter GetConverter(PdfPaint paint, PdfColorSpaceConverter targetColorSpace)
    {
        SnapColorConverterKey key = new(
            paint.ColorSpace,
            paint.DeviceColorMapping,
            paint.RenderingIntent,
            paint.UseBlackPointCompensation,
            paint.TransferFunction,
            targetColorSpace);

        if (!_converters.TryGetValue(key, out SnapColorConverter? converter))
        {
            converter = new SnapColorConverter(key, _deviceProfiles);
            _converters.Add(key, converter);
        }

        return converter;
    }

    /// <inheritdoc />
    public void Dispose()
    {
    }

    private sealed class CacheKey : ICommandCacheKey
    {
        private readonly PdfDeviceProfiles _deviceProfiles;

        public CacheKey(PdfDeviceProfiles deviceProfiles) => _deviceProfiles = deviceProfiles;

        public bool Equals(ICommandCacheKey? other) => other is CacheKey key && ReferenceEquals(_deviceProfiles, key._deviceProfiles);

        public override bool Equals(object? obj) => obj is CacheKey key && ReferenceEquals(_deviceProfiles, key._deviceProfiles);

        public override int GetHashCode() => RuntimeHelpers.GetHashCode(_deviceProfiles);
    }
}
