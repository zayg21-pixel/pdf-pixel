using System;

namespace PdfPixel.Commands.Cache;

/// <summary>
/// Identifies a <see cref="ShadingCommandCacheEntry"/> for a given <see cref="ShadingDecodingContext"/>.
/// Two keys are equal when they were captured for the same shading, color space converter,
/// rendering intent, and transfer function.
/// </summary>
internal sealed class ShadingCommandCacheKey : ICommandCacheKey
{
    private readonly ShadingDecodingContext _context;

    public ShadingCommandCacheKey(ShadingDecodingContext context) => _context = context ?? throw new ArgumentNullException(nameof(context));

    public bool Equals(ICommandCacheKey? other)
    {
        return other is ShadingCommandCacheKey key
            && ReferenceEquals(_context.Shading, key._context.Shading)
            && ReferenceEquals(_context.Converter, key._context.Converter)
            && _context.RenderingIntent == key._context.RenderingIntent
            && ReferenceEquals(_context.TransferFunction, key._context.TransferFunction);
    }

    public override bool Equals(object? obj) => Equals(obj as ICommandCacheKey);

    public override int GetHashCode() => HashCode.Combine(_context.Shading, _context.Converter, _context.RenderingIntent, _context.TransferFunction);
}
