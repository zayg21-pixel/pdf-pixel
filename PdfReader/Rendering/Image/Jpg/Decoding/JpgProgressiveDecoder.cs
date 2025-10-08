using System;
using System.Collections.Generic;
using PdfReader.Rendering.Image.Jpg.Huffman;
using PdfReader.Rendering.Image.Jpg.Idct;
using PdfReader.Rendering.Image.Jpg.Model;
using PdfReader.Rendering.Image.Jpg.Quantization;
using PdfReader.Rendering.Image.Jpg.Readers;
using PdfReader.Rendering.Image.Jpg.Color;

namespace PdfReader.Rendering.Image.Jpg.Decoding
{
    public interface IJpgDecoder
    {
        /// <summary>
        /// Read the next full image row of interleaved component samples into <paramref name="rowBuffer"/>.
        /// Returns false when no more rows are available.
        /// </summary>
        /// <param name="rowBuffer">Destination buffer. Length must be at least (Width * ComponentCount) as determined by the owning decoder logic.</param>
        /// <returns>True if a row was written; false on end of image.</returns>
        bool TryReadRow(Span<byte> rowBuffer);

        /// <summary>
        /// Zero-based index of the next row to be produced.
        /// </summary>
        int CurrentRow { get; }
    }

    /// <summary>
    /// Progressive JPEG decoder that yields interleaved component bytes (Gray=1, RGB=3, CMYK=4) one row at a time.
    /// All progressive scans are fully decoded into coefficient buffers (natural order) and IDCT is performed lazily
    /// while producing MCU-row bands.
    /// </summary>
    internal sealed class JpgProgressiveDecoder : IJpgDecoder
    {
        private const int DctBlockSize = 64;

        private readonly JpgHeader _header;
        private readonly ReadOnlyMemory<byte> _entropyMemory;

        private readonly int _hMax;
        private readonly int _vMax;

        private readonly int _mcuWidth;
        private readonly int _mcuHeight;

        private bool _decoderInitialized;

        private struct CoeffBuffers
        {
            public int BlocksX;
            public int BlocksY;
            public int[] Coeffs;
        }

        private CoeffBuffers[] _coeffBuffers;
        private JpgQuantizationManager _quantizationManager;
        private Idct.ScaledIdctPlan[] _scaledPlans;

        private readonly int[] _blockNatural = new int[DctBlockSize];
        private readonly int[] _idctWorkspace = new int[DctBlockSize];

        private byte[][] _componentTiles;
        private int[] _tileWidths;
        private int[] _tileHeights;

        private byte[] _bandBuffer;
        private int _mcuColumns;
        private int _mcuRows;
        private int _bandHeight;
        private int _bandProduced;
        private int _bandConsumed;

        private IMcuWriter _mcuWriter;

        private int _currentMcuRow;
        private int _currentRow;
        private int _outputStride;

        public int CurrentRow => _currentRow;

        public JpgProgressiveDecoder(JpgHeader header, ReadOnlyMemory<byte> entropyData)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }
            if (!header.IsProgressive)
            {
                throw new NotSupportedException("JpgProgressiveDecoder supports progressive JPEG only.");
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

            _decoderInitialized = false;
            _currentMcuRow = 0;
            _currentRow = 0;
        }

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

            _mcuColumns = (_header.Width + _mcuWidth - 1) / _mcuWidth;
            _mcuRows = (_header.Height + _mcuHeight - 1) / _mcuHeight;

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
            for (int componentIndex = 0; componentIndex < _header.ComponentCount; componentIndex++)
            {
                int qid = _header.Components[componentIndex].QuantizationTableId;
                _scaledPlans[componentIndex] = _quantizationManager.CreateScaledIdctPlan(qid);
            }

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
                        int blockY = _currentMcuRow * vFactor + vBlock;
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
                            bool dcOnly = true;
                            int dc = buffer.Coeffs[coeffBase];
                            _blockNatural[0] = dc;
                            for (int coefficientIndex = 1; coefficientIndex < DctBlockSize; coefficientIndex++)
                            {
                                int val = buffer.Coeffs[coeffBase + coefficientIndex];
                                _blockNatural[coefficientIndex] = val;
                                if (val != 0)
                                {
                                    dcOnly = false;
                                }
                            }

                            var plan = _scaledPlans[componentIndex];
                            int dstY0 = vBlock * 8;
                            int dstX0 = hBlock * 8;
                            int dstOffset = dstY0 * tileWidth + dstX0;
                            JpgIdct.TransformScaledNatural(_blockNatural, plan, tile.AsSpan(dstOffset), tileWidth, _idctWorkspace, dcOnly);
                        }
                    }
                }

                int xBase = mcuColumnIndex * _mcuWidth;
                _mcuWriter.WriteToBuffer(_bandBuffer, xBase, bandRows);
            }

            _bandHeight = bandRows;
            _bandProduced = bandRows * _outputStride;
            _bandConsumed = 0;
            _currentMcuRow++;
        }

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
                    break;
                }
                if (marker == 0xD9)
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
                    case 0xDB:
                        var newQuantTables = JpgQuantizationTable.ParseDqtPayload(payload);
                        quantTables.AddRange(newQuantTables);
                        break;
                    case 0xC4:
                        var newHuffTables = JpgHuffmanTable.ParseDhtPayload(payload);
                        huffTables.AddRange(newHuffTables);
                        break;
                    case 0xDD:
                        if (payloadLength >= 2)
                        {
                            restartInterval = payload[0] << 8 | payload[1];
                        }
                        break;
                    case 0xDA:
                        currentScan = JpgReader.ParseSos(payload);
                        ProcessCurrentScan(header, coeffBuffers, huffTables, restartInterval, ref bitReader, currentScan, previousDc, ref eobRun, mcuColumns, mcuRows);
                        break;
                    default:
                        break;
                }
            }
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
