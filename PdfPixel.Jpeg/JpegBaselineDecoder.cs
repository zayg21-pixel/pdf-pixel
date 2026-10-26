using System;
using System.Runtime.CompilerServices;
using PdfPixel.Jpeg.ColorConversion;
using PdfPixel.Jpeg.Decoding;
using PdfPixel.Jpeg.Idct;

namespace PdfPixel.Jpeg;

/// <summary>
/// Decodes baseline (SOF0) and extended sequential (SOF1) JPEG images row by row.
/// </summary>
public sealed class JpegBaselineDecoder : IJpegDecoder
{
    private readonly JpegHeader _header;
    private readonly ReadOnlyMemory<byte> _entropyMemory;
    private readonly JpegDecodingParameters _decodingParameters;
    private readonly JpegUpsampler? _upsampler;
    private readonly IJpegColorConverter _colorConverter;
    private readonly JpegBandPacker _bandPacker;
    private readonly JpegScanSpec _scan;
    private readonly JpegHuffmanDecoderManager _decoderManager;
    private readonly JpegQuantizationManager _quantizationManager;
    private readonly JpegRestartManager _restartManager;
    private readonly int[] _scanToSofIndex;
    private readonly Block8x8F[] _dequantizationBlocks;
    private readonly int[] _previousDc;
    private readonly Block8x8F[][] _componentBandBlocks;
    private readonly Block8x8F[][] _upsampledBandBlocks;
    private readonly Block8x8F[][] _workingBandBlocks;

    private int _bandRows;
    private int _bandRowIndex;
    private int _currentMcuRow;
    private int _currentRow;
    private bool _bandReconstructed;
    private JpegBitReaderState _savedState;

    /// <summary>
    /// Initializes a new <see cref="JpegBaselineDecoder"/> for the given header and entropy-coded data.
    /// </summary>
    /// <param name="header">Parsed JPEG header from <see cref="JpegHeader.Parse"/>.</param>
    /// <param name="entropyData">Entropy-coded image data beginning at the offset recorded in the header.</param>
    /// <param name="options">Optional decoding overrides; uses <see cref="JpegDecoderOptions.Default"/> when null.</param>
    public JpegBaselineDecoder(JpegHeader header, in ReadOnlyMemory<byte> entropyData, JpegDecoderOptions? options = null)
    {
        if (header == null)
        {
            throw new ArgumentNullException(nameof(header));
        }

        if (header.FrameType != JpegFrameType.BaselineDct && header.FrameType != JpegFrameType.ExtendedSequentialDct)
        {
            throw new NotSupportedException($"JpegBaselineDecoder supports baseline (SOF0) and extended sequential (SOF1) JPEG only. Got {header.FrameType.ToString()}.");
        }

        if (header.FrameType == JpegFrameType.ExtendedSequentialDct && header.HasMultipleScans)
        {
            throw new NotSupportedException("Extended sequential JPEG (SOF1) with multiple scans is not supported.");
        }

        if (header.ComponentCount <= 0 || header.Components == null || header.Components.Length != header.ComponentCount)
        {
            throw new ArgumentException("Invalid header components.", nameof(header));
        }

        if (header.Scans == null || header.Scans.Count == 0)
        {
            throw new NotSupportedException("No SOS scan found in header.");
        }

        options ??= JpegDecoderOptions.Default;

        _header = header;
        _entropyMemory = entropyData;
        _decodingParameters = new JpegDecodingParameters(header, options.DescaleFactor, options.RegionsOfInterest);

        _scan = _header.Scans[0];
        _decoderManager = JpegHuffmanDecoderManager.CreateFromHeader(_header);
        _quantizationManager = JpegQuantizationManager.CreateFromHeader(_header);
        _decoderManager.ValidateTablesForScan(_scan);
        for (int componentIndex = 0; componentIndex < _header.Components.Length; componentIndex++)
        {
            int quantTableId = _header.Components[componentIndex].QuantizationTableId;
            _quantizationManager.ValidateTableExists(quantTableId, componentIndex);
        }

        _restartManager = new JpegRestartManager(_header.RestartInterval);
        _upsampler = (_decodingParameters.NeedsUpsampling) ? new JpegUpsampler(_decodingParameters, _header) : null;
        _colorConverter = JpegColorConverterFactory.Create(_header, _decodingParameters, options);
        _bandPacker = new JpegBandPacker(_header, _decodingParameters);

        int componentCount = _header.ComponentCount;
        _componentBandBlocks = new Block8x8F[componentCount][];
        _upsampledBandBlocks = (_decodingParameters.NeedsUpsampling) ? new Block8x8F[componentCount][] : [];

        for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
        {
            int totalBlocksForBand = _decodingParameters.TotalBlocksPerBand[componentIndex];
            _componentBandBlocks[componentIndex] = new Block8x8F[totalBlocksForBand];
            if (_decodingParameters.NeedsUpsampling)
            {
                _upsampledBandBlocks[componentIndex] = new Block8x8F[_decodingParameters.ReconstructedMcuColumns * _decodingParameters.UpsampledBlocksPerMcu];
            }
        }

        _workingBandBlocks = (_decodingParameters.NeedsUpsampling) ? _upsampledBandBlocks : _componentBandBlocks;

        _scanToSofIndex = JpegComponentMapper.MapScanToSofIndices(_header, _scan)
            ?? throw new InvalidOperationException("Failed to map scan components to SOF indices.");
        _previousDc = new int[_header.ComponentCount];

        _dequantizationBlocks = new Block8x8F[_header.ComponentCount];
        for (int planIndex = 0; planIndex < _header.ComponentCount; planIndex++)
        {
            int qid = _header.Components[planIndex].QuantizationTableId;
            _dequantizationBlocks[planIndex] = _quantizationManager.CreateNaturalBlock(qid);
        }

        ReadOnlySpan<byte> startSpan = _entropyMemory.Span;
        JpegBitReader initialBitReader = new(startSpan);
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

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
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

        // Entropy-coded data is one serial stream, so every data unit is read whether or not it is
        // wanted; only the reconstruction that follows it is confined to the regions of interest.
        bool reconstructBand = _decodingParameters.IsMcuRowReconstructed(_currentMcuRow);
        int firstReconstructedColumn = _decodingParameters.ReconstructedMcuColumnStart;
        int reconstructedColumnLimit = firstReconstructedColumn + _decodingParameters.ReconstructedMcuColumns;

        var blockNatural = default(Block8x8F);
        ReadOnlySpan<byte> sourceSpan = _entropyMemory.Span;
        JpegBitReader bitReader = new(sourceSpan, _savedState);

        for (int mcuColumnIndex = 0; mcuColumnIndex < _decodingParameters.McuColumns; mcuColumnIndex++)
        {
            if (_restartManager.IsRestartNeeded)
            {
                _restartManager.ProcessRestart(ref bitReader, _previousDc);
            }

            int bandColumnIndex = mcuColumnIndex - firstReconstructedColumn;
            bool reconstructColumn = reconstructBand
                && mcuColumnIndex >= firstReconstructedColumn
                && mcuColumnIndex < reconstructedColumnLimit;

            for (int scanComponentIndex = 0; scanComponentIndex < _scan.Components.Length; scanComponentIndex++)
            {
                int componentIndex = _scanToSofIndex[scanComponentIndex];
                JpegScanComponentSpec scanComponent = _scan.Components[scanComponentIndex];
                (Huffman.JpegHuffmanDecoder dcDecoder, Huffman.JpegHuffmanDecoder acDecoder) decoders = _decoderManager.GetDecodersForScanComponent(scanComponent);
                Huffman.JpegHuffmanDecoder dcDecoder = decoders.dcDecoder;
                Huffman.JpegHuffmanDecoder acDecoder = decoders.acDecoder;
                int hFactor = _decodingParameters.ComponentBlocksH[componentIndex];
                int vFactor = _decodingParameters.ComponentBlocksV[componentIndex];
                int blocksPerMcu = _decodingParameters.BlocksPerMcu[componentIndex];
                Block8x8F[] bandBlocks = _componentBandBlocks[componentIndex];
                for (int vBlock = 0; vBlock < vFactor; vBlock++)
                {
                    for (int hBlock = 0; hBlock < hFactor; hBlock++)
                    {
                        JpegBlockDecoder.DecodeBaselineBlock(
                            ref bitReader,
                            dcDecoder,
                            acDecoder,
                            ref _previousDc[componentIndex],
                            ref blockNatural,
                            out bool dcOnly);

                        if (!reconstructColumn)
                        {
                            continue;
                        }

                        ref var dequantBlock = ref _dequantizationBlocks[componentIndex];
                        IdctTransform.TransformScaledNatural(
                            ref blockNatural,
                            ref dequantBlock,
                            dcOnly,
                            _decodingParameters.ComponentIdctWidth[componentIndex],
                            _decodingParameters.ComponentIdctHeight[componentIndex]);
                        int localBlockIndex = (vBlock * hFactor) + hBlock;
                        int bandBlockIndex = (bandColumnIndex * blocksPerMcu) + localBlockIndex;
                        bandBlocks[bandBlockIndex] = blockNatural;
                    }
                }
            }

            _restartManager.DecrementRestartCounter();
        }

        if (reconstructBand)
        {
            if (_decodingParameters.NeedsUpsampling && _upsampler != null)
            {
                _upsampler.UpsampleBand(_componentBandBlocks, _upsampledBandBlocks);
            }

            _colorConverter.ConvertInPlace(_workingBandBlocks);
        }

        _savedState = bitReader.CaptureState();
        _bandRows = bandRows;
        _bandRowIndex = 0;
        _bandReconstructed = reconstructBand;
        _currentMcuRow++;
    }
}
