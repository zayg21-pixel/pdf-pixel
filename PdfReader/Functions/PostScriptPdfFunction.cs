using Microsoft.Extensions.Logging;
using PdfReader.Models;
using PdfReader.PostScript;
using PdfReader.PostScript.Tokens;
using PdfReader.Text;
using System;
using System.Collections.Generic;

namespace PdfReader.Functions;

/// <summary>
/// Represents a PDF PostScript Calculator function (Type 4).
/// Implements evaluation using a restricted PostScript interpreter.
/// </summary>
public sealed class PostScriptPdfFunction : PdfFunction
{
    private readonly PostScriptEvaluator _evaluator;
    private readonly float[] _domain;
    private readonly float[] _range;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostScriptPdfFunction"/> class.
    /// </summary>
    /// <param name="evaluator">The PostScript expression evaluator.</param>
    /// <param name="domain">Domain array.</param>
    /// <param name="range">Range array.</param>
    private PostScriptPdfFunction(PostScriptEvaluator evaluator, float[] domain, float[] range)
    {
        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        _domain = domain ?? throw new ArgumentNullException(nameof(domain));
        _range = range ?? throw new ArgumentNullException(nameof(range));
    }

    /// <summary>
    /// Creates a PostScriptPdfFunction from a PDF function object.
    /// </summary>
    /// <param name="functionObject">PDF function object.</param>
    /// <returns>PostScriptPdfFunction instance, or null if invalid.</returns>
    public static PostScriptPdfFunction FromObject(PdfObject functionObject)
    {
        if (functionObject == null || functionObject.Dictionary == null)
        {
            return null;
        }

        var dictionary = functionObject.Dictionary;

        // Extract domain and range arrays
        float[] domain = dictionary.GetArray(PdfTokens.DomainKey)?.GetFloatArray();
        float[] range = dictionary.GetArray(PdfTokens.RangeKey)?.GetFloatArray();

        if (domain == null || domain.Length == 0)
        {
            return null;
        }

        if (range == null || range.Length == 0)
        {
            return null;
        }

        var streamData = functionObject.DecodeAsMemory();
        if (streamData.Length == 0)
        {
            return null;
        }

        var evaulator = new PostScriptEvaluator(streamData.Span, appendExec: true, functionObject.Document.LoggerFactory.CreateLogger<PostScriptEvaluator>());
        return new PostScriptPdfFunction(evaulator, domain, range);
    }

    /// <inheritdoc />
    public override ReadOnlySpan<float> Evaluate(float value)
    {
        // Delegate to multi-value overload for consistency
        return Evaluate(new float[] { value });
    }

    /// <inheritdoc />
    public override ReadOnlySpan<float> Evaluate(ReadOnlySpan<float> values)
    {
        if (values == null || values.Length == 0)
        {
            return Array.Empty<float>();
        }

        var stack = new Stack<PostScriptToken>();

        // Clamp and push input parameters to domain
        for (int inputIndex = 0; inputIndex < values.Length; inputIndex++)
        {
            float inputValue = values[inputIndex];
            float clampedValue = Clamp(inputValue, _domain, inputIndex);
            stack.Push(new PostScriptNumber(clampedValue));
        }

        _evaluator.EvaluateTokens(stack);

        int outputCount = _range.Length / 2;
        float[] result = new float[outputCount];
        for (int outputIndex = outputCount - 1; outputIndex >= 0; outputIndex--)
        {
            while (stack.Count > 0)
            {
                var value = stack.Pop();

                if (value is PostScriptNumber number)
                {
                    result[outputIndex] = Clamp(number.Value, _range, outputIndex);
                    break;
                }
            }
        }

        return result;
    }
}
