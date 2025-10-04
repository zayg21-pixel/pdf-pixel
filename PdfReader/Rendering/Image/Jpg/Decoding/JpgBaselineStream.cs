using System;
using System.IO;
using PdfReader.Rendering.Color;
using PdfReader.Rendering.Image.Jpg.Idct;
using PdfReader.Rendering.Image.Jpg.Model;
using PdfReader.Rendering.Image.Jpg.Readers;
using PdfReader.Rendering.Image.Jpg.Color;
using PdfReader.Streams;

namespace PdfReader.Rendering.Image.Jpg.Decoding
{
    /// <summary>
    /// Baseline JPEG decoder as a forward-only stream producing interleaved component bytes (Gray=1, RGB=3, CMYK=4).
    /// Performs per-MCU decoding and color conversion directly into an RGBA band buffer (one MCU row at a time).
    /// Plane-sized sampling info APIs are intentionally avoided (obsolete) in favor of direct MCU grid math.
    /// </summary>
    internal sealed class JpgBaselineStream : ContentStream
    {
        private const int DctBlockSize = 64;

        private readonly JpgHeader _header;

        private readonly int _hMax;
        private readonly int _vMax;

        private ReadOnlyMemory<byte> _entropyMemory;
        private bool _decoderInitialized;

        private int _mcuColumns;
        private int _mcuRows;

        private JpgHuffmanDecoderManager _decoderManager;
        private JpgQuantizationManager _quantizationManager;
        private JpgRestartManager _restartManager;
        private JpgScanSpec _scan;
        private int[] _scanToSofIndex;

        private Idct.ScaledIdctPlan[] _scaledPlans;

        private int[] _previousDc;

        private readonly int[] _blockZigZag = new int[DctBlockSize];
        private readonly int[] _idctWorkspace = new int[DctBlockSize];
        private readonly int[] _idctSubWorkspace = new int[DctBlockSize];

        private byte[][] _componentTiles;
        private int[] _tileWidths;
        private int[] _tileHeights;

        private byte[] _bandBuffer;
        private int _bandHeight;
        private int _bandProduced;
        private int _bandConsumed;

        private IMcuWriter _mcuWriter;

        public int McuWidth { get; }
        public int McuHeight { get; }
        public int McuColumns => _mcuColumns;
        public int McuRows => _mcuRows;
        public int Width => _header.Width;
        public int Height => _header.Height;
        public int OutputStride => checked(Width * _header.ComponentCount);
        public int CurrentMcuRow { get; private set; }

        public override long Position
        {
            get { return _outputBytePosition; }
            set { throw new NotSupportedException("Seeking is not supported."); }
        }

        public override long Length => throw new NotSupportedException("Length is not supported.");
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public JpgBaselineStream(JpgHeader header, ReadOnlyMemory<byte> entropyData)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }
            if (!header.IsBaseline)
            {
                throw new NotSupportedException("JpgBaselineStream supports baseline JPEG only.");
            }
            if (header.ComponentCount <= 0 || header.Components == null || header.Components.Count != header.ComponentCount)
            {
                throw new ArgumentException("Invalid header components.", nameof(header));
            }

            _header = header;
            _entropyMemory = entropyData;

            _outputBytePosition = 0;
            _bandBuffer = null;
            _bandProduced = 0;
            _bandConsumed = 0;

            int localHMax = 1;
            int localVMax = 1;
            for (int componentIndex = 0; componentIndex < header.Components.Count; componentIndex++)
            {
                var component = header.Components[componentIndex];
                if (component.HorizontalSamplingFactor > localHMax)
                {
                    localHMax = component.HorizontalSamplingFactor;
                }
                if (component.VerticalSamplingFactor > localVMax)
                {
                    localVMax = component.VerticalSamplingFactor;
                }
            }
            _hMax = Math.Max(1, localHMax);
            _vMax = Math.Max(1, localVMax);

            McuWidth = 8 * _hMax;
            McuHeight = 8 * _vMax;

            CurrentMcuRow = 0;
            _decoderInitialized = false;
        }

        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                return 0;
            }

            EnsureDecoderInitialized();

            int totalCopied = 0;
            int offset = 0;
            int remainingRequest = buffer.Length;

            while (remainingRequest > 0)
            {
                if (_bandBuffer == null || _bandConsumed >= _bandProduced)
                {
                    if (CurrentMcuRow >= _mcuRows)
                    {
                        break;
                    }
                    ProduceNextBand();
                }

                int available = _bandProduced - _bandConsumed;
                int toCopy = available < remainingRequest ? available : remainingRequest;
                _bandBuffer.AsSpan(_bandConsumed, toCopy).CopyTo(buffer.Slice(offset, toCopy));

                _bandConsumed += toCopy;
                offset += toCopy;
                remainingRequest -= toCopy;
                totalCopied += toCopy;
                _outputBytePosition += toCopy;
            }

            return totalCopied;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("Seek is not supported for JpgBaselineStream.");
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        private void EnsureDecoderInitialized()
        {
            if (_decoderInitialized)
            {
                return;
            }
            if (_header.Scans == null || _header.Scans.Count == 0)
            {
                throw new NotSupportedException("No SOS scan found in header.");
            }

            _scan = _header.Scans[0];
            _decoderManager = JpgHuffmanDecoderManager.CreateFromHeader(_header);
            _quantizationManager = JpgQuantizationManager.CreateFromHeader(_header);

            // New ValidateTablesForScan throws when invalid
            _decoderManager.ValidateTablesForScan(_scan);
            for (int componentIndex = 0; componentIndex < _header.Components.Count; componentIndex++)
            {
                int qid = _header.Components[componentIndex].QuantizationTableId;
                _quantizationManager.ValidateTableExists(qid, componentIndex);
            }

            _scanToSofIndex = JpgComponentMapper.MapScanToSofIndices(_header, _scan);
            if (_scanToSofIndex == null)
            {
                throw new InvalidOperationException("Failed to map scan components to SOF indices.");
            }

            _mcuColumns = (Width + McuWidth - 1) / McuWidth;
            _mcuRows = (Height + McuHeight - 1) / McuHeight;

            _restartManager = new JpgRestartManager(_header.RestartInterval);
            _previousDc = new int[_header.ComponentCount];

            int compCount = _header.ComponentCount;
            _componentTiles = new byte[compCount][];
            _tileWidths = new int[compCount];
            _tileHeights = new int[compCount];
            for (int ci = 0; ci < compCount; ci++)
            {
                int h = _header.Components[ci].HorizontalSamplingFactor;
                int v = _header.Components[ci].VerticalSamplingFactor;
                int tileWidth = 8 * h;
                int tileHeight = 8 * v;
                _tileWidths[ci] = tileWidth;
                _tileHeights[ci] = tileHeight;
                _componentTiles[ci] = new byte[tileWidth * tileHeight];
            }

            _bandHeight = McuHeight;
            _bandBuffer = new byte[_bandHeight * OutputStride];
            _bandProduced = 0;
            _bandConsumed = 0;

            _mcuWriter = McuWriterFactory.Create(
                _header,
                _componentTiles,
                _tileWidths,
                _tileHeights,
                _hMax,
                _vMax,
                McuWidth,
                Width,
                OutputStride);

            _scaledPlans = new Idct.ScaledIdctPlan[_header.ComponentCount];
            for (int ci = 0; ci < _header.ComponentCount; ci++)
            {
                int qid = _header.Components[ci].QuantizationTableId;
                _scaledPlans[ci] = _quantizationManager.CreateScaledIdctPlan(qid);
            }

            var startSpan = _entropyMemory.Span;
            var initialBitReader = new JpgBitReader(startSpan);
            _savedState = initialBitReader.CaptureState();
            _hasSavedState = true;

            _decoderInitialized = true;
        }

        private void ProduceNextBand()
        {
            int yBase = CurrentMcuRow * McuHeight;
            int rowsRemaining = Height - yBase;
            int bandRows = rowsRemaining < McuHeight ? rowsRemaining : McuHeight;
            if (bandRows <= 0)
            {
                _bandProduced = 0;
                _bandConsumed = 0;
                return;
            }

            var sourceSpan = _entropyMemory.Span;
            var bitReader = _hasSavedState ? new JpgBitReader(sourceSpan, _savedState) : new JpgBitReader(sourceSpan);

            for (int mcuColumnIndex = 0; mcuColumnIndex < _mcuColumns; mcuColumnIndex++)
            {
                if (_restartManager.IsRestartNeeded)
                {
                    _restartManager.ProcessRestart(ref bitReader, _previousDc);
                }

                for (int scanComponentIndex = 0; scanComponentIndex < _scan.Components.Count; scanComponentIndex++)
                {
                    int componentIndex = _scanToSofIndex[scanComponentIndex];
                    var component = _header.Components[componentIndex];
                    var scanComponent = _scan.Components[scanComponentIndex];

                    var decoders = _decoderManager.GetDecodersForScanComponent(scanComponent);
                    var dcDecoder = decoders.dcDecoder;
                    var acDecoder = decoders.acDecoder;

                    int hFactor = component.HorizontalSamplingFactor;
                    int vFactor = component.VerticalSamplingFactor;
                    int tileWidth = _tileWidths[componentIndex];
                    byte[] tile = _componentTiles[componentIndex];

                    for (int vBlock = 0; vBlock < vFactor; vBlock++)
                    {
                        for (int hBlock = 0; hBlock < hFactor; hBlock++)
                        {
                            JpgBlockDecoder.DecodeBaselineBlock(
                                ref bitReader,
                                dcDecoder,
                                acDecoder,
                                ref _previousDc[componentIndex],
                                _blockZigZag);

                            var plan = _scaledPlans[componentIndex];
                            int dstY0 = vBlock * 8;
                            int dstX0 = hBlock * 8;
                            int dstOffset = dstY0 * tileWidth + dstX0;
                            JpgIdct.TransformScaledZigZag(_blockZigZag, plan, tile.AsSpan(dstOffset), tileWidth, _idctWorkspace, _idctSubWorkspace);
                        }
                    }
                }

                int xBase = mcuColumnIndex * McuWidth;
                _mcuWriter.WriteToBuffer(_bandBuffer, xBase, bandRows);
                _restartManager.DecrementRestartCounter();
            }

            _savedState = bitReader.CaptureState();
            _hasSavedState = true;

            _bandHeight = bandRows;
            _bandProduced = bandRows * OutputStride;
            _bandConsumed = 0;
            CurrentMcuRow++;
        }

        private long _outputBytePosition;
        private JpgBitReaderState _savedState;
        private bool _hasSavedState;
    }
}
