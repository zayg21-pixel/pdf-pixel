using System;
using System.Collections.Generic;
using PdfRender.Imaging.Jpg.Color;
using PdfRender.Imaging.Jpg.Huffman;
using PdfRender.Imaging.Jpg.Idct;
using PdfRender.Imaging.Jpg.Model;
using PdfRender.Imaging.Jpg.Readers;

namespace PdfRender.Imaging.Jpg.Decoding;

/// <summary>
/// Progressive JPEG decoder producing interleaved component rows. Coefficients are stored (and refined) across scans.
/// After all scans have been processed the final coefficient buffers are de-quantized and inverse transformed band-by-band
/// using the same infrastructure as the baseline decoder (color conversion, optional upsampling, band packing).
/// </summary>
internal sealed class JpgProgressiveDecoder : IJpgDecoder
{
    private const int DctBlockSize = 64;

    private readonly JpgHeader _header;
    private readonly ReadOnlyMemory<byte> _entropyMemory;

    private bool _decoderInitialized;

    private struct CoeffBuffers
    {
        public int BlocksX;
        public int BlocksY;
        public int[] Coeffs; // Per-block coefficients (natural order expected by IDCT path)
    }

    private CoeffBuffers[] _coeffBuffers;
    private JpgQuantizationManager _quantizationManager;
    private Block8x8F[] _dequantizationBlocks;

    // New architecture fields (mirroring baseline decoder approach)
    private JpgDecodingParameters _decodingParameters;
    private JpgUpsampler _upsampler;
    private IJpgColorConverter _colorConverter;
    private JpgBandPacker _bandPacker;

    private Block8x8F[][] _componentBandBlocks;
    private Block8x8F[][] _upsampledBandBlocks;

    private byte[] _bandBuffer;
    private int _bandProduced;
    private int _bandConsumed;
    private int _bandHeight;

    private int _currentMcuRow;
    private int _currentRow;

    private Block8x8F _scratchBlock; // reused temporary block when emitting blocks

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

        // Compute sizing / sampling invariants once.
        _decodingParameters = new JpgDecodingParameters(_header);

        // Decode all scans to final coefficient buffers before on-demand band production.
        _coeffBuffers = InitializeCoefficientBuffers(_header, _decodingParameters.McuColumns, _decodingParameters.McuRows);
        ProcessProgressiveScans(
            _header,
            _entropyMemory.Span,
            _coeffBuffers,
            _decodingParameters.McuColumns,
            _decodingParameters.McuRows);

        // Quant tables & dequant blocks.
        _quantizationManager = JpgQuantizationManager.CreateFromHeader(_header);
        for (int componentIndex = 0; componentIndex < _header.ComponentCount; componentIndex++)
        {
            int qid = _header.Components[componentIndex].QuantizationTableId;
            _quantizationManager.ValidateTableExists(qid, componentIndex);
        }
        _dequantizationBlocks = new Block8x8F[_header.ComponentCount];
        for (int componentIndex = 0; componentIndex < _header.ComponentCount; componentIndex++)
        {
            int qid = _header.Components[componentIndex].QuantizationTableId;
            _dequantizationBlocks[componentIndex] = _quantizationManager.CreateNaturalBlock(qid);
        }

        // Allocate band block arrays per component (one MCU row worth at a time like baseline decoder).
        _componentBandBlocks = new Block8x8F[_header.ComponentCount][];
        if (_decodingParameters.NeedsUpsampling)
        {
            _upsampledBandBlocks = new Block8x8F[_header.ComponentCount][];
        }
        for (int componentIndex = 0; componentIndex < _header.ComponentCount; componentIndex++)
        {
            int totalBlocksForBand = _decodingParameters.TotalBlocksPerBand[componentIndex];
            _componentBandBlocks[componentIndex] = new Block8x8F[totalBlocksForBand];
            if (_decodingParameters.NeedsUpsampling)
            {
                _upsampledBandBlocks[componentIndex] = new Block8x8F[_decodingParameters.McuColumns * _decodingParameters.UpsampledBlocksPerMcu];
            }
        }

        // Color conversion & band packing infrastructure.
        _upsampler = new JpgUpsampler(_decodingParameters, _header);
        _colorConverter = JpgColorConverterFactory.Create(_header, _decodingParameters);
        _bandPacker = new JpgBandPacker(_header, _decodingParameters);

        _bandHeight = _decodingParameters.McuHeight;
        _bandBuffer = new byte[_bandHeight * _decodingParameters.OutputStride];
        _bandProduced = 0;
        _bandConsumed = 0;

        _decoderInitialized = true;
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
        if (rowBuffer.Length < _decodingParameters.OutputStride)
        {
            throw new ArgumentException("Row buffer too small for decoded row.", nameof(rowBuffer));
        }
        if (_bandBuffer == null || _bandConsumed >= _bandProduced)
        {
            if (_currentMcuRow >= _decodingParameters.McuRows)
            {
                return false;
            }
            ProduceNextBand();
            if (_bandProduced == 0)
            {
                return false;
            }
        }
        _bandBuffer.AsSpan(_bandConsumed, _decodingParameters.OutputStride).CopyTo(rowBuffer);
        _bandConsumed += _decodingParameters.OutputStride;
        _currentRow++;
        return true;
    }

    private void ProduceNextBand()
    {
        int yBase = _currentMcuRow * _decodingParameters.McuHeight;
        int remainingRows = _header.Height - yBase;
        int bandRows = remainingRows < _decodingParameters.McuHeight ? remainingRows : _decodingParameters.McuHeight;
        if (bandRows <= 0)
        {
            _bandProduced = 0;
            _bandConsumed = 0;
            return;
        }

        // Decode (inverse transform) one MCU row of blocks for each component into componentBandBlocks.
        for (int mcuColumnIndex = 0; mcuColumnIndex < _decodingParameters.McuColumns; mcuColumnIndex++)
        {
            for (int componentIndex = 0; componentIndex < _header.ComponentCount; componentIndex++)
            {
                var component = _header.Components[componentIndex];
                int hFactor = component.HorizontalSamplingFactor;
                int vFactor = component.VerticalSamplingFactor;
                int blocksPerMcu = _decodingParameters.BlocksPerMcu[componentIndex];
                Block8x8F[] bandBlocks = _componentBandBlocks[componentIndex];
                var coeffBuffer = _coeffBuffers[componentIndex];

                for (int vBlock = 0; vBlock < vFactor; vBlock++)
                {
                    int blockY = _currentMcuRow * vFactor + vBlock;
                    if (blockY >= coeffBuffer.BlocksY)
                    {
                        continue;
                    }
                    for (int hBlock = 0; hBlock < hFactor; hBlock++)
                    {
                        int blockX = mcuColumnIndex * hFactor + hBlock;
                        if (blockX >= coeffBuffer.BlocksX)
                        {
                            continue;
                        }

                        int coeffBase = (blockY * coeffBuffer.BlocksX + blockX) * DctBlockSize;
                        bool dcOnly = true;
                        // Populate scratch block with (natural-order) coefficients.
                        for (int coefficientIndex = 0; coefficientIndex < DctBlockSize; coefficientIndex++)
                        {
                            int coefficient = coeffBuffer.Coeffs[coeffBase + coefficientIndex];
                            _scratchBlock[coefficientIndex] = coefficient;
                            if (coefficientIndex != 0 && coefficient != 0)
                            {
                                dcOnly = false;
                            }
                        }

                        ref Block8x8F dequantBlock = ref _dequantizationBlocks[componentIndex];
                        IdctTransform.TransformScaledNatural(ref _scratchBlock, ref dequantBlock, dcOnly);
                        int localBlockIndex = vBlock * hFactor + hBlock;
                        int globalBlockIndex = mcuColumnIndex * blocksPerMcu + localBlockIndex;
                        bandBlocks[globalBlockIndex] = _scratchBlock;
                    }
                }
            }
        }

        // Optional upsampling + color conversion + band packing.
        Block8x8F[][] workingBlocks = _decodingParameters.NeedsUpsampling ? _upsampledBandBlocks : _componentBandBlocks;
        if (_decodingParameters.NeedsUpsampling)
        {
            _upsampler.UpsampleBand(_componentBandBlocks, _upsampledBandBlocks);
        }
        _colorConverter.ConvertInPlace(workingBlocks);
        _bandPacker.Pack(workingBlocks, bandRows, _bandBuffer);

        _bandHeight = bandRows;
        _bandProduced = bandRows * _decodingParameters.OutputStride;
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

        var bitReader = new JpgBitReader(content);
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
            if (marker == 0xD9) // EOI
            {
                break;
            }

            bool hasPayload = marker != 0xD8 && marker != 0xD9 && marker != 0x01 && (marker < 0xD0 || marker > 0xD7);
            if (!hasPayload)
            {
                continue;
            }

            ReadOnlySpan<byte> payload = bitReader.ReadSegmentPayload();

            switch (marker)
            {
                case 0xDB: // DQT
                {
                    var newQuantTables = JpgQuantizationTable.ParseDqtPayload(payload);
                    quantTables.AddRange(newQuantTables);
                    break;
                }
                case 0xC4: // DHT
                {
                    var newHuffTables = JpgHuffmanTable.ParseDhtPayload(payload);
                    huffTables.AddRange(newHuffTables);
                    break;
                }
                case 0xDD: // DRI
                {
                    if (payload.Length >= 2)
                    {
                        restartInterval = payload[0] << 8 | payload[1];
                    }
                    break;
                }
                case 0xDA: // SOS
                {
                    currentScan = JpgReader.ParseSos(payload);
                    ProcessCurrentScan(header, coeffBuffers, huffTables, restartInterval, ref bitReader, currentScan, previousDc, ref eobRun, mcuColumns, mcuRows);
                    break;
                }
                default:
                {
                    // Other marker payload ignored (APPn, COM, etc.).
                    break;
                }
            }
        }
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
            if (isDcScan && dcDecoders[scanComponentIndex] == null || !isDcScan && acDecoders[scanComponentIndex] == null)
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
                                    JpgProgressiveBlockDecoder.DecodeDcCoefficient(ref bitReader, dcDecoder, ref previousDc[componentIndex], coeffBuffer.Coeffs, blockIndex, firstPass, successiveApproxLow);
                                }
                                else
                                {
                                    if (firstPass)
                                    {
                                        JpgProgressiveBlockDecoder.DecodeAcCoefficientsFirstPass(ref bitReader, acDecoder, coeffBuffer.Coeffs, blockIndex, currentScan.SpectralStart, currentScan.SpectralEnd, successiveApproxLow, ref eobRun);
                                    }
                                    else
                                    {
                                        JpgProgressiveBlockDecoder.DecodeAcCoefficientsRefinement(ref bitReader, acDecoder, coeffBuffer.Coeffs, blockIndex, currentScan.SpectralStart, currentScan.SpectralEnd, successiveApproxHigh, successiveApproxLow, ref eobRun);
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
                        JpgProgressiveBlockDecoder.DecodeDcCoefficient(ref bitReader, dcDecoder, ref previousDc[componentIndex], coeffBuffers[componentIndex].Coeffs, blockIndex, firstPass, successiveApproxLow);
                    }
                    else
                    {
                        if (firstPass)
                        {
                            JpgProgressiveBlockDecoder.DecodeAcCoefficientsFirstPass(ref bitReader, acDecoder, coeffBuffers[componentIndex].Coeffs, blockIndex, currentScan.SpectralStart, currentScan.SpectralEnd, successiveApproxLow, ref eobRun);
                        }
                        else
                        {
                            JpgProgressiveBlockDecoder.DecodeAcCoefficientsRefinement(ref bitReader, acDecoder, coeffBuffers[componentIndex].Coeffs, blockIndex, currentScan.SpectralStart, currentScan.SpectralEnd, successiveApproxHigh, successiveApproxLow, ref eobRun);
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
