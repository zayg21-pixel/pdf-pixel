using System;
using System.Collections.Generic;

using PdfPixel.Jpg.Color;
using PdfPixel.Jpg.Huffman;
using PdfPixel.Jpg.Idct;
using PdfPixel.Jpg.Model;
using PdfPixel.Jpg.Readers;

namespace PdfPixel.Jpg.Decoding;

/// <summary>
/// Progressive JPEG decoder producing interleaved component rows. Coefficients are stored (and refined) across scans.
/// After all scans have been processed the final coefficient buffers are de-quantized and inverse transformed band-by-band
/// using the same infrastructure as the baseline decoder (color conversion, optional upsampling, band packing).
/// </summary>
public sealed class JpgProgressiveDecoder : IJpgDecoder
{
    private const int DctBlockSize = 64;
    private const int DctBlockEdge = 8;

    private readonly JpgHeader _header;
    private readonly ReadOnlyMemory<byte> _entropyMemory;

    private struct CoeffBuffers
    {
        public int BlocksX;
        public int BlocksY;
        public short[] Coeffs; // Per-block coefficients (natural order expected by IDCT path)
    }

    private readonly CoeffBuffers[] _coeffBuffers;
    private readonly JpgQuantizationManager _quantizationManager;
    private readonly Block8x8F[] _dequantizationBlocks;
    private readonly JpgDecodingParameters _decodingParameters;
    private readonly JpgUpsampler? _upsampler;
    private readonly IJpgColorConverter _colorConverter;
    private readonly JpgBandPacker _bandPacker;
    private readonly Block8x8F[][] _componentBandBlocks;
    private readonly Block8x8F[][] _upsampledBandBlocks;
    private readonly Block8x8F[][] _workingBandBlocks;

    private int _bandRows;
    private int _bandRowIndex;
    private int _currentMcuRow;
    private int _currentRow;
    private bool _bandReconstructed;
    private Block8x8F _scratchBlock;

    /// <summary>
    /// Initializes a new <see cref="JpgProgressiveDecoder"/>, processes all progressive scans eagerly, and prepares for row-by-row output.
    /// </summary>
    /// <param name="header">Parsed JPEG header from <see cref="Readers.JpgReader.ParseHeader"/>.</param>
    /// <param name="entropyData">Full entropy-coded image data starting at the offset recorded in the header.</param>
    /// <param name="options">Optional decoding overrides; uses <see cref="JpgDecoderOptions.Default"/> when null.</param>
    public JpgProgressiveDecoder(JpgHeader header, in ReadOnlyMemory<byte> entropyData, JpgDecoderOptions? options = null)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        if (header.FrameType != JpgFrameType.ProgressiveDct)
        {
            throw new NotSupportedException($"JpgProgressiveDecoder supports progressive JPEG (SOF2) only. Got {header.FrameType.ToString()}.");
        }

        if (header.ComponentCount <= 0 || header.Components == null || header.Components.Count != header.ComponentCount)
        {
            throw new ArgumentException("Invalid header components.", nameof(header));
        }

        if (header.SamplePrecision != 8)
        {
            throw new NotSupportedException("Only 8-bit progressive JPEG is supported.");
        }

        if (header.Scans == null || header.Scans.Count == 0)
        {
            throw new NotSupportedException("No progressive scans (SOS) found in header.");
        }

        options ??= JpgDecoderOptions.Default;

        _header = header;
        _entropyMemory = entropyData;
        _decodingParameters = new JpgDecodingParameters(header, options.DescaleFactor, options.RegionsOfInterest);

        _coeffBuffers = InitializeCoefficientBuffers(_header, _decodingParameters);
        ProcessProgressiveScans(
            _header,
            _entropyMemory.Span,
            _coeffBuffers,
            BuildSpectralLimits(_header, _decodingParameters),
            _decodingParameters.McuColumns,
            _decodingParameters.McuRows);

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

        _componentBandBlocks = new Block8x8F[_header.ComponentCount][];
        _upsampledBandBlocks = (_decodingParameters.NeedsUpsampling) ? new Block8x8F[_header.ComponentCount][] : Array.Empty<Block8x8F[]>();

        for (int componentIndex = 0; componentIndex < _header.ComponentCount; componentIndex++)
        {
            int totalBlocksForBand = _decodingParameters.TotalBlocksPerBand[componentIndex];
            _componentBandBlocks[componentIndex] = new Block8x8F[totalBlocksForBand];
            if (_decodingParameters.NeedsUpsampling)
            {
                _upsampledBandBlocks[componentIndex] = new Block8x8F[_decodingParameters.ReconstructedMcuColumns * _decodingParameters.UpsampledBlocksPerMcu];
            }
        }

        _workingBandBlocks = (_decodingParameters.NeedsUpsampling) ? _upsampledBandBlocks : _componentBandBlocks;

        _upsampler = (_decodingParameters.NeedsUpsampling) ? new JpgUpsampler(_decodingParameters, _header) : null;
        _colorConverter = JpgColorConverterFactory.Create(_header, _decodingParameters, options);
        _bandPacker = new JpgBandPacker(_header, _decodingParameters);
    }

    /// <inheritdoc/>
    public int CurrentRow => _currentRow;

    /// <inheritdoc/>
    public bool TryReadRow(Span<byte> rowBuffer)
    {
        if (rowBuffer.Length == 0)
        {
            return false;
        }

        if (_currentRow >= _decodingParameters.OutputHeight)
        {
            return false;
        }

        if (rowBuffer.Length < _decodingParameters.OutputStride)
        {
            throw new ArgumentException("Row buffer too small for decoded row.", nameof(rowBuffer));
        }

        if (_bandRowIndex >= _bandRows)
        {
            if (_currentMcuRow >= _decodingParameters.McuRows)
            {
                return false;
            }

            ProduceNextBand();
            if (_bandRows == 0)
            {
                return false;
            }
        }

        if (!_decodingParameters.IsFullWidthReconstructed || !_bandReconstructed)
        {
            rowBuffer.Slice(0, _decodingParameters.OutputStride).Clear();
        }

        if (_bandReconstructed)
        {
            _bandPacker.PackRow(_workingBandBlocks, _bandRowIndex, rowBuffer);
        }

        _bandRowIndex++;
        _currentRow++;
        return true;
    }

    private void ProduceNextBand()
    {
        int yBase = _currentMcuRow * _decodingParameters.OutputMcuHeight;
        int remainingRows = _decodingParameters.OutputHeight - yBase;
        int bandRows = (remainingRows < _decodingParameters.OutputMcuHeight) ? remainingRows : _decodingParameters.OutputMcuHeight;
        if (bandRows <= 0)
        {
            _bandRows = 0;
            _bandRowIndex = 0;
            return;
        }

        if (!_decodingParameters.IsMcuRowReconstructed(_currentMcuRow))
        {
            _bandRows = bandRows;
            _bandRowIndex = 0;
            _bandReconstructed = false;
            _currentMcuRow++;
            return;
        }

        for (int bandColumnIndex = 0; bandColumnIndex < _decodingParameters.ReconstructedMcuColumns; bandColumnIndex++)
        {
            int mcuColumnIndex = _decodingParameters.ReconstructedMcuColumnStart + bandColumnIndex;

            for (int componentIndex = 0; componentIndex < _header.ComponentCount; componentIndex++)
            {
                int hFactor = _decodingParameters.ComponentBlocksH[componentIndex];
                int vFactor = _decodingParameters.ComponentBlocksV[componentIndex];
                int blocksPerMcu = _decodingParameters.BlocksPerMcu[componentIndex];
                int idctWidth = _decodingParameters.ComponentIdctWidth[componentIndex];
                int idctHeight = _decodingParameters.ComponentIdctHeight[componentIndex];
                Block8x8F[] bandBlocks = _componentBandBlocks[componentIndex];
                CoeffBuffers coeffBuffer = _coeffBuffers[componentIndex];

                for (int vBlock = 0; vBlock < vFactor; vBlock++)
                {
                    int blockY = (_currentMcuRow * vFactor) + vBlock;
                    if (blockY >= coeffBuffer.BlocksY)
                    {
                        continue;
                    }

                    for (int hBlock = 0; hBlock < hFactor; hBlock++)
                    {
                        int blockX = (mcuColumnIndex * hFactor) + hBlock;
                        if (blockX >= coeffBuffer.BlocksX)
                        {
                            continue;
                        }

                        int coeffBase = ((blockY * coeffBuffer.BlocksX) + blockX) * DctBlockSize;
                        var dcOnly = true;
                        for (int coefficientRow = 0; coefficientRow < idctHeight; coefficientRow++)
                        {
                            int coefficientRowBase = coefficientRow * DctBlockEdge;
                            for (int coefficientColumn = 0; coefficientColumn < idctWidth; coefficientColumn++)
                            {
                                int coefficientIndex = coefficientRowBase + coefficientColumn;
                                int coefficient = coeffBuffer.Coeffs[coeffBase + coefficientIndex];
                                _scratchBlock[coefficientIndex] = coefficient;
                                if (coefficientIndex != 0 && coefficient != 0)
                                {
                                    dcOnly = false;
                                }
                            }
                        }

                        ref Block8x8F dequantBlock = ref _dequantizationBlocks[componentIndex];
                        IdctTransform.TransformScaledNatural(ref _scratchBlock, ref dequantBlock, dcOnly, idctWidth, idctHeight);
                        int localBlockIndex = (vBlock * hFactor) + hBlock;
                        int bandBlockIndex = (bandColumnIndex * blocksPerMcu) + localBlockIndex;
                        bandBlocks[bandBlockIndex] = _scratchBlock;
                    }
                }
            }
        }

        if (_decodingParameters.NeedsUpsampling && _upsampler != null)
        {
            _upsampler.UpsampleBand(_componentBandBlocks, _upsampledBandBlocks);
        }

        _colorConverter.ConvertInPlace(_workingBandBlocks);

        _bandRows = bandRows;
        _bandRowIndex = 0;
        _bandReconstructed = true;
        _currentMcuRow++;
    }

    private static CoeffBuffers[] InitializeCoefficientBuffers(JpgHeader header, JpgDecodingParameters parameters)
    {
        int componentCount = header.ComponentCount;
        var buffers = new CoeffBuffers[componentCount];
        for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
        {
            int blocksX = parameters.McuColumns * parameters.ComponentBlocksH[componentIndex];
            int blocksY = parameters.McuRows * parameters.ComponentBlocksV[componentIndex];
            buffers[componentIndex].BlocksX = blocksX;
            buffers[componentIndex].BlocksY = blocksY;
            buffers[componentIndex].Coeffs = new short[blocksX * blocksY * DctBlockSize];
        }

        return buffers;
    }

    private static void ProcessProgressiveScans(
        JpgHeader header,
        in ReadOnlySpan<byte> content,
        CoeffBuffers[] coeffBuffers,
        int[] spectralLimits,
        int mcuColumns,
        int mcuRows)
    {
        List<JpgHuffmanTable> huffTables = new(header.HuffmanTables);
        List<JpgQuantizationTable> quantTables = new(header.QuantizationTables);
        int restartInterval = header.RestartInterval;

        JpgBitReader bitReader = new(content);

        if (header.Scans.Count == 0)
        {
            throw new InvalidOperationException("No scans in available for progressive decoding.");
        }

        JpgScanSpec currentScan = header.Scans[0];
        var previousDc = new int[header.ComponentCount];
        int eobRun = 0;

        ProcessCurrentScan(header, coeffBuffers, huffTables, spectralLimits, restartInterval, ref bitReader, currentScan, previousDc, ref eobRun, mcuColumns, mcuRows);

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
                        List<JpgQuantizationTable> newQuantTables = JpgQuantizationTable.ParseDqtPayload(payload);
                    quantTables.AddRange(newQuantTables);
                    break;
                }
                case 0xC4: // DHT
                {
                        List<JpgHuffmanTable> newHuffTables = JpgHuffmanTable.ParseDhtPayload(payload);
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
                    ProcessCurrentScan(header, coeffBuffers, huffTables, spectralLimits, restartInterval, ref bitReader, currentScan, previousDc, ref eobRun, mcuColumns, mcuRows);
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
        int[] spectralLimits,
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
        int[] scanToComponent = JpgComponentMapper.MapScanToSofIndices(header, currentScan)
            ?? throw new InvalidOperationException("Failed to map scan components to SOF indices.");

        if (!IsScanNeeded(currentScan, scanToComponent, spectralLimits))
        {
            // Nothing this scan refines survives the reduced transform. Leaving its entropy data unread
            // costs nothing: the caller scans forward for the next marker either way.
            return;
        }

        var dcDecoders = new JpgHuffmanDecoder[scanComponentCount];
        var acDecoders = new JpgHuffmanDecoder[scanComponentCount];
        for (int scanComponentIndex = 0; scanComponentIndex < scanComponentCount; scanComponentIndex++)
        {
            if (isDcScan)
            {
                dcDecoders[scanComponentIndex] = GetDcDecoder(huffTables, currentScan.Components[scanComponentIndex].DcTableId);
            }
            else
            {
                acDecoders[scanComponentIndex] = GetAcDecoder(huffTables, currentScan.Components[scanComponentIndex].AcTableId);
            }
        }

        JpgRestartManager restartManager = new(restartInterval);
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
                        JpgHuffmanDecoder dcDecoder = dcDecoders[scanComponentIndex];
                        JpgHuffmanDecoder acDecoder = acDecoders[scanComponentIndex];

                        for (int vBlock = 0; vBlock < vFactor; vBlock++)
                        {
                            for (int hBlock = 0; hBlock < hFactor; hBlock++)
                            {
                                int blockX = (mcuColumn * hFactor) + hBlock;
                                int blockY = (mcuRow * vFactor) + vBlock;
                                CoeffBuffers coeffBuffer = coeffBuffers[componentIndex];
                                if (blockX >= coeffBuffer.BlocksX || blockY >= coeffBuffer.BlocksY)
                                {
                                    continue;
                                }

                                int blockIndex = ((blockY * coeffBuffer.BlocksX) + blockX) * DctBlockSize;
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
            int componentIndex = scanToComponent[0];
            JpgHuffmanDecoder dcDecoder = dcDecoders[0];
            JpgHuffmanDecoder acDecoder = acDecoders[0];
            int bufferBlocksX = coeffBuffers[componentIndex].BlocksX;

            int hSamp = header.Components[componentIndex].HorizontalSamplingFactor;
            int vSamp = header.Components[componentIndex].VerticalSamplingFactor;
            int hMax = 1;
            int vMax = 1;
            for (int ci = 0; ci < header.Components.Count; ci++)
            {
                if (header.Components[ci].HorizontalSamplingFactor > hMax)
                {
                    hMax = header.Components[ci].HorizontalSamplingFactor;
                }

                if (header.Components[ci].VerticalSamplingFactor > vMax)
                {
                    vMax = header.Components[ci].VerticalSamplingFactor;
                }
            }

            int scanBlocksX = ((header.Width * hSamp) + (hMax * 8) - 1) / (hMax * 8);
            int scanBlocksY = ((header.Height * vSamp) + (vMax * 8) - 1) / (vMax * 8);

            for (int blockRow = 0; blockRow < scanBlocksY; blockRow++)
            {
                for (int blockColumn = 0; blockColumn < scanBlocksX; blockColumn++)
                {
                    if (restartManager.IsRestartNeeded)
                    {
                        restartManager.ProcessRestart(ref bitReader, previousDc);
                        eobRun = 0;
                    }

                    int blockIndex = ((blockRow * bufferBlocksX) + blockColumn) * DctBlockSize;
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

    /// <summary>
    /// Highest spectral position each component still contributes to its reconstructed samples.
    /// </summary>
    /// <param name="header">Parsed JPEG header.</param>
    /// <param name="parameters">Decoding geometry holding the per-component transform sizes.</param>
    /// <returns>One spectral position per component.</returns>
    private static int[] BuildSpectralLimits(JpgHeader header, JpgDecodingParameters parameters)
    {
        byte[] zigZagToNatural = JpgZigZag.Table;
        var spectralLimits = new int[header.ComponentCount];

        for (int componentIndex = 0; componentIndex < header.ComponentCount; componentIndex++)
        {
            int idctWidth = parameters.ComponentIdctWidth[componentIndex];
            int idctHeight = parameters.ComponentIdctHeight[componentIndex];
            for (int spectralIndex = 0; spectralIndex < DctBlockSize; spectralIndex++)
            {
                int naturalIndex = zigZagToNatural[spectralIndex];
                if (naturalIndex % DctBlockEdge < idctWidth && naturalIndex / DctBlockEdge < idctHeight)
                {
                    spectralLimits[componentIndex] = spectralIndex;
                }
            }
        }

        return spectralLimits;
    }

    /// <summary>
    /// True when the scan carries coefficients at least one of its components still needs.
    /// </summary>
    /// <param name="scan">Scan about to be decoded.</param>
    /// <param name="scanToComponent">SOF component index per scan component.</param>
    /// <param name="spectralLimits">Highest spectral position needed per component.</param>
    private static bool IsScanNeeded(JpgScanSpec scan, int[] scanToComponent, int[] spectralLimits)
    {
        for (int scanComponentIndex = 0; scanComponentIndex < scanToComponent.Length; scanComponentIndex++)
        {
            if (scan.SpectralStart <= spectralLimits[scanToComponent[scanComponentIndex]])
            {
                return true;
            }
        }

        return false;
    }

    private static JpgHuffmanDecoder GetDcDecoder(List<JpgHuffmanTable> huffTables, int tableId)
    {
        JpgHuffmanTable table = FindHuffTable(huffTables, 0, tableId);
        return new JpgHuffmanDecoder(table);
    }

    private static JpgHuffmanDecoder GetAcDecoder(List<JpgHuffmanTable> huffTables, int tableId)
    {
        JpgHuffmanTable table = FindHuffTable(huffTables, 1, tableId);
        return new JpgHuffmanDecoder(table);
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

        throw new InvalidOperationException($"Can't find table with ID {id}  and class  {tableClass}.");
    }
}
