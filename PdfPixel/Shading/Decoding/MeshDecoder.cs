using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Sampling;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Rendering.State;
using PdfPixel.Shading.Model;
using PdfPixel.Text;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;

namespace PdfPixel.Shading.Decoding;

/// <summary>
/// Provides decoding of PDF mesh shading patches, extracting normalized control points and corner colors.
/// Supports both Coons patch mesh (Type 6) and Tensor-product patch mesh (Type 7).
/// </summary>
class MeshDecoder
{
    private readonly PdfShading _shading;
    private readonly IRgbaSampler _sampler;
    private readonly int _bitsPerFlag;
    private readonly int _bitsPerCoordinate;
    private readonly int _bitsPerComponent;
    private readonly int _numColorComponents;
    private readonly int _controlPointCount;
    private readonly float _componentDenominator;
    private readonly float _coordinateDenominator;
    private readonly float _xmin, _ymin;
    private readonly float _xScale;
    private readonly float _yScale;
    private readonly ColorMinAndScale[] _colorComponentMinAndScale;

    public MeshDecoder(PdfShading shading, PdfGraphicsState state)
    {
        if (shading.ShadingType != 6 &&  shading.ShadingType != 7)
        {
            throw new ArgumentException($"Not supported shading type {shading.ShadingType}");
        }

        _shading = shading;
        var converter = state.Page.Cache.ColorSpace.ResolveByObject(shading.ColorSpaceConverter);
        _sampler = converter.GetRgbaSampler(state.RenderingIntent, state.FullTransferFunction);
        PdfDictionary shadingDictionary = shading.SourceObject.Dictionary;

        _bitsPerCoordinate = shadingDictionary.GetIntegerOrDefault(PdfTokens.BitsPerCoordinateKey);
        _bitsPerComponent = shadingDictionary.GetIntegerOrDefault(PdfTokens.BitsPerComponentKey);
        _bitsPerFlag = shadingDictionary.GetIntegerOrDefault(PdfTokens.BitsPerFlagKey);
        float[] decodeArray = shadingDictionary.GetArray(PdfTokens.DecodeKey).GetFloatArray();
        if (decodeArray == null || decodeArray.Length < 6 || (decodeArray.Length - 4) % 2 != 0)
        {
            throw new ArgumentException("Decode array must contain at least xmin,xmax,ymin,ymax and pairs of min/max for each color component");
        }
        _numColorComponents = (decodeArray.Length - 4) / 2;
        _xmin = decodeArray[0];
        float xmax = decodeArray[1];
        _ymin = decodeArray[2];
        float ymax = decodeArray[3];
        float xRange = xmax - _xmin;
        float yRange = ymax - _ymin;
        _controlPointCount = shading.ShadingType == 7 ? 16 : 12;
        _componentDenominator = 1f / ((1UL << _bitsPerComponent) - 1);
        _coordinateDenominator = 1f / ((1UL << _bitsPerCoordinate) - 1);
        _xScale = _coordinateDenominator * xRange;
        _yScale = _coordinateDenominator * yRange;

        // Precompute min and pre-multiplied scale for each color component
        _colorComponentMinAndScale = new ColorMinAndScale[_numColorComponents];
        for (int componentIndex = 0; componentIndex < _numColorComponents; componentIndex++)
        {
            float minValue = decodeArray[4 + componentIndex * 2];
            float maxValue = decodeArray[4 + componentIndex * 2 + 1];
            float scalePremultiplied = _componentDenominator * (maxValue - minValue);
            _colorComponentMinAndScale[componentIndex] = new ColorMinAndScale(minValue, scalePremultiplied);
        }
    }

    /// <summary>
    /// Decodes all mesh patches from the shading stream, returning normalized control points and corner colors.
    /// </summary>
    /// <returns>List of decoded <see cref="MeshData"/> instances.</returns>
    public List<MeshData> Decode()
    {
        var memory = _shading.SourceObject.DecodeAsMemory();
        var bitReader = new UintBitReader(memory.Span);
        var patches = new List<MeshData>();
        MeshData previousPatch = null;

        while (!bitReader.EndOfData)
        {
            int rawFlag = (int)bitReader.ReadBits(_bitsPerFlag);
            byte flag = (byte)(rawFlag & 0x3);

            if (rawFlag != flag)
            {
                throw new InvalidOperationException("Invalid flag for mesh parsing");
            }

            MeshData patch = DecodeMesh(ref bitReader, flag, previousPatch);
            patches.Add(patch);
            previousPatch = patch;
        }

        return patches;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MeshData DecodeMesh(ref UintBitReader bitReader, byte flag, MeshData previousPatch)
    {
        SKPoint[] controlPoints = new SKPoint[_controlPointCount];
        SKColor[] cornerColors = new SKColor[4];

        if (flag == 0)
        {
            for (int i = 0; i < _controlPointCount; i++)
            {
                controlPoints[i] = MeshReader.ReadPoint(ref bitReader, _bitsPerCoordinate, _xmin, _ymin, _xScale, _yScale);
            }
            for (int c = 0; c < 4; c++)
            {
                cornerColors[c] = MeshReader.ReadColorComponents(ref bitReader, _bitsPerComponent, _colorComponentMinAndScale, _numColorComponents, _shading.Functions, _sampler);
            }
        }
        else
        {
            if (previousPatch == null)
            {
                throw new InvalidDataException("Nonzero flag with no previous patch.");
            }
            for (int i = 4; i < _controlPointCount; i++)
            {
                controlPoints[i] = MeshReader.ReadPoint(ref bitReader, _bitsPerCoordinate, _xmin, _ymin, _xScale, _yScale);
            }
            cornerColors[2] = MeshReader.ReadColorComponents(ref bitReader, _bitsPerComponent, _colorComponentMinAndScale, _numColorComponents, _shading.Functions, _sampler);
            cornerColors[3] = MeshReader.ReadColorComponents(ref bitReader, _bitsPerComponent, _colorComponentMinAndScale, _numColorComponents, _shading.Functions, _sampler);
            switch (flag)
            {
                case 1:
                    controlPoints[0] = previousPatch.Points[3];
                    controlPoints[1] = previousPatch.Points[4];
                    controlPoints[2] = previousPatch.Points[5];
                    controlPoints[3] = previousPatch.Points[6];
                    cornerColors[0] = previousPatch.CornerColors[1];
                    cornerColors[1] = previousPatch.CornerColors[2];
                    break;
                case 2:
                    controlPoints[0] = previousPatch.Points[6];
                    controlPoints[1] = previousPatch.Points[7];
                    controlPoints[2] = previousPatch.Points[8];
                    controlPoints[3] = previousPatch.Points[9];
                    cornerColors[0] = previousPatch.CornerColors[2];
                    cornerColors[1] = previousPatch.CornerColors[3];
                    break;
                case 3:
                    controlPoints[0] = previousPatch.Points[9];
                    controlPoints[1] = previousPatch.Points[10];
                    controlPoints[2] = previousPatch.Points[11];
                    controlPoints[3] = previousPatch.Points[0];
                    cornerColors[0] = previousPatch.CornerColors[3];
                    cornerColors[1] = previousPatch.CornerColors[0];
                    break;
            }
        }

        // PDF spec: Each patch record must be padded to the next byte boundary.
        // If not byte-aligned, skip the remaining bits in the current byte.
        if (!bitReader.IsByteAligned)
        {
            int bitsToPad = 8 - bitReader.BitPosition % 8;
            bitReader.ReadBits(bitsToPad);
        }

        return new MeshData(controlPoints, cornerColors, flag);
    }
}