using System;
using PdfReader.Rendering.Image.Jpg.Idct;
using PdfReader.Rendering.Image.Jpg.Model;
using PdfReader.Rendering.Image.Jpg.Readers;
using PdfReader.Rendering.Image.Jpg.Color;

namespace PdfReader.Rendering.Image.Jpg.Decoding
{
    /// <summary>
    /// Baseline JPEG decoder producing interleaved component rows (Gray=1, RGB=3, CMYK=4) via progressive row access.
    /// Decodes one MCU row band at a time, performing IDCT and color conversion directly into a band buffer.
    /// </summary>
    internal sealed class JpgBaselineDecoder : IJpgDecoder
    {
        private const int DctBlockSize = 64;

        private readonly JpgHeader _header;

        private readonly int _hMax;
        private readonly int _vMax;

        private readonly ReadOnlyMemory<byte> _entropyMemory;
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

        private readonly int _mcuWidth;
        private readonly int _mcuHeight;
        private int _currentMcuRow;
        private int _currentRow;
        private int _outputStride;

        private JpgBitReaderState _savedState;
        private bool _hasSavedState;

        public int CurrentRow => _currentRow;

        public JpgBaselineDecoder(JpgHeader header, ReadOnlyMemory<byte> entropyData)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }
            if (!header.IsBaseline)
            {
                throw new NotSupportedException("JpgBaselineDecoder supports baseline JPEG only.");
            }
            if (header.ComponentCount <= 0 || header.Components == null || header.Components.Count != header.ComponentCount)
            {
                throw new ArgumentException("Invalid header components.", nameof(header));
            }

            _header = header;
            _entropyMemory = entropyData;

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

            _mcuWidth = 8 * _hMax;
            _mcuHeight = 8 * _vMax;

            _currentMcuRow = 0;
            _currentRow = 0;
            _decoderInitialized = false;
        }

        public bool TryReadRow(Span<byte> rowBuffer)
        {
            if (rowBuffer.Length == 0)
            {
                return false;
            }

            EnsureDecoderInitialized();

            if (_currentRow >= _header.Height)
            {
                return false;
            }

            if (rowBuffer.Length < _outputStride)
            {
                throw new ArgumentException("Row buffer too small for decoded row.", nameof(rowBuffer));
            }

            if (_bandBuffer == null || _bandConsumed >= _bandProduced)
            {
                if (_currentMcuRow >= _mcuRows)
                {
                    return false;
                }
                ProduceNextBand();
                if (_bandProduced == 0)
                {
                    return false;
                }
            }

            _bandBuffer.AsSpan(_bandConsumed, _outputStride).CopyTo(rowBuffer);
            _bandConsumed += _outputStride;
            _currentRow++;
            return true;
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

            _mcuColumns = (_header.Width + _mcuWidth - 1) / _mcuWidth;
            _mcuRows = (_header.Height + _mcuHeight - 1) / _mcuHeight;

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

            _outputStride = checked(_header.Width * _header.ComponentCount);
            _bandHeight = _mcuHeight;
            _bandBuffer = new byte[_bandHeight * _outputStride];
            _bandProduced = 0;
            _bandConsumed = 0;

            _mcuWriter = McuWriterFactory.Create(
                _header,
                _componentTiles,
                _tileWidths,
                _tileHeights,
                _hMax,
                _vMax,
                _mcuWidth,
                _header.Width,
                _outputStride);

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
            int yBase = _currentMcuRow * _mcuHeight;
            int rowsRemaining = _header.Height - yBase;
            int bandRows = rowsRemaining < _mcuHeight ? rowsRemaining : _mcuHeight;
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

                int xBase = mcuColumnIndex * _mcuWidth;
                _mcuWriter.WriteToBuffer(_bandBuffer, xBase, bandRows);
                _restartManager.DecrementRestartCounter();
            }

            _savedState = bitReader.CaptureState();
            _hasSavedState = true;

            _bandHeight = bandRows;
            _bandProduced = bandRows * _outputStride;
            _bandConsumed = 0;
            _currentMcuRow++;
        }
    }
}
