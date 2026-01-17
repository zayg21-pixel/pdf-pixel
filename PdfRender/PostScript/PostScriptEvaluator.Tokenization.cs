using System;
using System.Collections.Generic;
using PdfRender.Text;
using System.Text;
using PdfRender.PostScript.Tokens;

namespace PdfRender.PostScript
{
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

        private List<PostScriptToken> Tokenize(ReadOnlySpan<byte> data)
        {
            var result = new List<PostScriptToken>();
            var frames = new Stack<Frame>();
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
                char c = (char)b; // Structural ASCII only.

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
                            throw new InvalidOperationException("Unexpected closing brace '}' with no matching '{'.");
                        }
                        Frame frame = frames.Pop();
                        int count = result.Count - frame.StartIndex;
                        if (count < 0)
                        {
                            throw new InvalidOperationException("Internal tokenizer frame error for procedure.");
                        }
                        var innerTokens = new List<PostScriptToken>(count);
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
                            throw new InvalidOperationException("Unexpected closing bracket ']' with no matching '['.");
                        }
                        Frame frame = frames.Pop();
                        int count = result.Count - frame.StartIndex;
                        if (count < 0)
                        {
                            throw new InvalidOperationException("Internal tokenizer frame error for array.");
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
                                throw new InvalidOperationException("Unexpected closing dictionary '>>' with no matching '<<'.");
                            }
                            Frame frame = frames.Pop();
                            int count = result.Count - frame.StartIndex;
                            if (count < 0)
                            {
                                throw new InvalidOperationException("Internal tokenizer frame error for dictionary.");
                            }
                            var dict = new Dictionary<string, PostScriptToken>();
                            for (int i = frame.StartIndex; i < result.Count; i += 2)
                            {
                                if (i + 1 >= result.Count)
                                {
                                    throw new InvalidOperationException("Odd number of elements in PostScript dictionary.");
                                }

                                var key = result[i];

                                if (key is not PostScriptLiteralName keyName)
                                {
                                    throw new InvalidOperationException("Invalid dictionary key type.");
                                }

                                dict[keyName.Name] = result[i + 1];
                            }
                            result.RemoveRange(frame.StartIndex, count);
                            result.Add(new PostScriptDictionary(dict));
                            continue;
                        }
                        else
                        {
                            throw new InvalidOperationException("Unexpected single '>' character in PostScript data.");
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
                        string name = Encoding.ASCII.GetString(data.Slice(startName, position - startName));
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
                        string raw = Encoding.ASCII.GetString(data.Slice(tokenStart, tokenLength));
                        // Binary block gate detection (RD / -|).
                        if (raw == "RD" || raw == "-|")
                        {
                            // Must have preceding length number token.
                            if (result.Count == 0 || result[result.Count - 1] is not PostScriptNumber lenToken)
                            {
                                throw new InvalidOperationException("Binary block operator '" + raw + "' encountered without preceding length number.");
                            }
                            int byteCount = (int)lenToken.Value;
                            if (byteCount < 0 || position + byteCount > length)
                            {
                                throw new InvalidOperationException("Binary block length out of range: " + byteCount);
                            }

                            // Consume exactly one whitespace delimiter (spec) if present at current position.
                            if (position < length && IsPsWhitespace(data[position]))
                            {
                                position++;
                            }

                            if (position + byteCount > length)
                            {
                                throw new InvalidOperationException("Insufficient data for binary block length " + byteCount);
                            }
                            byte[] block = new byte[byteCount];
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
                string kind = open.Kind == FrameKind.Procedure ? "{" : "[";
                throw new InvalidOperationException($"Unclosed PostScript {open.Kind} starting with '{kind}' at token index {open.StartIndex}.");
            }

            return result;
        }

        private static bool IsPsWhitespace(byte b)
        {
            return b == 0x20 || b == 0x09 || b == 0x0D || b == 0x0A || b == 0x0C;
        }

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

        private static void SkipWhitespace(ReadOnlySpan<byte> data, ref int position)
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

        private static byte[] ReadLiteralString(ReadOnlySpan<byte> data, ref int position)
        {
            if (position >= data.Length || data[position] != (byte)'(')
            {
                return Array.Empty<byte>();
            }
            position++; // consume '('
            var builder = new List<byte>();
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
                    var escaped = data[position++];
                    builder.Add(escaped);
                }
                else
                {
                    builder.Add(b);
                }
            }
            return builder.ToArray();
        }

        private static byte[] ReadHexString(ReadOnlySpan<byte> data, ref int position)
        {
            if (position >= data.Length || data[position] != (byte)'<')
            {
                return Array.Empty<byte>();
            }
            position++; // consume '<'
            var bytes = new List<byte>();
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
                    throw new InvalidOperationException($"Invalid hex character '{(char)b}' in PostScript hex string.");
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
}
