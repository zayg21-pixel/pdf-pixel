namespace PdfReader.Icc
{
    /// <summary>
    /// Partial ICC profile implementation: CMYK/LUT tag readers (minimal metadata for PDF use).
    /// We do not implement full LUT pipelines; we record structure so the renderer can choose strategies.
    /// </summary>
    internal sealed partial class IccProfile
    {
        internal static IccLutAToB ReadAToB(BigEndianReader r, IccTagEntry tag)
        {
            if (!HasMin(r, tag, 12)) return null;
            uint type = r.ReadUInt32(tag.Offset + 0);
            // Recognize classic LUT8/LUT16 and v4 mAB type ('mAB ')
            if (type == BigEndianReader.FourCC(IccConstants.TypeMAB))
            {
                return ReadMabType(r, tag);
            }
            if (type == BigEndianReader.FourCC(IccConstants.TypeLut8))
            {
                var meta = ReadLut8Type(r, tag, isAToB: true);
                return meta == null ? null : new IccLutAToB(meta.InChannels, meta.OutChannels, meta.GridPoints, meta.HasCurvesA, meta.HasMatrix, meta.HasCurvesM, meta.HasClut, meta.HasCurvesB)
                {
                    TagType = meta.TagType,
                    IsAToB = true
                };
            }
            if (type == BigEndianReader.FourCC(IccConstants.TypeLut16))
            {
                var meta = ReadLut16Type(r, tag, isAToB: true);
                return meta == null ? null : new IccLutAToB(meta.InChannels, meta.OutChannels, meta.GridPoints, meta.HasCurvesA, meta.HasMatrix, meta.HasCurvesM, meta.HasClut, meta.HasCurvesB)
                {
                    TagType = meta.TagType,
                    IsAToB = true
                };
            }
            // TODO: handle 'clut' type in v5 if encountered
            return null;
        }

        internal static IccLutBToA ReadBToA(BigEndianReader r, IccTagEntry tag)
        {
            if (!HasMin(r, tag, 12)) return null;
            uint type = r.ReadUInt32(tag.Offset + 0);
            if (type == BigEndianReader.FourCC(IccConstants.TypeMBA))
            {
                return ReadMbaType(r, tag);
            }
            if (type == BigEndianReader.FourCC(IccConstants.TypeLut8))
            {
                var meta = ReadLut8Type(r, tag, isAToB: false);
                return meta == null ? null : new IccLutBToA(meta.InChannels, meta.OutChannels, meta.GridPoints, meta.HasCurvesA, meta.HasMatrix, meta.HasCurvesM, meta.HasClut, meta.HasCurvesB)
                {
                    TagType = meta.TagType,
                    IsAToB = false
                };
            }
            if (type == BigEndianReader.FourCC(IccConstants.TypeLut16))
            {
                var meta = ReadLut16Type(r, tag, isAToB: false);
                return meta == null ? null : new IccLutBToA(meta.InChannels, meta.OutChannels, meta.GridPoints, meta.HasCurvesA, meta.HasMatrix, meta.HasCurvesM, meta.HasClut, meta.HasCurvesB)
                {
                    TagType = meta.TagType,
                    IsAToB = false
                };
            }
            return null;
        }

        private static bool HasMin(BigEndianReader r, IccTagEntry tag, int minSize)
        {
            try { r.Ensure(tag.Offset, minSize); return true; } catch { return false; }
        }

        private static IccLutAToB ReadMabType(BigEndianReader r, IccTagEntry tag)
        {
            // mABType per ICC v4: type(4), reserved(4), numIn(1), numOut(1), numGrid(1), reserved(1), offsets to A, M, CLUT, B curves
            if (!HasMin(r, tag, 32)) return null;
            byte inCh = r.ReadByte(tag.Offset + 8);
            byte outCh = r.ReadByte(tag.Offset + 9);
            byte grid = r.ReadByte(tag.Offset + 10);
            // offsets (u32) from start of tag
            uint bCurves = r.ReadUInt32(tag.Offset + 12);
            uint matrix = r.ReadUInt32(tag.Offset + 16);
            uint mCurves = r.ReadUInt32(tag.Offset + 20);
            uint clut = r.ReadUInt32(tag.Offset + 24);
            uint aCurves = r.ReadUInt32(tag.Offset + 28);

            bool hasA = aCurves != 0;
            bool hasM = mCurves != 0;
            bool hasMat = matrix != 0;
            bool hasClut = clut != 0;
            bool hasB = bCurves != 0;
            return new IccLutAToB(inCh, outCh, grid, hasA, hasMat, hasM, hasClut, hasB) { TagType = IccConstants.TypeMAB, IsAToB = true };
        }

        private static IccLutBToA ReadMbaType(BigEndianReader r, IccTagEntry tag)
        {
            // mBAType is symmetric to mAB but with offsets in BA order
            if (!HasMin(r, tag, 32)) return null;
            byte inCh = r.ReadByte(tag.Offset + 8);
            byte outCh = r.ReadByte(tag.Offset + 9);
            byte grid = r.ReadByte(tag.Offset + 10);

            uint aCurves = r.ReadUInt32(tag.Offset + 12);
            uint matrix = r.ReadUInt32(tag.Offset + 16);
            uint mCurves = r.ReadUInt32(tag.Offset + 20);
            uint clut = r.ReadUInt32(tag.Offset + 24);
            uint bCurves = r.ReadUInt32(tag.Offset + 28);

            bool hasA = aCurves != 0;
            bool hasM = mCurves != 0;
            bool hasMat = matrix != 0;
            bool hasClut = clut != 0;
            bool hasB = bCurves != 0;
            return new IccLutBToA(inCh, outCh, grid, hasA, hasMat, hasM, hasClut, hasB) { TagType = IccConstants.TypeMBA, IsAToB = false };
        }

        private static IccLutMeta ReadLut8Type(BigEndianReader r, IccTagEntry tag, bool isAToB)
        {
            // lut8: type(4) 'lut8', reserved(4), inCh(1), outCh(1), gridPoints(1), reserved(1)
            if (!HasMin(r, tag, 12)) return null;
            byte inCh = r.ReadByte(tag.Offset + 8);
            byte outCh = r.ReadByte(tag.Offset + 9);
            byte grid = r.ReadByte(tag.Offset + 10);
            // Then: 3x3 matrix (s15Fixed16), then 3*inCh input tables (uInt8), CLUT, 3*outCh output tables (uInt8)
            // We do not parse actual tables; only record existence.
            return new IccLutMeta(inCh, outCh, grid, hasCurvesA: true, hasMatrix: true, hasCurvesM: false, hasClut: true, hasCurvesB: true)
            {
                TagType = IccConstants.TypeLut8,
                IsAToB = isAToB
            };
        }

        private static IccLutMeta ReadLut16Type(BigEndianReader r, IccTagEntry tag, bool isAToB)
        {
            // lut16 is similar to lut8 but uInt16 tables
            if (!HasMin(r, tag, 12)) return null;
            byte inCh = r.ReadByte(tag.Offset + 8);
            byte outCh = r.ReadByte(tag.Offset + 9);
            byte grid = r.ReadByte(tag.Offset + 10);
            return new IccLutMeta(inCh, outCh, grid, hasCurvesA: true, hasMatrix: true, hasCurvesM: false, hasClut: true, hasCurvesB: true)
            {
                TagType = IccConstants.TypeLut16,
                IsAToB = isAToB
            };
        }
    }

    internal class IccLutMeta
    {
        public byte InChannels { get; }
        public byte OutChannels { get; }
        public byte GridPoints { get; }
        public bool HasCurvesA { get; }
        public bool HasMatrix { get; }
        public bool HasCurvesM { get; }
        public bool HasClut { get; }
        public bool HasCurvesB { get; }
        public string TagType { get; set; }
        public bool IsAToB { get; set; }

        public IccLutMeta(byte inCh, byte outCh, byte gridPoints, bool hasCurvesA, bool hasMatrix, bool hasCurvesM, bool hasClut, bool hasCurvesB)
        {
            InChannels = inCh;
            OutChannels = outCh;
            GridPoints = gridPoints;
            HasCurvesA = hasCurvesA;
            HasMatrix = hasMatrix;
            HasCurvesM = hasCurvesM;
            HasClut = hasClut;
            HasCurvesB = hasCurvesB;
        }
    }

    internal sealed class IccLutAToB : IccLutMeta
    {
        public IccLutAToB(byte inCh, byte outCh, byte gridPoints, bool hasCurvesA, bool hasMatrix, bool hasCurvesM, bool hasClut, bool hasCurvesB)
            : base(inCh, outCh, gridPoints, hasCurvesA, hasMatrix, hasCurvesM, hasClut, hasCurvesB) { }
    }

    internal sealed class IccLutBToA : IccLutMeta
    {
        public IccLutBToA(byte inCh, byte outCh, byte gridPoints, bool hasCurvesA, bool hasMatrix, bool hasCurvesM, bool hasClut, bool hasCurvesB)
            : base(inCh, outCh, gridPoints, hasCurvesA, hasMatrix, hasCurvesM, hasClut, hasCurvesB) { }
    }
}
