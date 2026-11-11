using PdfPixel.Functions;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Applies a transfer function to each component. A component the function produces no value for keeps
/// its input value.
/// </summary>
public sealed class PdfTransferFunctionColorTransform : IPdfColorTransform
{
    private readonly PdfFunction[] _componentFunctions;

    /// <summary>
    /// Initializes a transform applying <paramref name="functions"/> to <paramref name="componentCount"/> components.
    /// </summary>
    /// <param name="functions">One function applied to every component, or one function per component.</param>
    /// <param name="componentCount">Number of components of each color.</param>
    public PdfTransferFunctionColorTransform(PdfFunction[] functions, int componentCount)
    {
        if (componentCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(componentCount), "Component count must be positive.");
        }

        if (functions == null || (functions.Length != 1 && functions.Length < componentCount))
        {
            throw new ArgumentException("Either one function or one function per component is required.", nameof(functions));
        }

        _componentFunctions = new PdfFunction[componentCount];

        for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
        {
            _componentFunctions[componentIndex] = (functions.Length == 1) ? functions[0] : functions[componentIndex];
        }
    }

    /// <inheritdoc />
    public int InputComponentCount => _componentFunctions.Length;

    /// <inheritdoc />
    public int OutputComponentCount => _componentFunctions.Length;

    /// <inheritdoc />
    public void Transform(ref float input, ref float output)
    {
        for (int componentIndex = 0; componentIndex < _componentFunctions.Length; componentIndex++)
        {
            float value = Unsafe.Add(ref input, componentIndex);
            ReadOnlySpan<float> result = _componentFunctions[componentIndex].Evaluate(value);

            if (result.IsEmpty)
            {
                Unsafe.Add(ref output, componentIndex) = value;
            }
            else
            {
                Unsafe.Add(ref output, componentIndex) = result[0];
            }
        }
    }
}
