using System;
using System.IO;
using System.Collections.Generic;
using PdfReader.Rendering.Image.Jpg.Huffman;
using PdfReader.Rendering.Image.Jpg.Idct;
using PdfReader.Rendering.Image.Jpg.Model;
using PdfReader.Rendering.Image.Jpg.Quantization;
using PdfReader.Rendering.Image.Jpg.Readers;
using PdfReader.Rendering.Image.Jpg.Color;
using PdfReader.Streams;

namespace PdfReader.Rendering.Image.Jpg.Decoding
{
    /// <summary>
    /// Progressive JPEG decoder presented as a forward-only stream that yields interleaved component bytes
    /// (Gray=1, RGB=3, CMYK=4) one MCU-row band at a time. All progressive scans are first decoded into
    /// coefficient buffers (natural order) and IDCT is performed lazily while producing bands.
    /// </summary>
    internal sealed class JpgProgressiveStream : ContentStream
    {
        private const int DctBlockSize = 64;

        private readonly JpgHeader _header;
        private readonly ReadOnlyMemory<byte> _entropyMemory;

        // Maximum sampling factors across components (MCU geometry)
        private readonly int _hMax;
        private readonly int _vMax;

        public int McuWidth { get; }
        public int McuHeight { get; }

        private bool _decoderInitialized;

        /// <summary>
        /// Coefficient buffers for a single component (entire image) in natural order per block.
        /// </summary>
        private struct CoeffBuffers
        {
            public int BlocksX;            // Number of horizontal blocks for the component
            public int BlocksY;            // Number of vertical blocks for the component
            public int[] Coeffs;           // Length = BlocksX * BlocksY * 64 (natural order blocks)
        }

        private CoeffBuffers[] _coeffBuffers;
        private JpgQuantizationManager _quantizationManager;
        private Idct.ScaledIdctPlan[] _scaledPlans;

        // Per-block temporary buffers
        private readonly int[] _blockNatural = new int[DctBlockSize];
        private readonly int[] _idctWorkspace = new int[DctBlockSize];

        // Per-component MCU tiles (byte samples after IDCT)
        private byte[][] _componentTiles;
        private int[] _tileWidths;
        private int[] _tileHeights;

        // Output band buffer for one MCU row
        private byte[] _bandBuffer;
        private int _mcuColumns;
        private int _mcuRows;
        private int _bandHeight;
        private int _bandProduced;
        private int _bandConsumed;

        private IMcuWriter _mcuWriter;
        private long _outputBytePosition;

        public int Width => _header.Width;
        public int Height => _header.Height;
        public int CurrentMcuRow { get; private set; }

        /// <summary>
        /// Output stride = Width * componentCount.
        /// </summary>
        public int OutputStride => checked(Width * _header.ComponentCount);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException("Length is not supported.");

        public override long Position
        {
            get { return _outputBytePosition; }
            set { throw new NotSupportedException("Seeking is not supported."); }
        }

        public JpgProgressiveStream(JpgHeader header, ReadOnlyMemory<byte> entropyData)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }
            if (!header.IsProgressive)
            {
                throw new NotSupportedException("JpgProgressiveStream supports progressive JPEG only.");
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

            McuWidth = 8 * _hMax;
            McuHeight = 8 * _vMax;

            _decoderInitialized = false;
            _outputBytePosition = 0;
            CurrentMcuRow = 0;
        }

        public override int Read(Span<byte> buffer)
        {
            if (buffer.Length == 0)
            {
                return 0;
            }

            EnsureDecoderInitialized();

            int totalCopied = 0;
            int writeOffset = 0;
            int remainingRequest = buffer.Length;

            while (remainingRequest > 0)
            {
                if (_bandBuffer == null || _bandConsumed >= _bandProduced)
                {
                    if (CurrentMcuRow >= _mcuRows)
                    {
                        break; // EOF
                    }
                    ProduceNextBand();
                }

                int available = _bandProduced - _bandConsumed;
                int toCopy = available < remainingRequest ? available : remainingRequest;
                _bandBuffer.AsSpan(_bandConsumed, toCopy).CopyTo(buffer.Slice(writeOffset, toCopy));

                _bandConsumed += toCopy;
                writeOffset += toCopy;
                remainingRequest -= toCopy;
                totalCopied += toCopy;
                _outputBytePosition += toCopy;
            }

            return totalCopied;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException("Seek is not supported for JpgProgressiveStream.");
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

        /// <summary>
        /// Perform one-time decoding of all scans and allocate working buffers.
        /// </summary>
        private void EnsureDecoderInitialized()
        {
            if (_decoderInitialized)
            {
                return;
            }
            if (_header.SamplePrecision != 8)
            {
                throw new NotSupportedException("Only 8-bit progressive JPEG is supported.");
            }
            if (_header.Scans == null || _header.Scans.Count == 0)
            {
                throw new NotSupportedException("No progressive scans (SOS) found in header.");
            }

            _mcuColumns = (Width + McuWidth - 1) / McuWidth;
            _mcuRows = (Height + McuHeight - 1) / McuHeight;

            _coeffBuffers = InitializeCoefficientBuffers(_header, _mcuColumns, _mcuRows);
            ProcessProgressiveScans(_header, _entropyMemory.Span, _coeffBuffers, _mcuColumns, _mcuRows);

            _quantizationManager = JpgQuantizationManager.CreateFromHeader(_header);
            for (int componentIndex = 0; componentIndex < _header.Components.Count; componentIndex++)
            {
                int qid = _header.Components[componentIndex].QuantizationTableId;
                _quantizationManager.ValidateTableExists(qid, componentIndex);
            }

            int componentCount = _header.ComponentCount;
            _componentTiles = new byte[componentCount][];
            _tileWidths = new int[componentCount];
            _tileHeights = new int[componentCount];
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                var component = _header.Components[componentIndex];
                int tileWidth = 8 * component.HorizontalSamplingFactor;
                int tileHeight = 8 * component.VerticalSamplingFactor;
                _tileWidths[componentIndex] = tileWidth;
                _tileHeights[componentIndex] = tileHeight;
                _componentTiles[componentIndex] = new byte[tileWidth * tileHeight];
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
            for (int componentIndex = 0; componentIndex < _header.ComponentCount; componentIndex++)
            {
                int qid = _header.Components[componentIndex].QuantizationTableId;
                _scaledPlans[componentIndex] = _quantizationManager.CreateScaledIdctPlan(qid);
            }

            _decoderInitialized = true;
        }

        /// <summary>
        /// Build tiles for one MCU row and write them into the output band buffer.
        /// </summary>
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

            for (int mcuColumnIndex = 0; mcuColumnIndex < _mcuColumns; mcuColumnIndex++)
            {
                for (int componentIndex = 0; componentIndex < _header.ComponentCount; componentIndex++)
                {
                    var component = _header.Components[componentIndex];
                    int hFactor = component.HorizontalSamplingFactor;
                    int vFactor = component.VerticalSamplingFactor;
                    int tileWidth = _tileWidths[componentIndex];
                    byte[] tile = _componentTiles[componentIndex];
                    var buffer = _coeffBuffers[componentIndex];

                    for (int vBlock = 0; vBlock < vFactor; vBlock++)
                    {
                        int blockY = CurrentMcuRow * vFactor + vBlock;
                        if (blockY >= buffer.BlocksY)
                        {
                            continue;
                        }
                        for (int hBlock = 0; hBlock < hFactor; hBlock++)
                        {
                            int blockX = mcuColumnIndex * hFactor + hBlock;
                            if (blockX >= buffer.BlocksX)
                            {
                                continue;
                            }

                            int coeffBase = (blockY * buffer.BlocksX + blockX) * DctBlockSize;
                            for (int coefficientIndex = 0; coefficientIndex < DctBlockSize; coefficientIndex++)
                            {
                                _blockNatural[coefficientIndex] = buffer.Coeffs[coeffBase + coefficientIndex];
                            }

                            var plan = _scaledPlans[componentIndex];
                            int dstY0 = vBlock * 8;
                            int dstX0 = hBlock * 8;
                            int dstOffset = dstY0 * tileWidth + dstX0;
                            JpgIdct.TransformScaledNatural(_blockNatural, plan, tile.AsSpan(dstOffset), tileWidth, _idctWorkspace);
                        }
                    }
                }

                int xBase = mcuColumnIndex * McuWidth;
                _mcuWriter.WriteToBuffer(_bandBuffer, xBase, bandRows);
            }

            _bandHeight = bandRows;
            _bandProduced = bandRows * OutputStride;
            _bandConsumed = 0;
            CurrentMcuRow++;
        }

        /// <summary>
        /// Allocate coefficient buffers for each component based on MCU geometry and sampling factors.
        /// </summary>
        private static CoeffBuffers[] InitializeCoefficientBuffers(JpgHeader header, int mcuColumns, int mcuRows)
        {
            int componentCount = header.ComponentCount;
            var buffers = new CoeffBuffers[componentCount];
            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                var component = header.Components[componentIndex];
                int blocksX = mcuColumns * component.HorizontalSamplingFactor;
                int blocksY = mcuRows * component.VerticalSamplingFactor;
                buffers[componentIndex].BlocksX = blocksX;
                buffers[componentIndex].BlocksY = blocksY;
                buffers[componentIndex].Coeffs = new int[blocksX * blocksY * DctBlockSize];
            }
            return buffers;
        }

        /// <summary>
        /// Decode all progressive scans into coefficient buffers.
        /// </summary>
        private static void ProcessProgressiveScans(
            JpgHeader header,
            ReadOnlySpan<byte> content,
            CoeffBuffers[] coeffBuffers,
            int mcuColumns,
            int mcuRows)
        {
            var huffTables = new List<JpgHuffmanTable>(header.HuffmanTables);
            var quantTables = new List<JpgQuantizationTable>(header.QuantizationTables);
            int restartInterval = header.RestartInterval;

            ReadOnlySpan<byte> remainingStream = content;
            var bitReader = new JpgBitReader(remainingStream);
            JpgScanSpec currentScan = header.Scans.Count > 0 ? header.Scans[0] : null;
            int[] previousDc = new int[header.ComponentCount];
            int eobRun = 0;

            ProcessCurrentScan(header, coeffBuffers, huffTables, restartInterval, ref bitReader, currentScan, previousDc, ref eobRun, mcuColumns, mcuRows);

            while (true)
            {
                bitReader.ByteAlign();
                if (!bitReader.TryReadMarker(out byte marker))
                {
                    break; // No more markers
                }
                if (marker == 0xD9) // EOI
                {
                    break;
                }

                int pos = bitReader.Position / 8;
                if (pos + 2 > remainingStream.Length)
                {
                    throw new InvalidOperationException("Truncated marker segment length.");
                }

                ushort segmentLength = (ushort)(remainingStream[pos] << 8 | remainingStream[pos + 1]);
                int payloadLength = segmentLength - 2;
                pos += 2;
                if (payloadLength < 0 || pos + payloadLength > remainingStream.Length)
                {
                    throw new InvalidOperationException("Invalid marker segment length.");
                }

                var payload = remainingStream.Slice(pos, payloadLength);
                int nextPos = pos + payloadLength;
                remainingStream = remainingStream.Slice(nextPos);
                bitReader = new JpgBitReader(remainingStream);

                switch (marker)
                {
                    case 0xDB: // DQT
                        var newQuantTables = JpgQuantizationTable.ParseDqtPayload(payload);
                        quantTables.AddRange(newQuantTables);
                        break;
                    case 0xC4: // DHT
                        var newHuffTables = JpgHuffmanTable.ParseDhtPayload(payload);
                        huffTables.AddRange(newHuffTables);
                        break;
                    case 0xDD: // DRI
                        if (payloadLength >= 2)
                        {
                            restartInterval = payload[0] << 8 | payload[1];
                        }
                        break;
                    case 0xDA: // SOS
                        currentScan = JpgReader.ParseSos(payload);
                        ProcessCurrentScan(header, coeffBuffers, huffTables, restartInterval, ref bitReader, currentScan, previousDc, ref eobRun, mcuColumns, mcuRows);
                        break;
                    default:
                        // Non-coding marker ignored.
                        break;
                }
            }
        }

        /// <summary>
        /// Decode a single scan (interleaved or non-interleaved) into coefficient buffers.
        /// </summary>
        private static void ProcessCurrentScan(
            JpgHeader header,
            CoeffBuffers[] coeffBuffers,
            List<JpgHuffmanTable> huffTables,
            int restartInterval,
            ref JpgBitReader bitReader,
            JpgScanSpec currentScan,
            int[] previousDc,
            ref int eobRun,
            int mcuColumns,
            int mcuRows)
        {
            if (currentScan == null)
            {
                throw new InvalidOperationException("Current scan is null.");
            }

            bool isDcScan = currentScan.SpectralStart == 0 && currentScan.SpectralEnd == 0;
            bool firstPass = currentScan.SuccessiveApproxHigh == 0;
            int successiveApproxLow = currentScan.SuccessiveApproxLow;
            int successiveApproxHigh = currentScan.SuccessiveApproxHigh;

            int scanComponentCount = currentScan.Components.Count;
            int[] scanToComponent = JpgComponentMapper.MapScanToSofIndices(header, currentScan);
            if (scanToComponent == null)
            {
                throw new InvalidOperationException("Failed to map scan components to SOF indices.");
            }

            var dcDecoders = new JpgHuffmanDecoder[scanComponentCount];
            var acDecoders = new JpgHuffmanDecoder[scanComponentCount];
            for (int scanComponentIndex = 0; scanComponentIndex < scanComponentCount; scanComponentIndex++)
            {
                dcDecoders[scanComponentIndex] = GetDcDecoder(huffTables, currentScan.Components[scanComponentIndex].DcTableId);
                acDecoders[scanComponentIndex] = GetAcDecoder(huffTables, currentScan.Components[scanComponentIndex].AcTableId);
                if ((isDcScan && dcDecoders[scanComponentIndex] == null) || (!isDcScan && acDecoders[scanComponentIndex] == null))
                {
                    throw new InvalidOperationException($"Missing required Huffman table for scan component {scanComponentIndex}.");
                }
            }

            var restartManager = new JpgRestartManager(restartInterval);
            for (int componentIndex = 0; componentIndex < header.ComponentCount; componentIndex++)
            {
                previousDc[componentIndex] = 0;
            }
            eobRun = 0;

            if (scanComponentCount > 1)
            {
                // Interleaved scan
                for (int mcuRow = 0; mcuRow < mcuRows; mcuRow++)
                {
                    for (int mcuColumn = 0; mcuColumn < mcuColumns; mcuColumn++)
                    {
                        if (restartManager.IsRestartNeeded)
                        {
                            restartManager.ProcessRestart(ref bitReader, previousDc);
                            eobRun = 0;
                        }

                        for (int scanComponentIndex = 0; scanComponentIndex < scanComponentCount; scanComponentIndex++)
                        {
                            int componentIndex = scanToComponent[scanComponentIndex];
                            int hFactor = header.Components[componentIndex].HorizontalSamplingFactor;
                            int vFactor = header.Components[componentIndex].VerticalSamplingFactor;
                            var dcDecoder = dcDecoders[scanComponentIndex];
                            var acDecoder = acDecoders[scanComponentIndex];

                            for (int vBlock = 0; vBlock < vFactor; vBlock++)
                            {
                                for (int hBlock = 0; hBlock < hFactor; hBlock++)
                                {
                                    int blockX = mcuColumn * hFactor + hBlock;
                                    int blockY = mcuRow * vFactor + vBlock;
                                    var coeffBuffer = coeffBuffers[componentIndex];
                                    if (blockX >= coeffBuffer.BlocksX || blockY >= coeffBuffer.BlocksY)
                                    {
                                        continue;
                                    }

                                    int blockIndex = (blockY * coeffBuffer.BlocksX + blockX) * DctBlockSize;
                                    if (isDcScan)
                                    {
                                        JpgProgressiveBlockDecoder.DecodeDcCoefficient(
                                            ref bitReader,
                                            dcDecoder,
                                            ref previousDc[componentIndex],
                                            coeffBuffer.Coeffs,
                                            blockIndex,
                                            firstPass,
                                            successiveApproxLow);
                                    }
                                    else
                                    {
                                        if (firstPass)
                                        {
                                            JpgProgressiveBlockDecoder.DecodeAcCoefficientsFirstPass(
                                                ref bitReader,
                                                acDecoder,
                                                coeffBuffer.Coeffs,
                                                blockIndex,
                                                currentScan.SpectralStart,
                                                currentScan.SpectralEnd,
                                                successiveApproxLow,
                                                ref eobRun);
                                        }
                                        else
                                        {
                                            JpgProgressiveBlockDecoder.DecodeAcCoefficientsRefinement(
                                                ref bitReader,
                                                acDecoder,
                                                coeffBuffer.Coeffs,
                                                blockIndex,
                                                currentScan.SpectralStart,
                                                currentScan.SpectralEnd,
                                                successiveApproxHigh,
                                                successiveApproxLow,
                                                ref eobRun);
                                        }
                                    }
                                }
                            }
                        }
                        restartManager.DecrementRestartCounter();
                    }
                }
            }
            else
            {
                // Non-interleaved scan
                int componentIndex = scanToComponent[0];
                var dcDecoder = dcDecoders[0];
                var acDecoder = acDecoders[0];
                int blocksX = coeffBuffers[componentIndex].BlocksX;
                int blocksY = coeffBuffers[componentIndex].BlocksY;

                for (int blockRow = 0; blockRow < blocksY; blockRow++)
                {
                    for (int blockColumn = 0; blockColumn < blocksX; blockColumn++)
                    {
                        if (restartManager.IsRestartNeeded)
                        {
                            restartManager.ProcessRestart(ref bitReader, previousDc);
                            eobRun = 0;
                        }

                        int blockIndex = (blockRow * blocksX + blockColumn) * DctBlockSize;
                        if (isDcScan)
                        {
                            JpgProgressiveBlockDecoder.DecodeDcCoefficient(
                                ref bitReader,
                                dcDecoder,
                                ref previousDc[componentIndex],
                                coeffBuffers[componentIndex].Coeffs,
                                blockIndex,
                                firstPass,
                                successiveApproxLow);
                        }
                        else
                        {
                            if (firstPass)
                            {
                                JpgProgressiveBlockDecoder.DecodeAcCoefficientsFirstPass(
                                    ref bitReader,
                                    acDecoder,
                                    coeffBuffers[componentIndex].Coeffs,
                                    blockIndex,
                                    currentScan.SpectralStart,
                                    currentScan.SpectralEnd,
                                    successiveApproxLow,
                                    ref eobRun);
                            }
                            else
                            {
                                JpgProgressiveBlockDecoder.DecodeAcCoefficientsRefinement(
                                    ref bitReader,
                                    acDecoder,
                                    coeffBuffers[componentIndex].Coeffs,
                                    blockIndex,
                                    currentScan.SpectralStart,
                                    currentScan.SpectralEnd,
                                    successiveApproxHigh,
                                    successiveApproxLow,
                                    ref eobRun);
                            }
                        }
                        restartManager.DecrementRestartCounter();
                    }
                }
            }
        }

        private static JpgHuffmanDecoder GetDcDecoder(List<JpgHuffmanTable> huffTables, int tableId)
        {
            var table = FindHuffTable(huffTables, 0, tableId);
            return table != null ? new JpgHuffmanDecoder(table) : null;
        }

        private static JpgHuffmanDecoder GetAcDecoder(List<JpgHuffmanTable> huffTables, int tableId)
        {
            var table = FindHuffTable(huffTables, 1, tableId);
            return table != null ? new JpgHuffmanDecoder(table) : null;
        }

        private static JpgHuffmanTable FindHuffTable(List<JpgHuffmanTable> tables, int tableClass, int id)
        {
            for (int index = tables.Count - 1; index >= 0; index--)
            {
                if (tables[index].TableClass == tableClass && tables[index].TableId == id)
                {
                    return tables[index];
                }
            }
            return null;
        }
    }
}
