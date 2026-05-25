using System;
using PdfPixel.Jpg.Color;
using PdfPixel.Jpg.Idct;
using PdfPixel.Jpg.Model;
using PdfPixel.Jpg.Readers;

namespace PdfPixel.Jpg.Decoding;

/// <summary>
/// Decodes baseline (SOF0) and extended sequential (SOF1) JPEG images row by row.
/// </summary>
public sealed class JpgBaselineDecoder : IJpgDecoder
{
    private readonly JpgHeader _header;
    private readonly ReadOnlyMemory<byte> _entropyMemory;
    private readonly JpgDecodingParameters _decodingParameters;
    private readonly JpgUpsampler? _upsampler;
    private readonly IJpgColorConverter _colorConverter;
    private readonly JpgBandPacker _bandPacker;
    private readonly JpgScanSpec _scan;
    private readonly JpgHuffmanDecoderManager _decoderManager;
    private readonly JpgQuantizationManager _quantizationManager;
    private readonly JpgRestartManager _restartManager;
    private readonly int[] _scanToSofIndex;
    private readonly Block8x8F[] _dequantizationBlocks;
    private readonly int[] _previousDc;
    private readonly Block8x8F[][] _componentBandBlocks;
    private readonly Block8x8F[][] _upsampledBandBlocks;
    private readonly byte[] _bandBuffer;

    private int _bandProduced;
    private int _bandConsumed;
    private int _bandHeight;
    private int _currentMcuRow;
    private int _currentRow;
    private JpgBitReaderState _savedState;

    /// <summary>
    /// Initializes a new <see cref="JpgBaselineDecoder"/> for the given header and entropy-coded data.
    /// </summary>
    /// <param name="header">Parsed JPEG header from <see cref="Readers.JpgReader.ParseHeader"/>.</param>
    /// <param name="entropyData">Entropy-coded image data beginning at the offset recorded in the header.</param>
    /// <param name="conversionParams">Optional color conversion overrides; uses <see cref="JpegColorConversionParameters.Default"/> when null.</param>
    public JpgBaselineDecoder(JpgHeader header, in ReadOnlyMemory<byte> entropyData, JpegColorConversionParameters? conversionParams = null)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        if (header.FrameType != JpgFrameType.BaselineDct && header.FrameType != JpgFrameType.ExtendedSequentialDct)
        {
            throw new NotSupportedException($"JpgBaselineDecoder supports baseline (SOF0) and extended sequential (SOF1) JPEG only. Got {header.FrameType.ToString()}.");
        }

        if (header.FrameType == JpgFrameType.ExtendedSequentialDct && header.HasMultipleScans)
        {
            throw new NotSupportedException("Extended sequential JPEG (SOF1) with multiple scans is not supported.");
        }

        if (header.ComponentCount <= 0 || header.Components == null || header.Components.Count != header.ComponentCount)
        {
            throw new ArgumentException("Invalid header components.", nameof(header));
        }

        if (header.Scans == null || header.Scans.Count == 0)
        {
            throw new NotSupportedException("No SOS scan found in header.");
        }

        _header = header;
        _entropyMemory = entropyData;
        _decodingParameters = new JpgDecodingParameters(header);

        _scan = _header.Scans[0];
        _decoderManager = JpgHuffmanDecoderManager.CreateFromHeader(_header);
        _quantizationManager = JpgQuantizationManager.CreateFromHeader(_header);
        _decoderManager.ValidateTablesForScan(_scan);
        for (int componentIndex = 0; componentIndex < _header.Components.Count; componentIndex++)
        {
            int quantTableId = _header.Components[componentIndex].QuantizationTableId;
            _quantizationManager.ValidateTableExists(quantTableId, componentIndex);
        }

        _restartManager = new JpgRestartManager(_header.RestartInterval);
        _upsampler = (_decodingParameters.NeedsUpsampling) ? new JpgUpsampler(_decodingParameters, _header) : null;
        _colorConverter = JpgColorConverterFactory.Create(_header, _decodingParameters, conversionParams);
        _bandPacker = new JpgBandPacker(_header, _decodingParameters);

        int componentCount = _header.ComponentCount;
        _componentBandBlocks = new Block8x8F[componentCount][];
        _upsampledBandBlocks = (_decodingParameters.NeedsUpsampling) ? new Block8x8F[componentCount][] : [];

        for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
        {
            int totalBlocksForBand = _decodingParameters.TotalBlocksPerBand[componentIndex];
            _componentBandBlocks[componentIndex] = new Block8x8F[totalBlocksForBand];
            if (_decodingParameters.NeedsUpsampling)
            {
                _upsampledBandBlocks[componentIndex] = new Block8x8F[_decodingParameters.McuColumns * _decodingParameters.UpsampledBlocksPerMcu];
            }
        }

        _scanToSofIndex = JpgComponentMapper.MapScanToSofIndices(_header, _scan)
            ?? throw new InvalidOperationException("Failed to map scan components to SOF indices.");
        _previousDc = new int[_header.ComponentCount];

        _dequantizationBlocks = new Block8x8F[_header.ComponentCount];
        for (int planIndex = 0; planIndex < _header.ComponentCount; planIndex++)
        {
            int qid = _header.Components[planIndex].QuantizationTableId;
            _dequantizationBlocks[planIndex] = _quantizationManager.CreateNaturalBlock(qid);
        }

        _bandHeight = _decodingParameters.McuHeight;
        _bandBuffer = new byte[_bandHeight * _decodingParameters.OutputStride];

        ReadOnlySpan<byte> startSpan = _entropyMemory.Span;
        JpgBitReader initialBitReader = new(startSpan);
        _savedState = initialBitReader.CaptureState();
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

        if (_currentRow >= _header.Height)
        {
            return false;
        }

        if (rowBuffer.Length < _decodingParameters.OutputStride)
        {
            throw new ArgumentException("Row buffer too small for decoded row.", nameof(rowBuffer));
        }

        if (_bandConsumed >= _bandProduced)
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
        int bandRows = (remainingRows < _decodingParameters.McuHeight) ? remainingRows : _decodingParameters.McuHeight;
        if (bandRows <= 0)
        {
            _bandProduced = 0;
            _bandConsumed = 0;
            return;
        }

        var blockNatural = default(Block8x8F);
        ReadOnlySpan<byte> sourceSpan = _entropyMemory.Span;
        JpgBitReader bitReader = new(sourceSpan, _savedState);

        for (int mcuColumnIndex = 0; mcuColumnIndex < _decodingParameters.McuColumns; mcuColumnIndex++)
        {
            if (_restartManager.IsRestartNeeded)
            {
                _restartManager.ProcessRestart(ref bitReader, _previousDc);
            }

            for (int scanComponentIndex = 0; scanComponentIndex < _scan.Components.Count; scanComponentIndex++)
            {
                int componentIndex = _scanToSofIndex[scanComponentIndex];
                JpgScanComponentSpec scanComponent = _scan.Components[scanComponentIndex];
                (Huffman.JpgHuffmanDecoder dcDecoder, Huffman.JpgHuffmanDecoder acDecoder) decoders = _decoderManager.GetDecodersForScanComponent(scanComponent);
                Huffman.JpgHuffmanDecoder dcDecoder = decoders.dcDecoder;
                Huffman.JpgHuffmanDecoder acDecoder = decoders.acDecoder;
                int hFactor = _decodingParameters.ComponentBlocksH[componentIndex];
                int vFactor = _decodingParameters.ComponentBlocksV[componentIndex];
                int blocksPerMcu = _decodingParameters.BlocksPerMcu[componentIndex];
                Block8x8F[] bandBlocks = _componentBandBlocks[componentIndex];
                for (int vBlock = 0; vBlock < vFactor; vBlock++)
                {
                    for (int hBlock = 0; hBlock < hFactor; hBlock++)
                    {
                        JpgBlockDecoder.DecodeBaselineBlock(
                            ref bitReader,
                            dcDecoder,
                            acDecoder,
                            ref _previousDc[componentIndex],
                            ref blockNatural,
                            out bool dcOnly);
                        ref var dequantBlock = ref _dequantizationBlocks[componentIndex];
                        IdctTransform.TransformScaledNatural(ref blockNatural, ref dequantBlock, dcOnly);
                        int localBlockIndex = (vBlock * hFactor) + hBlock;
                        int globalBlockIndex = (mcuColumnIndex * blocksPerMcu) + localBlockIndex;
                        bandBlocks[globalBlockIndex] = blockNatural;
                    }
                }
            }

            _restartManager.DecrementRestartCounter();
        }

        Block8x8F[][] workingBlocks = (_decodingParameters.NeedsUpsampling) ? _upsampledBandBlocks : _componentBandBlocks;
        if (_decodingParameters.NeedsUpsampling && _upsampler != null)
        {
            _upsampler.UpsampleBand(_componentBandBlocks, _upsampledBandBlocks);
        }

        _colorConverter.ConvertInPlace(workingBlocks);
        _bandPacker.Pack(workingBlocks, bandRows, _bandBuffer);

        _savedState = bitReader.CaptureState();
        _bandHeight = bandRows;
        _bandProduced = bandRows * _decodingParameters.OutputStride;
        _bandConsumed = 0;
        _currentMcuRow++;
    }
}
