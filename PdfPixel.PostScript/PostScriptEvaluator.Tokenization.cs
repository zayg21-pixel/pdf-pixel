using Microsoft.Extensions.Logging;
using PdfPixel.PostScript.Tokens;
using System;
using System.Collections.Generic;
using System.Text;

namespace PdfPixel.PostScript;

/// <summary>
/// Tokenization helpers for PostScriptExpressionEvaluator (split for clarity).
/// Single-pass scanner building nested procedures and arrays using a stack.
/// Span-based to allow binary-safe parsing for Type1 font streams.
/// </summary>
public partial class PostScriptEvaluator
{
    private enum FrameKind
    {
        Procedure,
        Dictionary,
        Array
    }

    private struct Frame
    {
        public FrameKind Kind;
        public int StartIndex;

        public Frame(FrameKind kind, int startIndex)
        {
            Kind = kind;
            StartIndex = startIndex;
        }
    }

    private List<PostScriptToken> Tokenize(in ReadOnlySpan<byte> data)
    {
        List<PostScriptToken> result = [];
        Stack<Frame> frames = [];
        int position = 0;
        int length = data.Length;

        while (position < length)
        {
            SkipWhitespace(data, ref position);
            if (position >= length)
            {
                break;
            }

            byte b = data[position];
            var c = (char)b; // Structural ASCII only.

            switch (c)
            {
                case '{':
                {
                    position++;
                    frames.Push(new Frame(FrameKind.Procedure, result.Count));
                    continue;
                }
                case '}':
                {
                    position++;
                    if (frames.Count == 0 || frames.Peek().Kind != FrameKind.Procedure)
                    {
                        _logger.LogWarning("Unexpected closing brace '}}' with no matching '{{'.");
                        continue;
                    }

                    Frame frame = frames.Pop();
                    int count = result.Count - frame.StartIndex;
                    if (count < 0)
                    {
                        _logger.LogWarning("Internal tokenizer frame error for procedure.");
                        continue;
                    }

                    List<PostScriptToken> innerTokens = new(count);
                    for (int i = frame.StartIndex; i < result.Count; i++)
                    {
                        innerTokens.Add(result[i]);
                    }

                    result.RemoveRange(frame.StartIndex, count);
                    result.Add(new PostScriptProcedure(innerTokens));
                    continue;
                }
                case '[':
                {
                    position++;
                    frames.Push(new Frame(FrameKind.Array, result.Count));
                    continue;
                }
                case ']':
                {
                    position++;
                    if (frames.Count == 0 || frames.Peek().Kind != FrameKind.Array)
                    {
                        _logger.LogWarning("Unexpected closing bracket ']' with no matching '['.");
                        continue;
                    }

                    Frame frame = frames.Pop();
                    int count = result.Count - frame.StartIndex;
                    if (count < 0)
                    {
                        _logger.LogWarning("Internal tokenizer frame error for array.");
                        continue;
                    }

                    var inner = new PostScriptToken[count];
                    for (int i = 0; i < count; i++)
                    {
                        inner[i] = result[frame.StartIndex + i];
                    }

                    result.RemoveRange(frame.StartIndex, count);
                    result.Add(new PostScriptArray(inner));
                    continue;
                }
                case '>':
                {
                    position++;
                    if (position < length && data[position] == (byte)'>')
                    {
                        // Dictionary end '>>'
                        position++;
                        if (frames.Count == 0 || frames.Peek().Kind != FrameKind.Dictionary)
                        {
                            _logger.LogWarning("Unexpected closing dictionary '>>' with no matching '<<'.");
                            continue;
                        }

                        Frame frame = frames.Pop();
                        int count = result.Count - frame.StartIndex;
                        if (count < 0)
                        {
                            _logger.LogWarning("Internal tokenizer frame error for dictionary.");
                            continue;
                        }

                        Dictionary<string, PostScriptToken> dict = [];
                        PostScriptLiteralName? pendingKey = null;
                        for (int i = frame.StartIndex; i < result.Count; i++)
                        {
                            PostScriptToken token = result[i];
                            if (token is PostScriptExecutableName executableName && executableName.Name == "def")
                            {
                                continue;
                            }

                            if (pendingKey == null)
                            {
                                if (token is not PostScriptLiteralName keyName)
                                {
                                    _logger.LogWarning("Invalid dictionary key type.");
                                    continue;
                                }

                                pendingKey = keyName;
                            }
                            else
                            {
                                dict[pendingKey.Name] = token;
                                pendingKey = null;
                            }
                        }

                        if (pendingKey != null)
                        {
                            _logger.LogWarning("Odd number of elements in PostScript dictionary.");
                            continue;
                        }

                        result.RemoveRange(frame.StartIndex, count);
                        result.Add(new PostScriptDictionary(dict));
                        continue;
                    }
                    else
                    {
                        _logger.LogWarning("Unexpected single '>' character in PostScript data.");
                        continue;
                    }
                }
                case '(':
                {
                    byte[] str = ReadLiteralString(data, ref position);
                    result.Add(new PostScriptString(str));
                    continue;
                }
                case '<':
                {
                    if (position + 1 < length && data[position + 1] == (byte)'<')
                    {
                        // Dictionary start '<<'
                        position += 2;
                        frames.Push(new Frame(FrameKind.Dictionary, result.Count));
                        continue;
                    }
                    else
                    {
                        byte[] hex = ReadHexString(data, ref position);
                        result.Add(new PostScriptString(hex));
                        continue;
                    }
                }
                case '/':
                {
                    position++;
                    int startName = position;
                    while (position < length && !IsTokenTerminator(data[position]))
                    {
                        position++;
                    }

#if NETSTANDARD2_0
                    string name = Encoding.ASCII.GetString(data.Slice(startName, position - startName).ToArray());
#else
                    string name = Encoding.ASCII.GetString(data.Slice(startName, position - startName));
#endif
                    result.Add(new PostScriptLiteralName(name));
                    continue;
                }
                default:
                {
                    int tokenStart = position;
                    while (position < length && !IsTokenTerminator(data[position]))
                    {
                        position++;
                    }

                    int tokenLength = position - tokenStart;
                    if (tokenLength <= 0)
                    {
                        position++;
                        continue;
                    }

#if NETSTANDARD2_0
                    string raw = Encoding.ASCII.GetString(data.Slice(tokenStart, tokenLength).ToArray());
#else
                    string raw = Encoding.ASCII.GetString(data.Slice(tokenStart, tokenLength));
#endif

                    // Binary block gate detection (RD / -|).
                    if (raw == "RD" || raw == "-|")
                    {
                        // Must have preceding length number token.
                        if (result.Count == 0 || result[result.Count - 1] is not PostScriptNumber lenToken)
                        {
                            _logger.LogWarning("Binary block operator '{Operator}' encountered without preceding length number.", raw);
                            continue;
                        }

                        var byteCount = (int)lenToken.Number;
                        if (byteCount < 0 || position + byteCount > length)
                        {
                            _logger.LogWarning("Binary block length out of range: {ByteCount}", byteCount);
                            continue;
                        }

                        // Consume exactly one whitespace delimiter (spec) if present at current position.
                        if (position < length && IsPsWhitespace(data[position]))
                        {
                            position++;
                        }

                        if (position + byteCount > length)
                        {
                            _logger.LogWarning("Insufficient data for binary block length {ByteCount}", byteCount);
                            continue;
                        }

                        var block = new byte[byteCount];
                        data.Slice(position, byteCount).CopyTo(block);
                        position += byteCount;
                        result[result.Count - 1] = new PostScriptBinaryString(block); // Replace length with binary data.
                        continue;
                    }

                    char firstChar = raw[0];
                    bool numericStart = firstChar == '+' || firstChar == '-' || firstChar == '.' || (firstChar >= '0' && firstChar <= '9');

                    if (string.Equals(raw, "true", StringComparison.Ordinal))
                    {
                        result.Add(new PostScriptBoolean(true));
                    }
                    else if (string.Equals(raw, "false", StringComparison.Ordinal))
                    {
                        result.Add(new PostScriptBoolean(false));
                    }
                    else if (numericStart && float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float number))
                    {
                        result.Add(new PostScriptNumber(number));
                    }
                    else
                    {
                        result.Add(new PostScriptExecutableName(raw));
                    }

                    continue;
                }
            }
        }

        if (frames.Count > 0)
        {
            Frame open = frames.Peek();
            string kind = (open.Kind == FrameKind.Procedure) ? "{" : "[";
            _logger.LogWarning("Unclosed PostScript {Kind} starting with '{OpenChar}' at token index {StartIndex}.", open.Kind, kind, open.StartIndex);
        }

        return result;
    }

    private static bool IsPsWhitespace(byte b) => b == 0x20 || b == 0x09 || b == 0x0D || b == 0x0A || b == 0x0C || b == 0x00;

    private static bool IsTokenTerminator(byte b)
    {
        if (IsPsWhitespace(b))
        {
            return true;
        }

        switch (b)
        {
            case (byte)'%':
            case (byte)'/':
            case (byte)'(':
            case (byte)')':
            case (byte)'{':
            case (byte)'}':
            case (byte)'[':
            case (byte)']':
            case (byte)'<':
            case (byte)'>':
                return true;

            default:
                return false;
        }
    }

    private static void SkipWhitespace(in ReadOnlySpan<byte> data, ref int position)
    {
        int length = data.Length;
        while (position < length)
        {
            byte b = data[position];
            if (IsPsWhitespace(b))
            {
                position++;
                continue;
            }

            if (b == (byte)'%')
            {
                position++;
                while (position < length)
                {
                    byte cc = data[position];
                    if (cc == (byte)'\n' || cc == (byte)'\r')
                    {
                        break;
                    }

                    position++;
                }

                continue;
            }

            break;
        }
    }

    private static byte[] ReadLiteralString(in ReadOnlySpan<byte> data, ref int position)
    {
        if (position >= data.Length || data[position] != (byte)'(')
        {
            return Array.Empty<byte>();
        }

        position++; // consume '('
        List<byte> builder = [];
        int depth = 1;
        int length = data.Length;
        while (position < length && depth > 0)
        {
            byte b = data[position++];
            if (b == '(')
            {
                depth++;
                builder.Add(b);
            }
            else if (b == ')')
            {
                depth--;
                if (depth > 0)
                {
                    builder.Add(b);
                }
            }
            else if (b == '\\' && position < length)
            {
                byte escaped = data[position++];
                builder.Add(escaped);
            }
            else
            {
                builder.Add(b);
            }
        }

        return builder.ToArray();
    }

    private byte[] ReadHexString(in ReadOnlySpan<byte> data, ref int position)
    {
        if (position >= data.Length || data[position] != (byte)'<')
        {
            return Array.Empty<byte>();
        }

        position++; // consume '<'
        List<byte> bytes = [];
        int firstNibble = -1;
        int length = data.Length;
        while (position < length)
        {
            byte b = data[position];
            if (b == (byte)'>')
            {
                position++; // consume '>'
                break;
            }

            if (IsPsWhitespace(b))
            {
                position++;
                continue;
            }

            int value = HexCharToInt((char)b);
            if (value == -1)
            {
                _logger.LogWarning("Invalid hex character '{Character}' in PostScript hex string.", (char)b);
                position++;
                continue;
            }

            if (firstNibble == -1)
            {
                firstNibble = value;
            }
            else
            {
                bytes.Add((byte)((firstNibble << 4) | value));
                firstNibble = -1;
            }

            position++;
        }

        if (firstNibble != -1)
        {
            // Odd number of digits: pad with 0
            bytes.Add((byte)(firstNibble << 4));
        }

        return bytes.ToArray();
    }

    /// <summary>
    /// Converts a hex character to its integer value, or -1 if invalid.
    /// </summary>
    private static int HexCharToInt(char c)
    {
        if (c >= '0' && c <= '9')
        {
            return c - '0';
        }

        if (c >= 'A' && c <= 'F')
        {
            return c - 'A' + 10;
        }

        if (c >= 'a' && c <= 'f')
        {
            return c - 'a' + 10;
        }

        return -1;
    }
}
