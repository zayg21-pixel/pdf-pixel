using System.Runtime.CompilerServices;

namespace PdfReader.Parsing;

partial struct PdfParser
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool GetIsAtEnd()
    {
        if (_streamMode)
        {
            return _lastSetPostion >= _length;
        }
        else
        {
            return _parseContext.IsAtEnd;
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int GetPosition()
    {
        if (_streamMode)
        {
            return _lastSetPostion;
        }
        else
        {
            return _parseContext.Position;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetPosition(int position)
    {
        if (_streamMode)
        {
            _stream.Position = position;
            _lastSetPostion = position;
        }
        else
        {
            _parseContext.Position = position;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte PeekByte(int offset = 0)
    {
        if (_streamMode)
        {
            RestorePosition(offset: offset);
            var value = _stream.ReadByte();

            if (value == -1)
            {
                return 0;
            }

            return (byte)value;
        }
        else
        {
            return _parseContext.PeekByte(offset);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private byte ReadByte()
    {
        if (_streamMode)
        {
            RestorePosition();
            int value = _stream.ReadByte();

            if (value == -1)
            {
                return 0;
            }

            _lastSetPostion++;
            return (byte)value;
        }
        else
        {
            return _parseContext.ReadByte();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Advance(int offset)
    {
        if (_streamMode)
        {
            _lastSetPostion += offset;
        }
        else
        {
            _parseContext.Advance(offset);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RestorePosition(int offset = 0)
    {
        if (_streamMode)
        {
            if (_stream.Position != _lastSetPostion + offset)
            {
                _stream.Position = _lastSetPostion + offset;
            }
        }
    }
}
