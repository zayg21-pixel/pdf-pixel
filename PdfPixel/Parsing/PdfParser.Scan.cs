using System;
using PdfPixel.Models;

namespace PdfPixel.Parsing;

internal partial struct PdfParser
{
    /// <summary>
    /// Scans forward from the current position to find the first occurrence of the specified token.
    /// Returns the byte distance from the current position to the start of the token, or -1 if not found.
    /// The parser position is not modified.
    /// </summary>
    /// <summary>
    /// Attempts to consume the specified token at the current position.
    /// If the token matches, advances past it and returns true.
    /// Otherwise, leaves the position unchanged and returns false.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private bool TryConsumeToken(in PdfString token)
    {
        ReadOnlySpan<byte> tokenBytes = token.Value.Span;
        int tokenLength = tokenBytes.Length;

        if (tokenLength > Length - Position)
        {
            return false;
        }

        for (int i = 0; i < tokenLength; i++)
        {
            if (PeekByte(i) != tokenBytes[i])
            {
                return false;
            }
        }

        Advance(tokenLength);
        return true;
    }

    /// <summary>
    /// Scans forward from the current position to find the first occurrence of the specified token.
    /// Returns the byte distance from the current position to the start of the token, or -1 if not found.
    /// The parser position is not modified.
    /// </summary>
    private int ScanForToken(in PdfString token)
    {
        ReadOnlySpan<byte> tokenBytes = token.Value.Span;
        int tokenLength = tokenBytes.Length;
        int savedPosition = Position;
        byte firstByte = tokenBytes[0];

        while (Position + tokenLength <= Length)
        {
            byte current = PeekByte();

            if (current == firstByte)
            {
                var match = true;
                for (int i = 1; i < tokenLength; i++)
                {
                    if (PeekByte(i) != tokenBytes[i])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    int distance = Position - savedPosition;
                    Position = savedPosition;
                    return distance;
                }
            }

            Advance(1);
        }

        Position = savedPosition;
        return -1;
    }
}
