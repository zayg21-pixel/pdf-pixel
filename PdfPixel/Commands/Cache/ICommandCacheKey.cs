using System;

namespace PdfPixel.Commands.Cache;

/// <summary>
/// Identifies an entry stored in a <see cref="CommandCache"/>.
/// </summary>
public interface ICommandCacheKey : IEquatable<ICommandCacheKey>
{
}
