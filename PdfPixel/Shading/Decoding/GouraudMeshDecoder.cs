using PdfPixel.Color.Sampling;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Shading.Model;
using PdfPixel.Text;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfPixel.Shading.Decoding;

/// <summary>
/// Provides decoding of PDF Gouraud-shaded triangle meshes (Type 4 and Type 5).
/// Extracts triangle vertices and their associated colors from the shading stream.
/// </summary>
internal class GouraudMeshDecoder
{
    private readonly PdfShading _shading;
    private readonly ColorTransformSampler _sampler;
    private readonly int _bitsPerFlag;
    private readonly int _bitsPerCoordinate;
    private readonly int _bitsPerComponent;
    private readonly int _numColorComponents;
    private readonly bool _readFlag;
    private readonly float _xmin;
    private readonly float _ymin;
    private readonly float _xScale;
    private readonly float _yScale;
    private readonly ColorMinAndScale[] _colorComponentMinAndScale;
    private readonly int _verticesPerRow;

    public GouraudMeshDecoder(PdfShading shading, ColorTransformSampler sampler)
    {
        if (shading.ShadingType != PdfShadingType.FreeFormGouraud && shading.ShadingType != PdfShadingType.LatticeFormGouraud)
        {
            throw new ArgumentException($"Not supported shading type {shading.ShadingType}");
        }

        _shading = shading;
        _sampler = sampler;
        PdfDictionary shadingDictionary = shading.SourceObject.Dictionary;

        _bitsPerFlag = shadingDictionary.GetIntegerOrDefault(PdfTokens.BitsPerFlagKey);
        _bitsPerCoordinate = shadingDictionary.GetIntegerOrDefault(PdfTokens.BitsPerCoordinateKey);
        _bitsPerComponent = shadingDictionary.GetIntegerOrDefault(PdfTokens.BitsPerComponentKey);
        float[]? decodeArray = shadingDictionary.GetArray(PdfTokens.DecodeKey)?.GetFloatArray();
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
        float componentDenominator = 1f / ((1UL << _bitsPerComponent) - 1);
        float coordinateDenominator = 1f / ((1UL << _bitsPerCoordinate) - 1);
        _xScale = coordinateDenominator * xRange;
        _yScale = coordinateDenominator * yRange;

        _readFlag = shading.ShadingType == PdfShadingType.FreeFormGouraud;

        if (shading.ShadingType == PdfShadingType.LatticeFormGouraud)
        {
            _verticesPerRow = shadingDictionary.GetIntegerOrDefault(PdfTokens.VerticesPerRowKey);
            if (_verticesPerRow < 2)
            {
                throw new ArgumentException("VerticesPerRow must be at least 2 for Type 5 shading.");
            }
        }

        // Precompute min and pre-multiplied scale for each color component
        _colorComponentMinAndScale = new ColorMinAndScale[_numColorComponents];
        for (int componentIndex = 0; componentIndex < _numColorComponents; componentIndex++)
        {
            float minValue = decodeArray[4 + (componentIndex * 2)];
            float maxValue = decodeArray[4 + (componentIndex * 2) + 1];
            float scalePremultiplied = componentDenominator * (maxValue - minValue);
            _colorComponentMinAndScale[componentIndex] = new ColorMinAndScale(minValue, scalePremultiplied);
        }
    }

    /// <summary>
    /// Decodes all triangles from the shading stream, returning each as a MeshData (3 points, 3 colors).
    /// </summary>
    /// <returns>List of decoded MeshData instances.</returns>
    public List<MeshData> Decode()
    {
        ReadOnlyMemory<byte> memory = _shading.SourceObject.DecodeAsMemory();
        UintBitReader bitReader = new(memory.Span);

        if (_readFlag)
        {
            return ReadType4(ref bitReader);
        }
        else
        {
            return ReadType5(ref bitReader);
        }
    }


    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private List<MeshData> ReadType4(ref UintBitReader bitReader)
    {
        List<MeshData> patches = [];
        MeshData? previous = null;

        while (!bitReader.EndOfData)
        {
            SKPoint[]? previousVertices = previous?.Points;
            SKColor[]? previousColors = previous?.CornerColors;

            var vertices = new SKPoint[3];
            var colors = new SKColor[3];
            uint flag = bitReader.ReadBits(_bitsPerFlag);

            if (flag == 0 || previousVertices == null)
            {
                // Flag 0: read all three vertices/colors, ignore flags for vb and vc
                for (int i = 0; i < 3; i++)
                {
                    if (i > 0)
                    {
                        bitReader.ReadBits(_bitsPerFlag); // Ignore edge flag for vb and vc
                    }

                    vertices[i] = MeshReader.ReadPoint(ref bitReader, _bitsPerCoordinate, _xmin, _ymin, _xScale, _yScale);
                    colors[i] = MeshReader.ReadColorComponents(ref bitReader, _bitsPerComponent, _colorComponentMinAndScale, _numColorComponents, _shading.Functions, _sampler);
                    // Skip padding bits for each vertex
                    bitReader.AlignToNextByte();
                }
            }
            else if (flag == 1 && previousColors != null)
            {
                // Flag 1: copy last two vertices/colors from previous triangle, read one new
                vertices[0] = previousVertices[1];
                vertices[1] = previousVertices[2];
                colors[0] = previousColors[1];
                colors[1] = previousColors[2];
                vertices[2] = MeshReader.ReadPoint(ref bitReader, _bitsPerCoordinate, _xmin, _ymin, _xScale, _yScale);
                colors[2] = MeshReader.ReadColorComponents(ref bitReader, _bitsPerComponent, _colorComponentMinAndScale, _numColorComponents, _shading.Functions, _sampler);
                // Skip padding bits for the new vertex
                bitReader.AlignToNextByte();
            }
            else if (flag == 2 && previousColors != null)
            {
                // Flag 2: copy first and last vertices/colors from previous triangle, read one new
                vertices[0] = previousVertices[0];
                vertices[2] = previousVertices[2];
                colors[0] = previousColors[0];
                colors[2] = previousColors[2];
                vertices[1] = MeshReader.ReadPoint(ref bitReader, _bitsPerCoordinate, _xmin, _ymin, _xScale, _yScale);
                colors[1] = MeshReader.ReadColorComponents(ref bitReader, _bitsPerComponent, _colorComponentMinAndScale, _numColorComponents, _shading.Functions, _sampler);
                // Skip padding bits for the new vertex
                bitReader.AlignToNextByte();
            }
            else
            {
                // Invalid flag value
                throw new InvalidOperationException($"Invalid Gouraud mesh triangle flag: {flag}");
            }

            MeshData meshData = new(vertices, colors, flag);
            patches.Add(meshData);
            previous = meshData;
        }

        return patches;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private List<MeshData> ReadType5(ref UintBitReader bitReader)
    {
        List<(SKPoint point, SKColor color)> vertexList = [];
        while (!bitReader.EndOfData)
        {
            SKPoint point = MeshReader.ReadPoint(ref bitReader, _bitsPerCoordinate, _xmin, _ymin, _xScale, _yScale);
            SKColor color = MeshReader.ReadColorComponents(ref bitReader, _bitsPerComponent, _colorComponentMinAndScale, _numColorComponents, _shading.Functions, _sampler);

            bitReader.AlignToNextByte();

            vertexList.Add((point, color));
        }

        List<MeshData> patches = [];
        int rowCount = vertexList.Count / _verticesPerRow;
        for (int rowIndex = 0; rowIndex < rowCount - 1; rowIndex++)
        {
            for (int columnIndex = 0; columnIndex < _verticesPerRow - 1; columnIndex++)
            {
                int idx0 = (rowIndex * _verticesPerRow) + columnIndex;
                int idx1 = idx0 + 1;
                int idx2 = idx0 + _verticesPerRow;
                int idx3 = idx2 + 1;

                // Triangle 1
                MeshData triangle1 = new(
                    [vertexList[idx0].point, vertexList[idx1].point, vertexList[idx2].point],
                    [vertexList[idx0].color, vertexList[idx1].color, vertexList[idx2].color],
                    0);
                patches.Add(triangle1);

                // Triangle 2
                MeshData triangle2 = new(
                    [vertexList[idx1].point, vertexList[idx3].point, vertexList[idx2].point],
                    [vertexList[idx1].color, vertexList[idx3].color, vertexList[idx2].color],
                    0);
                patches.Add(triangle2);
            }
        }

        return patches;
    }
}
