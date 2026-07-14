using System;

namespace PdfPixel.Commands.Cache;

/// <summary>
/// A value stored in a <see cref="CommandCache"/> under an <see cref="ICommandCacheKey"/>.
/// </summary>
public interface ICommandCacheItem : IDisposable
{
}
