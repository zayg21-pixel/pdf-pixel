using PdfPixel.Imaging.Jpx.Model;
using PdfPixel.Text;
using System;
using System.Collections.Generic;
using System.IO;

namespace PdfPixel.Imaging.Jpx.Parsing;

/// <summary>
/// JPEG 2000 header reader that parses marker segments from the main header
/// and collects metadata needed for decoding. Supports both raw codestream
/// and JP2 file format wrapper parsing.
/// </summary>
internal static class JpxReader
{
    private const uint JP2_SIGNATURE_BOX_SIZE = 12;

    /// <summary>
    /// Parses JPEG 2000 header from the provided data.
    /// Automatically detects JP2 wrapper vs raw codestream format.
    /// </summary>
    public static JpxHeader ParseHeader(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8)
        {
            throw new InvalidDataException("Insufficient data for JPEG 2000 header.");
        }

        var header = new JpxHeader();

        // Check if this is a JP2 file (with box structure) or raw codestream
        if (IsJp2Format(data))
        {
            ParseJp2Format(data, header);
        }
        else
        {
            header.IsRawCodestream = true;
            ParseRawCodestream(data, 0, header);
        }

        return header;
    }

    /// <summary>
    /// Determines if the data represents JP2 format (with box structure).
    /// </summary>
    private static bool IsJp2Format(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12)
        {
            return false;
        }

        // Check for JP2 signature box: length=12, type="jP\0x20\0x20", data=0x0D0A870A
        uint boxLength = ReadUInt32BE(data);
        uint boxType = ReadUInt32BE(data.Slice(4));
        
        return boxLength == JP2_SIGNATURE_BOX_SIZE && 
               boxType == JpxMarkers.JPEG2000_SIGNATURE;
    }

    /// <summary>
    /// Parses JP2 file format with box structure.
    /// </summary>
    private static void ParseJp2Format(ReadOnlySpan<byte> data, JpxHeader header)
    {
        var reader = new JpxSpanReader(data);

        // Skip the signature box that was already validated in IsJp2Format
        reader.Skip(12); // JP2 signature box is always 12 bytes

        while (!reader.EndOfSpan)
        {
            if (reader.Remaining < 8)
            {
                break;
            }

            uint boxLength = reader.ReadUInt32BE();
            uint boxType = reader.ReadUInt32BE();

            uint contentLength;

            if (boxLength == 0)
            {
                // Box extends to the end of the file (ISO/IEC 15444-1 §I.2.2)
                contentLength = (uint)reader.Remaining;
            }
            else if (boxLength == 1)
            {
                // Extended 64-bit XLBox; read only lower 32 bits for simplicity
                if (reader.Remaining < 8)
                {
                    break;
                }

                reader.ReadUInt32BE(); // upper 32 bits (ignored; files this large are not supported)
                uint extendedLength = reader.ReadUInt32BE();
                contentLength = extendedLength - 16; // 16 = 4 (LBox) + 4 (TBox) + 8 (XLBox)
            }
            else
            {
                contentLength = boxLength - 8; // 8 = 4 (LBox) + 4 (TBox)
            }

            if (reader.Remaining < contentLength)
            {
                break;
            }

            switch (boxType)
            {
                case JpxMarkers.FILETYPE_BOX:
                    ParseFileTypeBox(reader.ReadBytes((int)contentLength), header);
                    break;

                case JpxMarkers.HEADER_BOX:
                    // jp2h is a superbox; parse its child boxes (ihdr, colr, etc.)
                    ParseJp2HeaderSuperBox(reader.ReadBytes((int)contentLength), header);
                    break;

                case JpxMarkers.CONTIGUOUS_CODESTREAM_BOX:
                    int codePosition = reader.Position;
                    header.CodestreamOffset = codePosition;
                    // Found the codestream - parse its header
                    ReadOnlySpan<byte> codestreamData = reader.ReadBytes((int)contentLength);
                    ParseRawCodestream(codestreamData, codePosition, header);
                    return; // We've found the codestream, we're done

                case JpxMarkers.COLOR_SPECIFICATION_BOX:
                    ParseColorSpecificationBox(reader.ReadBytes((int)contentLength), header);
                    break;

                default:
                    // Skip unknown boxes
                    reader.Skip((int)contentLength);
                    break;
            }
        }
    }

    /// <summary>
    /// Parses the JP2 Header superbox (jp2h) and its child boxes.
    /// Handles at minimum the Image Header Box (ihdr) and Color Specification Box (colr).
    /// </summary>
    private static void ParseJp2HeaderSuperBox(ReadOnlySpan<byte> data, JpxHeader header)
    {
        int offset = 0;

        while (offset + 8 <= data.Length)
        {
            uint subBoxLength = ReadUInt32BE(data.Slice(offset));
            uint subBoxType = ReadUInt32BE(data.Slice(offset + 4));

            uint subContentLength;

            if (subBoxLength == 0)
            {
                subContentLength = (uint)(data.Length - offset - 8);
            }
            else if (subBoxLength < 8)
            {
                // Malformed sub-box; stop parsing
                break;
            }
            else
            {
                subContentLength = subBoxLength - 8;
            }

            int subContentStart = offset + 8;

            if (subContentStart + subContentLength > data.Length)
            {
                break;
            }

            ReadOnlySpan<byte> subBoxContent = data.Slice(subContentStart, (int)subContentLength);

            switch (subBoxType)
            {
                case JpxMarkers.IMAGE_HEADER_BOX:
                    ParseImageHeaderBox(subBoxContent, header);
                    break;

                case JpxMarkers.COLOR_SPECIFICATION_BOX:
                    ParseColorSpecificationBox(subBoxContent, header);
                    break;

                case JpxMarkers.CHANNEL_DEFINITION_BOX:
                    ParseChannelDefinitionBox(subBoxContent, header);
                    break;

                default:
                    break; // Skip unrecognised child boxes
            }

            offset = subBoxLength == 0 ? data.Length : (int)(offset + subBoxLength);
        }
    }

    /// <summary>
    /// Parses the Image Header Box (ihdr) to extract image dimensions and component count.
    /// These values serve as a reliable fallback when the SIZ marker is not yet parsed.
    /// </summary>
    private static void ParseImageHeaderBox(ReadOnlySpan<byte> data, JpxHeader header)
    {
        // ihdr content: Height (4) + Width (4) + Components (2) + BPC (1) + Compression (1) + CSunk (1) + IPR (1) = 14 bytes
        if (data.Length < 14)
        {
            return;
        }

        uint height = ReadUInt32BE(data);
        uint width = ReadUInt32BE(data.Slice(4));
        ushort componentCount = (ushort)(data[8] << 8 | data[9]);

        // Set dimensions from ihdr; the SIZ segment (if parsed) will overwrite these
        // with the reference-grid values, which are authoritative for decoding.
        if (header.Width == 0)
        {
            header.Width = width;
        }

        if (header.Height == 0)
        {
            header.Height = height;
        }

        if (header.ComponentCount == 0)
        {
            header.ComponentCount = componentCount;
        }
    }

    /// <summary>
    /// Parses raw JPEG 2000 codestream (without JP2 wrapper).
    /// </summary>
    private static void ParseRawCodestream(ReadOnlySpan<byte> data, int initialOffset, JpxHeader header)
    {
        var reader = new JpxSpanReader(data);

        // Check for SOC marker
        ushort socMarker = reader.ReadUInt16BE();
        if (socMarker != JpxMarkers.SOC)
        {
            throw new InvalidDataException($"Expected SOC marker (0x{JpxMarkers.SOC:X4}), found 0x{socMarker:X4}.");
        }

        header.CodestreamOffset = initialOffset + 2; // After SOC marker

        // Parse main header segments until SOT or EOC
        while (!reader.EndOfSpan)
        {
            if (reader.Remaining < 2)
            {
                break;
            }

            ushort marker = reader.ReadUInt16BE();

            if (marker == JpxMarkers.SOT)
            {
                // Start of tile-part found, main header parsing complete
                header.CodestreamOffset = initialOffset + reader.Position - 2; // Include SOT marker
                break;
            }

            if (marker == JpxMarkers.EOC)
            {
                // End of codestream
                break;
            }

            // Parse marker segment
            ParseMarkerSegment(marker, ref reader, header);
        }
    }

    /// <summary>
    /// Parses a single marker segment.
    /// </summary>
    private static void ParseMarkerSegment(ushort marker, ref JpxSpanReader reader, JpxHeader header)
    {
        switch (marker)
        {
            case JpxMarkers.SIZ:
                ParseSizSegment(ref reader, header);
                break;

            case JpxMarkers.COD:
                ParseCodSegment(ref reader, header);
                break;

            case JpxMarkers.QCD:
                ParseQcdSegment(ref reader, header);
                break;

            case JpxMarkers.COC:
                ParseCocSegment(ref reader, header);
                break;

            case JpxMarkers.QCC:
                ParseQccSegment(ref reader, header);
                break;

            case JpxMarkers.COM:
                ParseComSegment(ref reader, header);
                break;

            default:
                // Skip unknown markers with length
                if (JpxMarkers.IsFunctionalMarker(marker) || 
                    JpxMarkers.IsInformationalMarker(marker) || 
                    JpxMarkers.IsPointerMarker(marker))
                {
                    SkipSegmentWithLength(ref reader);
                }
                break;
        }
    }

    /// <summary>
    /// Parses SIZ (Image and tile size) marker segment.
    /// </summary>
    private static void ParseSizSegment(ref JpxSpanReader reader, JpxHeader header)
    {
        ushort segmentLength = reader.ReadUInt16BE();
        
        header.Profile = reader.ReadUInt16BE();
        header.Width = reader.ReadUInt32BE();
        header.Height = reader.ReadUInt32BE();
        header.OriginX = reader.ReadUInt32BE();
        header.OriginY = reader.ReadUInt32BE();
        header.TileWidth = reader.ReadUInt32BE();
        header.TileHeight = reader.ReadUInt32BE();
        header.TileOriginX = reader.ReadUInt32BE();
        header.TileOriginY = reader.ReadUInt32BE();
        header.ComponentCount = reader.ReadUInt16BE();

        // Parse component parameters
        for (int i = 0; i < header.ComponentCount; i++)
        {
            var component = new JpxComponent
            {
                SamplePrecision = reader.ReadByte(),
                HorizontalSeparation = reader.ReadByte(),
                VerticalSeparation = reader.ReadByte()
            };
            header.Components.Add(component);
        }
    }

    /// <summary>
    /// Parses COD (Coding style default) marker segment.
    /// </summary>
    private static void ParseCodSegment(ref JpxSpanReader reader, JpxHeader header)
    {
        ushort segmentLength = reader.ReadUInt16BE();
        
        var codingStyle = new JpxCodingStyle
        {
            Style = reader.ReadByte(),
            ProgressionOrder = reader.ReadByte(),
            NumberOfLayers = reader.ReadUInt16BE(),
            MultiComponentTransform = reader.ReadByte(),
            DecompositionLevels = reader.ReadByte(),
            CodeBlockWidthExponent = reader.ReadByte(),
            CodeBlockHeightExponent = reader.ReadByte(),
            CodeBlockStyle = reader.ReadByte(),
            Transform = reader.ReadByte()
        };

        // Parse precinct size parameters only if explicitly present (JPEG2000 spec: Scod bit 0)
        int remainingBytes = segmentLength - 2 - 12; // 2 for length, 12 for fixed parameters
        bool hasPrecinctSizes = (codingStyle.Style & 0x01) != 0; // Check bit 0 of Scod
        
        if (hasPrecinctSizes && remainingBytes > 0)
        {
            // Each byte represents precinct sizes for one resolution level
            // Number of resolution levels = decomposition levels + 1
            int expectedPrecinctSizes = codingStyle.DecompositionLevels + 1;
            int precinctSizesToRead = Math.Min(remainingBytes, expectedPrecinctSizes);
            
            var precinctSizeExponents = new byte[precinctSizesToRead];
            for (int i = 0; i < precinctSizesToRead; i++)
            {
                precinctSizeExponents[i] = reader.ReadByte();
            }
            
            codingStyle.PrecinctSizeExponents = precinctSizeExponents;
            
            // Skip any additional bytes (shouldn't happen in well-formed streams)
            int extraBytes = remainingBytes - precinctSizesToRead;
            if (extraBytes > 0)
            {
                reader.Skip(extraBytes);
            }
        }
        else if (remainingBytes > 0)
        {
            // Precinct sizes not defined (bit 0 of Scod is 0), skip remaining bytes
            reader.Skip(remainingBytes);
        }

        header.CodingStyle = codingStyle;
    }

    /// <summary>
    /// Parses QCD (Quantization default) marker segment.
    /// </summary>
    private static void ParseQcdSegment(ref JpxSpanReader reader, JpxHeader header)
    {
        ushort segmentLength = reader.ReadUInt16BE();
        
        var quantization = new JpxQuantization
        {
            Style = reader.ReadByte()
        };

        int remainingBytes = segmentLength - 2 - 1; // 2 for length, 1 for style
        
        // Parse step sizes based on quantization type
        if (quantization.QuantizationType == 0)
        {
            // No quantization - step sizes are 8-bit exponents
            var stepSizes = new List<ushort>();
            for (int i = 0; i < remainingBytes; i++)
            {
                stepSizes.Add((ushort)(reader.ReadByte() << 8)); // Exponent in upper 8 bits
            }
            quantization.StepSizes = stepSizes.ToArray();
        }
        else
        {
            // Scalar quantization - step sizes are 16-bit values
            int numStepSizes = remainingBytes / 2;
            var stepSizes = new ushort[numStepSizes];
            for (int i = 0; i < numStepSizes; i++)
            {
                stepSizes[i] = reader.ReadUInt16BE();
            }
            quantization.StepSizes = stepSizes;
            
            // Skip any odd remaining byte
            if (remainingBytes % 2 == 1)
            {
                reader.Skip(1);
            }
        }

        header.Quantization = quantization;
    }

    /// <summary>
    /// Parses COC (Coding style component) marker segment.
    /// </summary>
    private static void ParseCocSegment(ref JpxSpanReader reader, JpxHeader header)
    {
        ushort segmentLength = reader.ReadUInt16BE();
        
        var componentCodingStyle = new JpxComponentCodingStyle();
        
        if (header.ComponentCount < 257)
        {
            componentCodingStyle.ComponentIndex = reader.ReadByte();
        }
        else
        {
            componentCodingStyle.ComponentIndex = reader.ReadUInt16BE();
        }

        var codingStyle = new JpxCodingStyle
        {
            Style = reader.ReadByte(),
            DecompositionLevels = reader.ReadByte(),
            CodeBlockWidthExponent = reader.ReadByte(),
            CodeBlockHeightExponent = reader.ReadByte(),
            CodeBlockStyle = reader.ReadByte(),
            Transform = reader.ReadByte()
        };

        // Parse precinct size parameters only if explicitly present (JPEG2000 spec: Scod bit 0)
        int consumedBytes = header.ComponentCount < 257 ? 7 : 8; // 1 or 2 for component + 6 for coding style
        int remainingBytes = segmentLength - 2 - consumedBytes;
        bool hasPrecinctSizes = (codingStyle.Style & 0x01) != 0; // Check bit 0 of Scod
        
        if (hasPrecinctSizes && remainingBytes > 0)
        {
            // Each byte represents precinct sizes for one resolution level
            int expectedPrecinctSizes = codingStyle.DecompositionLevels + 1;
            int precinctSizesToRead = Math.Min(remainingBytes, expectedPrecinctSizes);
            
            var precinctSizeExponents = new byte[precinctSizesToRead];
            for (int i = 0; i < precinctSizesToRead; i++)
            {
                precinctSizeExponents[i] = reader.ReadByte();
            }
            
            codingStyle.PrecinctSizeExponents = precinctSizeExponents;
            
            // Skip any additional bytes
            int extraBytes = remainingBytes - precinctSizesToRead;
            if (extraBytes > 0)
            {
                reader.Skip(extraBytes);
            }
        }
        else if (remainingBytes > 0)
        {
            // Precinct sizes not defined, skip remaining bytes
            reader.Skip(remainingBytes);
        }

        componentCodingStyle.CodingStyle = codingStyle;
        header.ComponentCodingStyles.Add(componentCodingStyle);
    }

    /// <summary>
    /// Parses QCC (Quantization component) marker segment.
    /// </summary>
    private static void ParseQccSegment(ref JpxSpanReader reader, JpxHeader header)
    {
        ushort segmentLength = reader.ReadUInt16BE();
        
        var componentQuantization = new JpxComponentQuantization();
        
        if (header.ComponentCount < 257)
        {
            componentQuantization.ComponentIndex = reader.ReadByte();
        }
        else
        {
            componentQuantization.ComponentIndex = reader.ReadUInt16BE();
        }

        var quantization = new JpxQuantization
        {
            Style = reader.ReadByte()
        };

        int consumedBytes = (header.ComponentCount < 257 ? 1 : 2) + 1; // Component index + style
        int remainingBytes = segmentLength - 2 - consumedBytes;
        
        // Parse step sizes (similar to QCD)
        if (quantization.QuantizationType == 0)
        {
            var stepSizes = new List<ushort>();
            for (int i = 0; i < remainingBytes; i++)
            {
                stepSizes.Add((ushort)(reader.ReadByte() << 8));
            }
            quantization.StepSizes = stepSizes.ToArray();
        }
        else
        {
            int numStepSizes = remainingBytes / 2;
            var stepSizes = new ushort[numStepSizes];
            for (int i = 0; i < numStepSizes; i++)
            {
                stepSizes[i] = reader.ReadUInt16BE();
            }
            quantization.StepSizes = stepSizes;
            
            if (remainingBytes % 2 == 1)
            {
                reader.Skip(1);
            }
        }

        componentQuantization.Quantization = quantization;
        header.ComponentQuantizations.Add(componentQuantization);
    }

    /// <summary>
    /// Parses COM (Comment) marker segment.
    /// </summary>
    private static void ParseComSegment(ref JpxSpanReader reader, JpxHeader header)
    {
        ushort segmentLength = reader.ReadUInt16BE();
        
        var comment = new JpxComment
        {
            Registration = reader.ReadUInt16BE()
        };

        int dataLength = segmentLength - 2 - 2; // Minus length field and registration field
        if (dataLength > 0)
        {
            comment.Data = reader.ReadBytes(dataLength).ToArray();
        }
        else
        {
            comment.Data = [];
        }

        header.Comments.Add(comment);
        header.HasComments = true;
    }

    /// <summary>
    /// Parses the Channel Definition box (cdef) to identify which components carry
    /// colour or alpha/opacity data (ISO/IEC 15444-1 Table I-11).
    /// </summary>
    private static void ParseChannelDefinitionBox(ReadOnlySpan<byte> data, JpxHeader header)
    {
        // cdef content: N (2 bytes) followed by N × 3 × 2-byte entries (i, typ, asoc)
        if (data.Length < 2)
        {
            return;
        }

        int channelCount = (data[0] << 8) | data[1];
        int requiredBytes = 2 + channelCount * 6;

        if (data.Length < requiredBytes)
        {
            return;
        }

        for (int i = 0; i < channelCount; i++)
        {
            int offset = 2 + i * 6;
            var definition = new JpxChannelDefinition
            {
                ComponentIndex = (ushort)((data[offset] << 8) | data[offset + 1]),
                ChannelType    = (ushort)((data[offset + 2] << 8) | data[offset + 3]),
                Association    = (ushort)((data[offset + 4] << 8) | data[offset + 5])
            };

            header.ChannelDefinitions.Add(definition);
        }
    }

    /// <summary>
    /// Parses color specification box from JP2 format.
    /// </summary>
    private static void ParseColorSpecificationBox(ReadOnlySpan<byte> data, JpxHeader header)
    {
        if (data.Length < 3)
        {
            return;
        }

        var colorSpec = new JpxColorSpecification
        {
            Method = data[0],
            Precedence = data[1],
            Approximation = data[2]
        };

        if (colorSpec.IsEnumerated && data.Length >= 7)
        {
            colorSpec.EnumeratedColorSpace = ReadUInt32BE(data.Slice(3));
        }
        else if ((colorSpec.IsRestrictedIcc || colorSpec.IsAnyIcc) && data.Length > 3)
        {
            colorSpec.IccProfile = data.Slice(3).ToArray();
        }

        header.ColorSpecifications.Add(colorSpec);
        header.HasColorSpecification = true;
    }

    /// <summary>
    /// Parses file type box from JP2 format.
    /// </summary>
    private static void ParseFileTypeBox(ReadOnlySpan<byte> data, JpxHeader header)
    {
        if (data.Length < 8)
        {
            return; // Need at least brand (4 bytes) + minor version (4 bytes)
        }

        // Extract brand (4 bytes)
        header.Brand = System.Text.Encoding.ASCII.GetString(data.Slice(0, 4));
        
        // Extract minor version (4 bytes, big-endian)
        header.MinorVersion = ReadUInt32BE(data.Slice(4));
        
        // Extract compatible brands (remaining bytes in groups of 4)
        header.CompatibleBrands.Clear();
        for (int i = 8; i < data.Length; i += 4)
        {
            if (i + 4 <= data.Length)
            {
                string compatibleBrand = System.Text.Encoding.ASCII.GetString(data.Slice(i, 4));
                string trimmedBrand = compatibleBrand.Trim('\0', ' ');
                if (!string.IsNullOrWhiteSpace(trimmedBrand))
                {
                    header.CompatibleBrands.Add(trimmedBrand);
                }
            }
        }
    }

    /// <summary>
    /// Skips a segment that has a length field.
    /// </summary>
    private static void SkipSegmentWithLength(ref JpxSpanReader reader)
    {
        if (reader.Remaining < 2)
        {
            return;
        }

        ushort segmentLength = reader.ReadUInt16BE();
        int skipBytes = segmentLength - 2; // Minus the length field itself
        
        if (skipBytes > 0 && reader.Remaining >= skipBytes)
        {
            reader.Skip(skipBytes);
        }
    }

    /// <summary>
    /// Reads a 32-bit big-endian unsigned integer from the span.
    /// </summary>
    private static uint ReadUInt32BE(ReadOnlySpan<byte> span)
    {
        return (uint)(span[0] << 24 | span[1] << 16 | span[2] << 8 | span[3]);
    }
}