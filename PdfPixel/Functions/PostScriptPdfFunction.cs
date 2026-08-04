using Microsoft.Extensions.Logging;
using PdfPixel.Models;
using PdfPixel.PostScript;
using PdfPixel.PostScript.Tokens;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Functions;

/// <summary>
/// Represents a PDF PostScript Calculator function (Type 4).
/// Implements evaluation using a restricted PostScript interpreter.
/// </summary>
public sealed class PostScriptPdfFunction : PdfFunction
{
    private readonly PostScriptEvaluator _evaluator;
    private readonly Action<float[], float[]>? _compiled;
    private readonly float[]? _argBuffer;
    private readonly float[]? _resultBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostScriptPdfFunction"/> class.
    /// </summary>
    /// <param name="evaluator">The PostScript expression evaluator.</param>
    /// <param name="domain">Domain entries.</param>
    /// <param name="range">Range entries.</param>
    private PostScriptPdfFunction(PostScriptEvaluator evaluator, PdfRange[] domain, PdfRange[] range)
        : base(domain, range)
    {
        if (Range == null)
        {
            throw new ArgumentNullException(nameof(range));
        }

        _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));

        // Attempt to compile a fast path producing all outputs.
        List<string> parameterNames = new(capacity: domain.Length);
        int paramCount = domain.Length;
        for (int i = 0; i < paramCount; i++)
        {
            parameterNames.Add((i == 0) ? "x" : "x" + i);
        }

        if (_evaluator.TryCompile(parameterNames, out Action<float[], float[]>? fn))
        {
            _compiled = fn;
            _argBuffer = new float[paramCount];
            _resultBuffer = new float[range.Length];
        }
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
        if (values.IsEmpty)
        {
            return Array.Empty<float>();
        }

        // should never happen
        if (Range == null)
        {
            return Array.Empty<float>();
        }

        int outputCount = Range.Length;

        // Fast path: compiled vector function if available.
        if (_compiled != null && _argBuffer != null && _resultBuffer != null)
        {
            values.CopyTo(_argBuffer);
            Clamp(_argBuffer, Domain);

            _compiled(_argBuffer, _resultBuffer);

            Clamp(_resultBuffer, Range);

            return _resultBuffer.AsSpan(0, outputCount);
        }

        Stack<PostScriptToken> stack = [];

        // Clamp and push input parameters to domain
        for (int inputIndex = 0; inputIndex < values.Length; inputIndex++)
        {
            float inputValue = values[inputIndex];
            float clampedValue = Clamp(inputValue, Domain, inputIndex);
            stack.Push(new PostScriptNumber(clampedValue));
        }

        _evaluator.EvaluateTokens(stack);

        var resultInterp = new float[outputCount];
        for (int outputIndex = outputCount - 1; outputIndex >= 0; outputIndex--)
        {
            while (stack.Count > 0)
            {
                PostScriptToken value = stack.Pop();

                if (value is PostScriptNumber number)
                {
                    resultInterp[outputIndex] = Clamp(number.Number, Range, outputIndex);
                    break;
                }
            }
        }

        return resultInterp;
    }

    /// <summary>
    /// Creates a PostScriptPdfFunction from a PDF function object.
    /// </summary>
    /// <param name="functionObject">PDF function object.</param>
    /// <returns>PostScriptPdfFunction instance, or null if invalid.</returns>
    public static PostScriptPdfFunction? FromObject(PdfObject functionObject)
    {
        if (functionObject == null)
        {
            return null;
        }

        PdfDictionary dictionary = functionObject.Dictionary;

        // Extract domain and range arrays
        float[]? domain = dictionary.GetArray(PdfTokens.DomainKey)?.GetFloatArray();
        float[]? range = dictionary.GetArray(PdfTokens.RangeKey)?.GetFloatArray();

        if (domain == null || domain.Length == 0)
        {
            return null;
        }

        if (range == null || range.Length == 0)
        {
            return null;
        }

        ReadOnlyMemory<byte> streamData = functionObject.DecodeAsMemory();
        if (streamData.Length == 0)
        {
            return null;
        }

        PostScriptEvaluator evaulator = new(streamData.Span, appendExec: true, functionObject.Document.LoggerFactory.CreateLogger<PostScriptEvaluator>());
        return new PostScriptPdfFunction(evaulator, PdfRange.FromArray(domain), PdfRange.FromArray(range));
    }
}
