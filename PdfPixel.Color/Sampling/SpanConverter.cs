using PdfPixel.Color.Transform;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Sampling;

/// <summary>
/// Pre-processes a span of color components before they enter a color transform pipeline.
/// Used for tint functions in Separation and DeviceN color spaces.
/// </summary>
/// <param name="source">Input color components, one element per channel.</param>
/// <returns>Remapped components to be forwarded to the transform pipeline.</returns>
public delegate ReadOnlySpan<float> SpanConverter(ReadOnlySpan<float> source);
