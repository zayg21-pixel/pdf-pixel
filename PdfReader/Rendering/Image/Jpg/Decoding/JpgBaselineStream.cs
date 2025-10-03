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
    /// Baseline JPEG decoder that exposes the decoded sample bytes as a forward-only, non-seekable stream.
    /// The stream produces interleaved raw component bytes (Gray=1, RGB=3, CMYK=4) in row-major order on Read().
    /// Color conversion or additional processing is expected to be handled by a higher level post-processor.
    /// </summary>
    internal sealed class JpgBaselineStream : ContentStream
    {
        private readonly JpgHeader _header;
        private readonly PdfColorSpaceConverter _colorConverter;

        private readonly int _hMax;
        private readonly int _vMax;

        private ReadOnlyMemory<byte> _entropyMemory;
        private bool _decoderInitialized;

        private SamplingInfo _samplingInfo;
        private int _mcuColumns;
        private int _mcuRows;

        private JpgHuffmanDecoderManager _decoderManager;
        private JpgQuantizationManager _quantizationManager;
        private JpgRestartManager _restartManager;
        private JpgScanSpec _scan;
        private int[] _scanToSofIndex;

        private Idct.ScaledIdctPlan[] _scaledPlans;

        private int[] _previousDc;

        // Per-MCU reusable buffers
        private readonly int[] _blockZigZag = new int[64];
        private readonly int[] _blockNatural = new int[64];
        private readonly byte[] _idctTemp = new byte[64];
        private readonly int[] _idctWorkspace = new int[64];
        private readonly int[] _idctSubWorkspace = new int[64];

        // Per-component MCU tiles (reused)
        private byte[][] _componentTiles;
        private int[] _tileWidths;
        private int[] _tileHeights;

        // Output band buffer for one MCU row (reused)
        private byte[] _bandBuffer;
        private int _bandHeight;
        private int _bandProduced;
        private int _bandConsumed;

        private IMcuWriter _mcuWriter;

        /// <summary>
        /// Width of one MCU in pixels.
        /// </summary>
        public int McuWidth { get; }

        /// <summary>
        /// Height of one MCU in pixels.
        /// </summary>
        public int McuHeight { get; }

        /// <summary>
        /// Number of MCU columns covering the full image width.
        /// </summary>
        public int McuColumns => _mcuColumns;

        /// <summary>
        /// Number of MCU rows covering the full image height.
        /// </summary>
        public int McuRows => _mcuRows;

        /// <summary>
        /// Image width in pixels.
        /// </summary>
        public int Width => _header.Width;

        /// <summary>
        /// Image height in pixels.
        /// </summary>
        public int Height => _header.Height;

        /// <summary>
        /// Output pixel stride in bytes per row = Width * ComponentCount (1,3,4).
        /// </summary>
        public int OutputStride => checked(Width * _header.ComponentCount);

        /// <summary>
        /// Current MCU row index (0-based) for streaming decode.
        /// </summary>
        public int CurrentMcuRow { get; private set; }

        public override long Position
        {
            get { return _outputBytePosition; }
            set { throw new NotSupportedException("Seeking is not supported."); }
        }

        public override long Length
        {
            get { throw new NotSupportedException("Length is not supported."); }
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public JpgBaselineStream(JpgHeader header, ReadOnlyMemory<byte> entropyData, PdfColorSpaceConverter colorConverter)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            if (colorConverter == null)
            {
                throw new ArgumentNullException(nameof(colorConverter));
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
            _colorConverter = colorConverter;
            _entropyMemory = entropyData;

            _outputBytePosition = 0;
            _bandBuffer = null;
            _bandProduced = 0;
            _bandConsumed = 0;

            int localHMax = 1;
            int localVMax = 1;
            for (int i = 0; i < header.Components.Count; i++)
            {
                var c = header.Components[i];
                if (c.HorizontalSamplingFactor > localHMax)
                {
                    localHMax = c.HorizontalSamplingFactor;
                }

                if (c.VerticalSamplingFactor > localVMax)
                {
                    localVMax = c.VerticalSamplingFactor;
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
            int count = buffer.Length;

            while (count > 0)
            {
                if (_bandBuffer == null || _bandConsumed >= _bandProduced)
                {
                    if (CurrentMcuRow >= _mcuRows)
                    {
                        break;
                    }

                    // Produce the next MCU-row band into _bandBuffer
                    ProduceNextBand();
                }

                int available = _bandProduced - _bandConsumed;
                int toCopy = available < count ? available : count;
                _bandBuffer.AsSpan(_bandConsumed, toCopy).CopyTo(buffer.Slice(offset, toCopy));

                _bandConsumed += toCopy;
                offset += toCopy;
                count -= toCopy;
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

            if (!_decoderManager.ValidateTablesForScan(_scan))
            {
                throw new InvalidOperationException("Required Huffman tables missing for scan.");
            }

            for (int componentIndex = 0; componentIndex < _header.Components.Count; componentIndex++)
            {
                int qid = _header.Components[componentIndex].QuantizationTableId;
                if (!_quantizationManager.ValidateTableExists(qid, componentIndex))
                {
                    throw new InvalidOperationException($"Quantization table {qid} missing for component {componentIndex}.");
                }
            }

            _scanToSofIndex = JpgComponentMapper.MapScanToSofIndices(_header, _scan, permissive: false);
            if (_scanToSofIndex == null)
            {
                throw new InvalidOperationException("Failed to map scan components to SOF indices.");
            }

            _samplingInfo = JpgComponentSampler.CalculateSamplingInfo(_header);
            var dims = JpgComponentSampler.CalculateMcuDimensions(_header, _samplingInfo);
            _mcuColumns = dims.mcuColumns;
            _mcuRows = dims.mcuRows;

            _restartManager = new JpgRestartManager(_header.RestartInterval);

            _previousDc = new int[_header.ComponentCount];

            // Prepare per-component tiles reused per MCU.
            int compCount = _header.ComponentCount;
            _componentTiles = new byte[compCount][];
            _tileWidths = new int[compCount];
            _tileHeights = new int[compCount];
            for (int ci = 0; ci < compCount; ci++)
            {
                int h = _header.Components[ci].HorizontalSamplingFactor;
                int v = _header.Components[ci].VerticalSamplingFactor;
                int tw = 8 * h;
                int th = 8 * v;
                _tileWidths[ci] = tw;
                _tileHeights[ci] = th;
                _componentTiles[ci] = new byte[tw * th];
            }

            // Allocate band buffer for one MCU row (RGBA). We do not clear it per band because we overwrite all valid pixels.
            _bandHeight = McuHeight;
            _bandBuffer = new byte[_bandHeight * OutputStride];
            _bandProduced = 0;
            _bandConsumed = 0;

            // Create appropriate MCU writer based on color mode
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

            // Precompute scaled IDCT plans per component
            _scaledPlans = new Idct.ScaledIdctPlan[_header.ComponentCount];
            for (int ci = 0; ci < _header.ComponentCount; ci++)
            {
                int qid = _header.Components[ci].QuantizationTableId;
                _scaledPlans[ci] = _quantizationManager.CreateScaledIdctPlan(qid);
            }

            // Initialize bit reader state at start
            var startSpan = _entropyMemory.Span;
            var br0 = new JpgBitReader(startSpan);
            _savedState = br0.CaptureState();
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

            // No Array.Clear: the writer fills all pixels up to image width for each row.

            var sourceSpan = _entropyMemory.Span;
            var bitReader = _hasSavedState ? new JpgBitReader(sourceSpan, _savedState) : new JpgBitReader(sourceSpan);

            for (int mcuCol = 0; mcuCol < _mcuColumns; mcuCol++)
            {
                if (_restartManager.IsRestartNeeded)
                {
                    if (!_restartManager.ProcessRestart(ref bitReader, _previousDc))
                    {
                        throw new InvalidOperationException($"Restart processing failed at MCU ({mcuCol},{CurrentMcuRow}).");
                    }
                }

                // Decode all blocks for components into their MCU tiles
                for (int scanComp = 0; scanComp < _scan.Components.Count; scanComp++)
                {
                    int compIndex = _scanToSofIndex[scanComp];
                    var comp = _header.Components[compIndex];
                    var scanComponent = _scan.Components[scanComp];

                    var decoders = _decoderManager.GetDecodersForScanComponent(scanComponent);
                    var dcDecoder = decoders.dcDecoder;
                    var acDecoder = decoders.acDecoder;

                    int qid = comp.QuantizationTableId;
                    int h = comp.HorizontalSamplingFactor;
                    int v = comp.VerticalSamplingFactor;

                    int tileWidth = _tileWidths[compIndex];
                    byte[] tile = _componentTiles[compIndex];

                    for (int vBlock = 0; vBlock < v; vBlock++)
                    {
                        for (int hBlock = 0; hBlock < h; hBlock++)
                        {
                            if (!JpgBlockDecoder.DecodeBaselineBlock(
                                ref bitReader,
                                dcDecoder,
                                acDecoder,
                                ref _previousDc[compIndex],
                                _blockZigZag))
                            {
                                throw new InvalidOperationException($"Block decode failed at MCU ({mcuCol},{CurrentMcuRow}), component {scanComp}, block ({hBlock},{vBlock}).");
                            }

                            var plan = _scaledPlans[compIndex];
                            int dstY0 = vBlock * 8;
                            int dstX0 = hBlock * 8;
                            int dstOff = dstY0 * tileWidth + dstX0;
                            JpgIdct.TransformScaledZigZag(_blockZigZag, plan, tile.AsSpan(dstOff), tileWidth, _idctWorkspace, _idctSubWorkspace);
                        }
                    }
                }

                // Convert this MCU region into RGBA in band buffer
                int xBase = mcuCol * McuWidth;
                _mcuWriter.WriteToBuffer(_bandBuffer, xBase, bandRows);

                _restartManager.DecrementRestartCounter();
            }

            // Save state for next band
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
