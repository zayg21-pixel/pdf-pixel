using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace PdfReader.Parsing
{
    partial struct PdfParser
    {

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool GetIsAtEnd()
        {
            if (_streamMode)
            {
                return (int)_lastSetPostion >= _reader.BaseStream.Length;
            }
            else
            {
                return _parseContext.IsAtEnd;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int GetLength()
        {
            if (_streamMode)
            {
                return (int)_reader.BaseStream.Length;
            }
            else
            {
                return _parseContext.Length;
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
                _reader.BaseStream.Position = position;
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
                RestorePosition();

                if (offset == 0)
                {
                    return (byte)_reader.PeekChar();
                }

                _reader.BaseStream.Seek(offset, SeekOrigin.Current);
                int value = _reader.PeekChar();
                _reader.BaseStream.Seek(-offset, SeekOrigin.Current);

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
                int value = _reader.Read();

                if (value == -1)
                {
                    return 0;
                }

                SavePosition();

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
                RestorePosition();
                _reader.BaseStream.Seek(offset, SeekOrigin.Current);
                SavePosition();
            }
            else
            {
                _parseContext.Advance(offset);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ReadOnlyMemory<byte> ReadSliceFromCurrent(int dataLength)
        {
            if (_streamMode)
            {
                RestorePosition();
                long originalPosition = _reader.BaseStream.Position;
                byte[] buffer = new byte[dataLength];
                _reader.Read(buffer, 0, dataLength);

                SavePosition();

                return buffer;
            }

            var result = _parseContext.GetSliceFromCurrent(dataLength);
            _parseContext.Advance(dataLength);
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SavePosition()
        {
            if (_streamMode)
            {
                _lastSetPostion = (int)_reader.BaseStream.Position;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void RestorePosition()
        {
            if (_streamMode)
            {
                if (_reader.BaseStream.Position != _lastSetPostion)
                {
                    _reader.BaseStream.Position = _lastSetPostion;
                }
            }
        }
    }
}
