using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Represents a function that processes a color vector and returns a transformed color vector.
/// </summary>
/// <param name="input">The input color vector.</param>
/// <returns>The transformed color vector.</returns>
public delegate Vector4 PixelProcessorFunction(Vector4 input);
