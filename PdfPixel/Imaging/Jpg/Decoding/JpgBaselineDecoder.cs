using System;
using PdfPixel.Imaging.Jpg.Color;
using PdfPixel.Imaging.Jpg.Idct;
using PdfPixel.Imaging.Jpg.Model;
using PdfPixel.Imaging.Jpg.Readers;

namespace PdfPixel.Imaging.Jpg.Decoding;

internal sealed class JpgBaselineDecoder : IJpgDecoder
{
    private readonly JpgHeader _header;
    private readonly ReadOnlyMemory<byte> _entropyMemory;
    private readonly JpgDecodingParameters _decodingParameters;
    private readonly JpgUpsampler _upsampler;
    private readonly IJpgColorConverter _colorConverter;
    private readonly JpgBandPacker _bandPacker;
    private readonly JpgScanSpec _scan;                       // now readonly, initialized in ctor
    private readonly JpgHuffmanDecoderManager _decoderManager; // now readonly, initialized in ctor
    private readonly JpgQuantizationManager _quantizationManager; // now readonly, initialized in ctor
    private readonly JpgRestartManager _restartManager;       // now readonly, initialized in ctor

    private bool _decoderInitialized; // bands allocation flag
    private int[] _scanToSofIndex;
    private Block8x8F[] _dequantizationBlocks;
    private int[] _previousDc;
    private Block8x8F[][] _componentBandBlocks;
    private Block8x8F[][] _upsampledBandBlocks; // Only allocated when upsampling is required.
    private byte[] _bandBuffer;
    private int _bandProduced;
    private int _bandConsumed;
    private int _bandHeight;
    private int _currentMcuRow;
    private int _currentRow;
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
            throw new NotSupportedException("JpgBaselineDecoder supports baseline (non-progressive) JPEG only.");
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

        // Initialize fixed subsystems eagerly (requested change)
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

        _upsampler = new JpgUpsampler(_decodingParameters, _header);
        _colorConverter = JpgColorConverterFactory.Create(_header, _decodingParameters);
        _bandPacker = new JpgBandPacker(_header, _decodingParameters);

        _decoderInitialized = false; // band-related allocations pending
        _currentMcuRow = 0;
        _currentRow = 0;
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

    private void EnsureDecoderInitialized()
    {
        if (_decoderInitialized)
        {
            return;
        }

        // Allocate band-level structures (the only deferred work now)
        int componentCount = _header.ComponentCount;
        _componentBandBlocks = new Block8x8F[componentCount][];
        if (_decodingParameters.NeedsUpsampling)
        {
            _upsampledBandBlocks = new Block8x8F[componentCount][];
        }

        for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
        {
            int totalBlocksForBand = _decodingParameters.TotalBlocksPerBand[componentIndex];
            _componentBandBlocks[componentIndex] = new Block8x8F[totalBlocksForBand];
            if (_decodingParameters.NeedsUpsampling)
            {
                _upsampledBandBlocks[componentIndex] = new Block8x8F[_decodingParameters.McuColumns * _decodingParameters.UpsampledBlocksPerMcu];
            }
        }

        _scanToSofIndex = JpgComponentMapper.MapScanToSofIndices(_header, _scan);
        if (_scanToSofIndex == null)
        {
            throw new InvalidOperationException("Failed to map scan components to SOF indices.");
        }

        _previousDc = new int[_header.ComponentCount];

        _bandHeight = _decodingParameters.McuHeight;
        _bandBuffer = new byte[_bandHeight * _decodingParameters.OutputStride];
        _bandProduced = 0;
        _bandConsumed = 0;

        _dequantizationBlocks = new Block8x8F[_header.ComponentCount];
        for (int planIndex = 0; planIndex < _header.ComponentCount; planIndex++)
        {
            int qid = _header.Components[planIndex].QuantizationTableId;
            _dequantizationBlocks[planIndex] = _quantizationManager.CreateNaturalBlock(qid);
        }

        var startSpan = _entropyMemory.Span;
        var initialBitReader = new JpgBitReader(startSpan);
        _savedState = initialBitReader.CaptureState();
        _hasSavedState = true;
        _decoderInitialized = true;
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

        var blockNatural = default(Block8x8F);
        var sourceSpan = _entropyMemory.Span;
        var bitReader = _hasSavedState ? new JpgBitReader(sourceSpan, _savedState) : new JpgBitReader(sourceSpan);

        for (int mcuColumnIndex = 0; mcuColumnIndex < _decodingParameters.McuColumns; mcuColumnIndex++)
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
                        int localBlockIndex = vBlock * hFactor + hBlock;
                        int globalBlockIndex = mcuColumnIndex * blocksPerMcu + localBlockIndex;
                        bandBlocks[globalBlockIndex] = blockNatural;
                    }
                }
            }
            _restartManager.DecrementRestartCounter();
        }

        Block8x8F[][] workingBlocks = _decodingParameters.NeedsUpsampling ? _upsampledBandBlocks : _componentBandBlocks;
        if (_decodingParameters.NeedsUpsampling)
        {
            _upsampler.UpsampleBand(_componentBandBlocks, _upsampledBandBlocks);
        }
        _colorConverter.ConvertInPlace(workingBlocks);
        _bandPacker.Pack(workingBlocks, bandRows, _bandBuffer);

        _savedState = bitReader.CaptureState();
        _hasSavedState = true;
        _bandHeight = bandRows;
        _bandProduced = bandRows * _decodingParameters.OutputStride;
        _bandConsumed = 0;
        _currentMcuRow++;
    }
}
