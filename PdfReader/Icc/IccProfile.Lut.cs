using System;

namespace PdfReader.Icc
{
    /// <summary>
    /// Partial ICC profile implementation: parsing of LUT-based A2B pipelines (lut8/lut16, mAB).
    /// Keeps raw LUT data for evaluation elsewhere.
    /// </summary>
    internal sealed partial class IccProfile
    {
        // Overload used by IccProfile.Parse switch to follow same pattern
        internal static IccLutPipeline ParseA2BLut(BigEndianReader r, IccTagEntry tag)
        {
            if (r == null || tag == null) return null;
            uint type = r.ReadUInt32(tag.Offset);
            if (type == BigEndianReader.FourCC(IccConstants.TypeLut8))
            {
                return ParseLut8(r, tag.Offset, tag.Size);
            }
            if (type == BigEndianReader.FourCC(IccConstants.TypeLut16))
            {
                return ParseLut16(r, tag.Offset, tag.Size);
            }
            if (type == BigEndianReader.FourCC(IccConstants.TypeMAB))
            {
                return ParseMab(r, tag.Offset, tag.Size);
            }
            /*
             * TODO (mBA / B2A support – PCS → device direction):
             * - Parse B2A0/B2A1/B2A2 tags and recognize their types: lut8, lut16 and 'mBA ' (multi-process elements, reverse order).
             * - Build an IccLutPipeline for reverse transforms. For mBA the stage order is:
             *     B curves (per output channel in PCS) -> CLUT (per-dimension grid) -> optional Matrix3x3 + Offset -> A curves (per device channel).
             *   This is the mirror of mAB (A -> M -> CLUT -> M -> B) but in the BToA layout defined by ICC.
             * - Implement an evaluator similar to EvaluateMabToPcs, e.g. EvaluateMbaFromPcs, supporting both XYZ and Lab PCS.
             *   Honor PCS type from profile header (Lab/XYZ); clamp/normalize appropriately.
             * - Rendering intent selection: expose B2A0 (Perceptual), B2A1 (Relative), B2A2 (Saturation), and optionally Absolute fallback.
             * - Integration: only needed if the engine requires PCS→device (e.g., proofing, overprint simulation, pattern shading back-transforms).
             *   Our current device→sRGB path does not require B2A.
             * - Robustness: validate offsets/sizes, grid products, and precision; guard against overflows and OOB access.
             * - Performance: reuse small buffers; offer tetrahedral interpolation for 3D CLUTs as in A2B.
             */
            return null;
        }

        private static IccLutPipeline ParseLut8(BigEndianReader r, int off, int size)
        {
            int inCh = r.ReadByte(off + 8);
            int outCh = r.ReadByte(off + 9);
            int grid = r.ReadByte(off + 10);

            // Matrix 3x3 (s15Fixed16) stage
            var mat = new float[3, 3];
            int mpos = off + 12;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    int v = r.ReadInt32(mpos + (i * 3 + j) * 4);
                    mat[i, j] = BigEndianReader.S15Fixed16ToSingle(v);
                }
            }

            int cursor = mpos + 9 * 4;

            // Input tables: inCh * 256 bytes
            var inTables = new float[inCh][];
            for (int c = 0; c < inCh; c++)
            {
                inTables[c] = new float[256];
                for (int i = 0; i < 256; i++) inTables[c][i] = r.ReadByte(cursor + c * 256 + i) / 255f;
            }
            cursor += inCh * 256;

            // CLUT: outCh * grid^inCh bytes
            int gridTotal = 1;
            for (int d = 0; d < inCh; d++) gridTotal *= grid;
            int clutCount = gridTotal * outCh;
            var clut = new float[clutCount];
            for (int i = 0; i < clutCount; i++) clut[i] = r.ReadByte(cursor + i) / 255f;
            cursor += clutCount;

            // Output tables: outCh * 256 bytes
            var outTables = new float[outCh][];
            for (int c = 0; c < outCh; c++)
            {
                outTables[c] = new float[256];
                for (int i = 0; i < 256; i++) outTables[c][i] = r.ReadByte(cursor + c * 256 + i) / 255f;
            }

            var p = new IccLutPipeline(inCh, outCh, grid, inTables, clut, outTables)
            {
                Matrix3x3 = inCh == 3 ? mat : null
            };
            p.GridPointsPerDim = new int[inCh];
            for (int i = 0; i < inCh; i++) p.GridPointsPerDim[i] = grid;
            p.ClutPrecisionBytes = 1;
            return p;
        }

        private static IccLutPipeline ParseLut16(BigEndianReader r, int off, int size)
        {
            int inCh = r.ReadByte(off + 8);
            int outCh = r.ReadByte(off + 9);
            int grid = r.ReadByte(off + 10);

            // Matrix 3x3 (s15Fixed16)
            var mat = new float[3, 3];
            int mpos = off + 12;
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    int v = r.ReadInt32(mpos + (i * 3 + j) * 4);
                    mat[i, j] = BigEndianReader.S15Fixed16ToSingle(v);
                }
            }

            int cursor = mpos + 9 * 4;
            int inEntries = r.ReadUInt16(cursor); // uInt16
            int outEntries = r.ReadUInt16(cursor + 2);
            cursor += 4;

            // Input tables
            var inTables = new float[inCh][];
            for (int c = 0; c < inCh; c++)
            {
                var t = new float[inEntries];
                for (int i = 0; i < inEntries; i++) t[i] = r.ReadUInt16(cursor + (c * inEntries + i) * 2) / 65535f;
                inTables[c] = t;
            }
            cursor += inCh * inEntries * 2;

            // CLUT
            int gridTotal = 1;
            for (int d = 0; d < inCh; d++) gridTotal *= grid;
            int clutCount = gridTotal * outCh;
            var clut = new float[clutCount];
            for (int i = 0; i < clutCount; i++) clut[i] = r.ReadUInt16(cursor + i * 2) / 65535f;
            cursor += clutCount * 2;

            // Output tables
            var outTables = new float[outCh][];
            for (int c = 0; c < outCh; c++)
            {
                var t = new float[outEntries];
                for (int i = 0; i < outEntries; i++) t[i] = r.ReadUInt16(cursor + (c * outEntries + i) * 2) / 65535f;
                outTables[c] = t;
            }

            var p = new IccLutPipeline(inCh, outCh, grid, inTables, clut, outTables)
            {
                Matrix3x3 = inCh == 3 ? mat : null
            };
            p.GridPointsPerDim = new int[inCh];
            for (int i = 0; i < inCh; i++) p.GridPointsPerDim[i] = grid;
            p.ClutPrecisionBytes = 2;
            return p;
        }

        private static IccLutPipeline ParseMab(BigEndianReader r, int off, int size)
        {
            int inCh = r.ReadByte(off + 8);
            int outCh = r.ReadByte(off + 9);
            // numGrid is nominal; actual per-dim grid sizes stored in CLUT block
            // Offsets (u32) from start of tag
            uint offB = r.ReadUInt32(off + 12);
            uint offMatrix = r.ReadUInt32(off + 16);
            uint offM = r.ReadUInt32(off + 20);
            uint offClut = r.ReadUInt32(off + 24);
            uint offA = r.ReadUInt32(off + 28);

            IccTrc[] curvesA = null, curvesB = null, curvesM = null;
            if (offA != 0)
            {
                curvesA = ParseCurveSequence(r, off + (int)offA, inCh);
            }
            if (offB != 0)
            {
                curvesB = ParseCurveSequence(r, off + (int)offB, outCh);
            }
            if (offM != 0)
            {
                curvesM = ParseCurveSequence(r, off + (int)offM, outCh);
            }

            // CLUT: per-dim grid and precision
            int clutPos = off + (int)offClut;
            int[] gridPerDim = new int[inCh];
            for (int i = 0; i < inCh; i++) gridPerDim[i] = r.ReadByte(clutPos + i);
            int prec = r.ReadByte(clutPos + inCh); // 1 or 2
            int clutDataPos = clutPos + inCh + 1;
            // align to next 4-byte boundary
            int pad = (4 - (clutDataPos & 3)) & 3;
            clutDataPos += pad;

            long gridTotal = 1;
            for (int i = 0; i < inCh; i++) gridTotal *= gridPerDim[i] <= 0 ? 1 : gridPerDim[i];
            long clutCount = gridTotal * outCh;
            if (clutCount <= 0 || clutCount > int.MaxValue / Math.Max(1, prec)) return null;
            var clut = new float[(int)clutCount];
            if (prec == 1)
            {
                for (int i = 0; i < clut.Length; i++) clut[i] = r.ReadByte(clutDataPos + i) / 255f;
            }
            else
            {
                for (int i = 0; i < clut.Length; i++) clut[i] = r.ReadUInt16(clutDataPos + i * 2) / 65535f;
            }

            // Matrix (optional): 3x3 + 3 offsets (s15Fixed16)
            float[,] mat = null;
            float[] mOffset = null;
            if (offMatrix != 0)
            {
                int mpos = off + (int)offMatrix;
                mat = new float[3, 3];
                for (int i = 0; i < 3; i++)
                {
                    for (int j = 0; j < 3; j++)
                    {
                        int v = r.ReadInt32(mpos + (i * 3 + j) * 4);
                        mat[i, j] = BigEndianReader.S15Fixed16ToSingle(v);
                    }
                }
                mOffset = new float[3];
                for (int i = 0; i < 3; i++)
                {
                    int v = r.ReadInt32(mpos + 9 * 4 + i * 4);
                    mOffset[i] = BigEndianReader.S15Fixed16ToSingle(v);
                }
            }

            var p = IccLutPipeline.CreateMab(inCh, outCh, gridPerDim, (byte)(prec == 1 ? 1 : 2), curvesA, curvesM, curvesB, clut, mat, mOffset);
            return p;
        }

        private static IccTrc[] ParseCurveSequence(BigEndianReader r, int pos, int count)
        {
            var list = new IccTrc[count];
            int cursor = pos;
            for (int i = 0; i < count; i++)
            {
                list[i] = ReadCurveAt(r, cursor, out int size);
                // advance to next 4-byte boundary after this curve
                int next = cursor + size;
                int pad = (4 - (next & 3)) & 3;
                cursor = next + pad;
            }
            return list;
        }

        private static IccTrc ReadCurveAt(BigEndianReader r, int pos, out int size)
        {
            size = 0;
            uint type = r.ReadUInt32(pos);
            // curveType 'curv'
            if (type == BigEndianReader.FourCC(IccConstants.TypeCurv))
            {
                uint count = r.ReadUInt32(pos + 8);
                if (count == 1)
                {
                    ushort u8f8 = r.ReadUInt16(pos + 12);
                    size = 12 + 2; // 14 bytes + padding handled by caller
                    return IccTrc.FromGamma(BigEndianReader.U8Fixed8ToSingle(u8f8));
                }
                else
                {
                    // sampled curves not supported yet; skip over data
                    size = 12 + (int)count * 2;
                    return IccTrc.Sampled((int)count);
                }
            }
            // parametricCurveType 'para'
            if (type == BigEndianReader.FourCC(IccConstants.TypePara))
            {
                ushort funcType = r.ReadUInt16(pos + 8);
                int paramCount = GetParamCount(funcType);
                size = 12 + paramCount * 4;
                if (funcType == 0 && paramCount >= 1)
                {
                    int g = r.ReadInt32(pos + 12);
                    return IccTrc.FromGamma(BigEndianReader.S15Fixed16ToSingle(g));
                }
                return IccTrc.UnsupportedParametric(funcType);
            }
            // Unknown: treat as unsupported, try to skip minimal header
            size = 12;
            return IccTrc.UnsupportedParametric(-1);
        }
    }

    /// <summary>
    /// Parsed LUT pipeline for A2B (CMYK -> PCS) or similar. Evaluation is handled elsewhere.
    /// </summary>
    internal sealed class IccLutPipeline
    {
        public int InChannels { get; }
        public int OutChannels { get; }
        public int GridPoints { get; } // for lut8/16 uniform grids
        public int[] GridPointsPerDim { get; set; } // for mAB per-dimension grids
        public float[][] InputTables { get; } // lut8/16 input tables
        public float[] Clut { get; }
        public float[][] OutputTables { get; } // lut8/16 output tables
        public byte ClutPrecisionBytes { get; set; }

        // mAB specific and shared matrix (lut types use 3x3 without offset)
        public bool IsMab { get; private set; }
        public IccTrc[] CurvesA { get; private set; }
        public IccTrc[] CurvesM { get; private set; }
        public IccTrc[] CurvesB { get; private set; }
        public float[,] Matrix3x3 { get; set; }
        public float[] MatrixOffset { get; private set; }

        public IccLutPipeline(int inCh, int outCh, int grid, float[][] inTables, float[] clut, float[][] outTables)
        {
            InChannels = inCh;
            OutChannels = outCh;
            GridPoints = grid;
            InputTables = inTables;
            Clut = clut;
            OutputTables = outTables;
            IsMab = false;
        }

        public static IccLutPipeline CreateMab(int inCh, int outCh, int[] gridPerDim, byte precisionBytes, IccTrc[] a, IccTrc[] m, IccTrc[] b, float[] clut, float[,] matrix, float[] offset)
        {
            var p = new IccLutPipeline(inCh, outCh, 0, null, clut, null)
            {
                GridPointsPerDim = gridPerDim,
                ClutPrecisionBytes = precisionBytes,
                CurvesA = a,
                CurvesM = m,
                CurvesB = b,
                Matrix3x3 = matrix,
                MatrixOffset = offset,
                IsMab = true
            };
            return p;
        }
    }
}
