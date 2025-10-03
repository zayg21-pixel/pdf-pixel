using System;
using System.IO;
using System.Collections.Generic;
using PdfReader.Rendering.Color;
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
    /// Progressive JPEG decoder that exposes decoded sample bytes as a forward-only, non-seekable stream.
    /// Output pixel format is raw interleaved component bytes (Gray = 1, YCbCr/RGB = 3 after conversion, CMYK/YCCK = 4).
    /// Color space conversion for YCbCr -> RGB and YCCK -> CMYK happens inside the MCU writers; grayscale and CMYK remain raw.
    /// The stream produces one MCU-row band at a time to minimize working memory.
    /// </summary>
    internal sealed class JpgProgressiveStream : ContentStream
    {
        private const int DctBlockSize = 64;

        private readonly JpgHeader _header;
        private readonly PdfColorSpaceConverter _colorConverter;
        private readonly ReadOnlyMemory<byte> _entropyMemory;

        // Sampling / MCU geometry
        private readonly int _hMax;
        private readonly int _vMax;
        public int McuWidth { get; }
        public int McuHeight { get; }

        private bool _decoderInitialized;

        // Coefficient buffers for the whole image (natural order, per block)
        private struct CoeffBuffers
        {
            public int BlocksX;
            public int BlocksY;
            public int[] Coeffs; // length = BlocksX * BlocksY * 64
        }

        private SamplingInfo _samplingInfo;
        private CoeffBuffers[] _coeffBuffers;

        private JpgQuantizationManager _quantizationManager;
        private Idct.ScaledIdctPlan[] _scaledPlans;

        // Per-MCU reusable buffers for rendering
        private readonly int[] _blockNatural = new int[DctBlockSize];
        private readonly byte[] _idctTemp = new byte[DctBlockSize];
        private readonly int[] _idctWorkspace = new int[DctBlockSize];

        // Per-component MCU tiles (reused for writing one MCU to band)
        private byte[][] _componentTiles;
        private int[] _tileWidths;
        private int[] _tileHeights;

        // Output band buffer for one MCU row (reused)
        private byte[] _bandBuffer;
        private int _mcuColumns;
        private int _mcuRows;
        private int _bandHeight;
        private int _bandProduced;
        private int _bandConsumed;

        private IMcuWriter _mcuWriter;

        public int Width => _header.Width;
        public int Height => _header.Height;

        /// <summary>
        /// Output stride in bytes for one row = Width * componentCount (1, 3 or 4).
        /// </summary>
        public int OutputStride => checked(Width * _header.ComponentCount);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length
        {
            get { throw new NotSupportedException("Length is not supported."); }
        }

        private long _outputBytePosition;
        public override long Position
        {
            get { return _outputBytePosition; }
            set { throw new NotSupportedException("Seeking is not supported."); }
        }

        public int CurrentMcuRow { get; private set; }

        public JpgProgressiveStream(JpgHeader header, ReadOnlyMemory<byte> entropyData, PdfColorSpaceConverter colorConverter)
        {
            if (header == null)
            {
                throw new ArgumentNullException(nameof(header));
            }

            if (colorConverter == null)
            {
                throw new ArgumentNullException(nameof(colorConverter));
            }

            if (!header.IsProgressive)
            {
                throw new NotSupportedException("JpgProgressiveStream supports progressive JPEG only.");
            }

            _header = header;
            _colorConverter = colorConverter;
            _entropyMemory = entropyData;

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
            int offset = 0;
            int count = buffer.Length;

            while (count > 0)
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

            if (_header.ComponentCount <= 0 || _header.Components == null || _header.Components.Count != _header.ComponentCount)
            {
                throw new ArgumentException("Invalid header components.", nameof(_header));
            }

            // Prepare sampling and MCU geometry
            _samplingInfo = JpgComponentSampler.CalculateSamplingInfo(_header);
            var dims = JpgComponentSampler.CalculateMcuDimensions(_header, _samplingInfo);
            _mcuColumns = dims.mcuColumns;
            _mcuRows = dims.mcuRows;

            // Initialize coefficient buffers for whole image (natural order)
            _coeffBuffers = InitializeCoefficientBuffers(_samplingInfo);

            // Decode all progressive scans into coefficient buffers (no plane allocation)
            if (!ProcessProgressiveScans(_header, _entropyMemory.Span, _samplingInfo, _coeffBuffers))
            {
                throw new InvalidOperationException("Failed to decode progressive scans.");
            }

            // Prepare quantization manager
            _quantizationManager = JpgQuantizationManager.CreateFromHeader(_header);
            for (int componentIndex = 0; componentIndex < _header.Components.Count; componentIndex++)
            {
                int qid = _header.Components[componentIndex].QuantizationTableId;
                if (!_quantizationManager.ValidateTableExists(qid, componentIndex))
                {
                    throw new InvalidOperationException($"Quantization table {qid} missing for component {componentIndex}.");
                }
            }

            // Prepare per-component tiles reused per MCU rendering
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

            // Allocate band buffer for one MCU row (RGBA). No clearing; we fully overwrite valid pixel region.
            _bandHeight = McuHeight;
            _bandBuffer = new byte[_bandHeight * OutputStride];
            _bandProduced = 0;
            _bandConsumed = 0;

            // Create MCU writer based on color mode
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

            // No Array.Clear: writer fills all pixels up to image width for each output row.

            for (int mcuCol = 0; mcuCol < _mcuColumns; mcuCol++)
            {
                // For each component, build its MCU tile from coefficient buffers
                for (int ci = 0; ci < _header.ComponentCount; ci++)
                {
                    var comp = _header.Components[ci];
                    int hs = comp.HorizontalSamplingFactor;
                    int vs = comp.VerticalSamplingFactor;
                    int tileWidth = _tileWidths[ci];
                    byte[] tile = _componentTiles[ci];
                    var cb = _coeffBuffers[ci];

                    for (int by = 0; by < vs; by++)
                    {
                        int blockY = CurrentMcuRow * vs + by;
                        if (blockY >= cb.BlocksY)
                        {
                            continue;
                        }

                        for (int bx = 0; bx < hs; bx++)
                        {
                            int blockX = mcuCol * hs + bx;
                            if (blockX >= cb.BlocksX)
                            {
                                continue;
                            }

                            int coeffBase = (blockY * cb.BlocksX + blockX) * DctBlockSize;

                            // Load natural-order coefficients for this block
                            for (int i = 0; i < DctBlockSize; i++)
                            {
                                _blockNatural[i] = cb.Coeffs[coeffBase + i];
                            }

                            // Dequantize and IDCT using precomputed plan (guaranteed non-null after validation)
                            var plan = _scaledPlans[ci];
                            int dstY0 = by * 8;
                            int dstX0 = bx * 8;
                            int dstOff = dstY0 * tileWidth + dstX0;
                            JpgIdct.TransformScaledNatural(_blockNatural, plan, tile.AsSpan(dstOff), tileWidth, _idctWorkspace);
                        }
                    }
                }

                int xBase = mcuCol * McuWidth;
                _mcuWriter.WriteToBuffer(_bandBuffer, xBase, bandRows);
            }

            _bandHeight = bandRows;
            _bandProduced = bandRows * OutputStride;
            _bandConsumed = 0;
            CurrentMcuRow++;
        }

        private static CoeffBuffers[] InitializeCoefficientBuffers(SamplingInfo samplingInfo)
        {
            int componentCount = samplingInfo.ComponentWidths.Length;
            var coeffBuffers = new CoeffBuffers[componentCount];

            for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
            {
                coeffBuffers[componentIndex].BlocksX = samplingInfo.ComponentBlocksX[componentIndex];
                coeffBuffers[componentIndex].BlocksY = samplingInfo.ComponentBlocksY[componentIndex];
                coeffBuffers[componentIndex].Coeffs = new int[
                    samplingInfo.ComponentBlocksX[componentIndex] *
                    samplingInfo.ComponentBlocksY[componentIndex] *
                    DctBlockSize];
            }

            return coeffBuffers;
        }

        // Scans processing adapted from JpgProgressiveDecoder, restricted to filling _coeffBuffers
        private static bool ProcessProgressiveScans(
            JpgHeader header,
            ReadOnlySpan<byte> content,
            SamplingInfo samplingInfo,
            CoeffBuffers[] coeffBuffers)
        {
            var huffTables = new List<JpgHuffmanTable>(header.HuffmanTables);
            var quantTables = new List<JpgQuantizationTable>(header.QuantizationTables);
            int restartInterval = header.RestartInterval;

            ReadOnlySpan<byte> stream = content;
            var bitReader = new JpgBitReader(stream);
            JpgScanSpec currentScan = header.Scans.Count > 0 ? header.Scans[0] : null;
            int[] prevDc = new int[header.ComponentCount];
            int eobRun = 0;

            if (!ProcessCurrentScan(header, samplingInfo, coeffBuffers, huffTables, restartInterval, ref bitReader, currentScan, prevDc, ref eobRun))
            {
                return false;
            }

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
                if (pos + 2 > stream.Length)
                {
                    break;
                }

                ushort segLen = (ushort)(stream[pos] << 8 | stream[pos + 1]);
                int payloadLen = segLen - 2;
                pos += 2;
                if (payloadLen < 0 || pos + payloadLen > stream.Length)
                {
                    break;
                }

                var payload = stream.Slice(pos, payloadLen);
                int nextPos = pos + payloadLen;
                stream = stream.Slice(nextPos);
                bitReader = new JpgBitReader(stream);

                switch (marker)
                {
                    case 0xDB:
                        var qts = JpgQuantizationTable.ParseDqtPayload(payload);
                        quantTables.AddRange(qts);
                        break;
                    case 0xC4:
                        var hts = JpgHuffmanTable.ParseDhtPayload(payload);
                        huffTables.AddRange(hts);
                        break;
                    case 0xDD:
                        if (payloadLen >= 2)
                        {
                            restartInterval = payload[0] << 8 | payload[1];
                        }
                        break;
                    case 0xDA:
                        currentScan = JpgReader.ParseSos(payload);
                        if (!ProcessCurrentScan(header, samplingInfo, coeffBuffers, huffTables, restartInterval, ref bitReader, currentScan, prevDc, ref eobRun))
                        {
                            return false;
                        }
                        break;
                    default:
                        break;
                }
            }

            return true;
        }

        private static bool ProcessCurrentScan(
            JpgHeader header,
            SamplingInfo samplingInfo,
            CoeffBuffers[] coeffs,
            List<JpgHuffmanTable> huffTables,
            int restartInterval,
            ref JpgBitReader bitReader,
            JpgScanSpec currentScan,
            int[] prevDc,
            ref int eobRun)
        {
            if (currentScan == null)
            {
                Console.Error.WriteLine("[PdfReader][JPEG] Progressive: currentScan is null");
                return false;
            }

            bool isDcScan = currentScan.SpectralStart == 0 && currentScan.SpectralEnd == 0;
            bool firstPass = currentScan.SuccessiveApproxHigh == 0;
            int Al = currentScan.SuccessiveApproxLow;
            int Ah = currentScan.SuccessiveApproxHigh;

            int scanCompCount = currentScan.Components.Count;
            int[] scanToComp = JpgComponentMapper.MapScanToSofIndices(header, currentScan, permissive: true);
            if (scanToComp == null)
            {
                Console.Error.WriteLine("[PdfReader][JPEG] Progressive: Failed to map scan components to SOF indices");
                return false;
            }

            var dcDecoders = new JpgHuffmanDecoder[scanCompCount];
            var acDecoders = new JpgHuffmanDecoder[scanCompCount];
            for (int i = 0; i < scanCompCount; i++)
            {
                dcDecoders[i] = GetDcDecoder(huffTables, currentScan.Components[i].DcTableId);
                acDecoders[i] = GetAcDecoder(huffTables, currentScan.Components[i].AcTableId);

                if ((isDcScan && dcDecoders[i] == null) || (!isDcScan && acDecoders[i] == null))
                {
                    Console.Error.WriteLine($"[PdfReader][JPEG] Progressive: Missing required table for component {i}");
                    return false;
                }
            }

            var restartManager = new JpgRestartManager(restartInterval);

            for (int c = 0; c < header.ComponentCount; c++)
            {
                prevDc[c] = 0;
            }

            eobRun = 0;

            if (scanCompCount > 1)
            {
                var (mcuCols, mcuRows) = JpgComponentSampler.CalculateMcuDimensions(header, samplingInfo);

                for (int mcuY = 0; mcuY < mcuRows; mcuY++)
                {
                    for (int mcuX = 0; mcuX < mcuCols; mcuX++)
                    {
                        if (restartManager.TryProcessPendingRestart(ref bitReader, prevDc))
                        {
                            eobRun = 0;
                        }

                        if (restartManager.IsRestartNeeded)
                        {
                            if (!restartManager.ProcessRestart(ref bitReader, prevDc))
                            {
                                Console.Error.WriteLine($"[PdfReader][JPEG] Progressive: Restart failed at MCU ({mcuX}, {mcuY})");
                                return false;
                            }
                            eobRun = 0;
                        }

                        for (int si = 0; si < scanCompCount; si++)
                        {
                            int compIndex = scanToComp[si];
                            int hs = header.Components[compIndex].HorizontalSamplingFactor;
                            int vs = header.Components[compIndex].VerticalSamplingFactor;
                            var dc = dcDecoders[si];
                            var ac = acDecoders[si];

                            for (int by = 0; by < vs; by++)
                            {
                                for (int bx = 0; bx < hs; bx++)
                                {
                                    int blockX = mcuX * hs + bx;
                                    int blockY = mcuY * vs + by;
                                    if (blockX >= coeffs[compIndex].BlocksX || blockY >= coeffs[compIndex].BlocksY)
                                    {
                                        continue;
                                    }

                                    int blockIndex = (blockY * coeffs[compIndex].BlocksX + blockX) * DctBlockSize;

                                    if (isDcScan)
                                    {
                                        if (!JpgProgressiveBlockDecoder.DecodeDcCoefficient(
                                            ref bitReader,
                                            dc,
                                            ref prevDc[compIndex],
                                            coeffs[compIndex].Coeffs,
                                            blockIndex,
                                            firstPass,
                                            Al))
                                        {
                                            Console.Error.WriteLine($"[PdfReader][JPEG] Progressive: DC decode failed at MCU ({mcuX}, {mcuY})");
                                            return false;
                                        }
                                    }
                                    else
                                    {
                                        if (firstPass)
                                        {
                                            if (!JpgProgressiveBlockDecoder.DecodeAcCoefficientsFirstPass(
                                                ref bitReader,
                                                ac,
                                                coeffs[compIndex].Coeffs,
                                                blockIndex,
                                                currentScan.SpectralStart,
                                                currentScan.SpectralEnd,
                                                Al,
                                                ref eobRun))
                                            {
                                                Console.Error.WriteLine($"[PdfReader][JPEG] Progressive: AC first pass failed at MCU ({mcuX}, {mcuY}), component {compIndex}");
                                                return false;
                                            }
                                        }
                                        else
                                        {
                                            if (!JpgProgressiveBlockDecoder.DecodeAcCoefficientsRefinement(
                                                ref bitReader,
                                                ac,
                                                coeffs[compIndex].Coeffs,
                                                blockIndex,
                                                currentScan.SpectralStart,
                                                currentScan.SpectralEnd,
                                                Ah,
                                                Al,
                                                ref eobRun))
                                            {
                                                Console.Error.WriteLine($"[PdfReader][JPEG] Progressive: AC refinement failed at MCU ({mcuX}, {mcuY}), component {compIndex}");
                                                return false;
                                            }
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
                int compIndex = scanToComp[0];
                var dc = dcDecoders[0];
                var ac = acDecoders[0];

                int blocksX = coeffs[compIndex].BlocksX;
                int blocksY = coeffs[compIndex].BlocksY;

                int blocksProcessed = 0;
                for (int by = 0; by < blocksY; by++)
                {
                    for (int bx = 0; bx < blocksX; bx++)
                    {
                        if (restartManager.TryProcessPendingRestart(ref bitReader, prevDc))
                        {
                            eobRun = 0;
                        }

                        if (restartManager.IsRestartNeeded)
                        {
                            if (!restartManager.ProcessRestart(ref bitReader, prevDc))
                            {
                                Console.Error.WriteLine($"[PdfReader][JPEG] Progressive: Restart failed at block ({bx}, {by})");
                                return false;
                            }
                            eobRun = 0;
                        }

                        int blockIndex = (by * blocksX + bx) * DctBlockSize;
                        blocksProcessed++;

                        if (isDcScan)
                        {
                            if (!JpgProgressiveBlockDecoder.DecodeDcCoefficient(
                                ref bitReader,
                                dc,
                                ref prevDc[compIndex],
                                coeffs[compIndex].Coeffs,
                                blockIndex,
                                firstPass,
                                Al))
                            {
                                Console.Error.WriteLine($"[PdfReader][JPEG] Progressive: DC decode failed at block ({bx}, {by}), processed {blocksProcessed} blocks");
                                return false;
                            }
                        }
                        else
                        {
                            if (firstPass)
                            {
                                if (!JpgProgressiveBlockDecoder.DecodeAcCoefficientsFirstPass(
                                    ref bitReader,
                                    ac,
                                    coeffs[compIndex].Coeffs,
                                    blockIndex,
                                    currentScan.SpectralStart,
                                    currentScan.SpectralEnd,
                                    Al,
                                    ref eobRun))
                                {
                                    Console.Error.WriteLine($"[PdfReader][JPEG] Progressive: AC first pass failed at block ({bx}, {by}), processed {blocksProcessed} blocks, eobRun={eobRun}");
                                    return false;
                                }
                            }
                            else
                            {
                                if (!JpgProgressiveBlockDecoder.DecodeAcCoefficientsRefinement(
                                    ref bitReader,
                                    ac,
                                    coeffs[compIndex].Coeffs,
                                    blockIndex,
                                    currentScan.SpectralStart,
                                    currentScan.SpectralEnd,
                                    Ah,
                                    Al,
                                    ref eobRun))
                                {
                                    Console.Error.WriteLine($"[PdfReader][JPEG] Progressive: AC refinement failed at block ({bx}, {by}), processed {blocksProcessed} blocks, eobRun={eobRun}");
                                    return false;
                                }
                            }
                        }

                        restartManager.DecrementRestartCounter();
                    }
                }
            }

            return true;
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
            for (int i = tables.Count - 1; i >= 0; i--)
            {
                if (tables[i].TableClass == tableClass && tables[i].TableId == id)
                {
                    return tables[i];
                }
            }

            return null;
        }
    }
}
