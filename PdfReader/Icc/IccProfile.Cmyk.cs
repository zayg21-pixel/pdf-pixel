namespace PdfReader.Icc
{
    /// <summary>
    /// Partial ICC profile implementation: CMYK / LUT tag readers (minimal metadata for PDF use).
    /// We do not implement full LUT pipelines here; we only record enough structural metadata so the renderer
    /// / converters can decide how to evaluate or approximate color transforms elsewhere.
    /// </summary>
    internal sealed partial class IccProfile
    {
        /// <summary>
        /// Read an AToB (A2B) LUT tag and return lightweight structural metadata representation.
        /// Supports legacy lut8 / lut16 and v4 multi-process elements ('mAB ').
        /// </summary>
        internal static IccLutAToB ReadAToB(BigEndianReader reader, IccTagEntry tag)
        {
            if (!CanReadMinimum(reader, tag, 12))
            {
                return null;
            }

            uint type = reader.ReadUInt32(tag.Offset + 0);

            // Recognize classic LUT8 / LUT16 and v4 mAB type ('mAB ').
            if (type == BigEndianReader.FourCC(IccConstants.TypeMAB))
            {
                return ReadMabType(reader, tag);
            }

            if (type == BigEndianReader.FourCC(IccConstants.TypeLut8))
            {
                IccLutMeta meta = ReadLut8Type(reader, tag, isAToB: true);
                if (meta == null)
                {
                    return null;
                }

                return new IccLutAToB(meta.InChannels, meta.OutChannels, meta.GridPoints, meta.HasCurvesA, meta.HasMatrix, meta.HasCurvesM, meta.HasClut, meta.HasCurvesB)
                {
                    TagType = meta.TagType,
                    IsAToB = true
                };
            }

            if (type == BigEndianReader.FourCC(IccConstants.TypeLut16))
            {
                IccLutMeta meta = ReadLut16Type(reader, tag, isAToB: true);
                if (meta == null)
                {
                    return null;
                }

                return new IccLutAToB(meta.InChannels, meta.OutChannels, meta.GridPoints, meta.HasCurvesA, meta.HasMatrix, meta.HasCurvesM, meta.HasClut, meta.HasCurvesB)
                {
                    TagType = meta.TagType,
                    IsAToB = true
                };
            }

            // TODO (future): Handle potential ICC v5 'clut' type if encountered.
            return null;
        }

        /// <summary>
        /// Read a BToA (B2A) LUT tag and return lightweight structural metadata representation.
        /// Supports legacy lut8 / lut16 and v4 multi-process elements ('mBA ').
        /// </summary>
        internal static IccLutBToA ReadBToA(BigEndianReader reader, IccTagEntry tag)
        {
            if (!CanReadMinimum(reader, tag, 12))
            {
                return null;
            }

            uint type = reader.ReadUInt32(tag.Offset + 0);

            if (type == BigEndianReader.FourCC(IccConstants.TypeMBA))
            {
                return ReadMbaType(reader, tag);
            }

            if (type == BigEndianReader.FourCC(IccConstants.TypeLut8))
            {
                IccLutMeta meta = ReadLut8Type(reader, tag, isAToB: false);
                if (meta == null)
                {
                    return null;
                }

                return new IccLutBToA(meta.InChannels, meta.OutChannels, meta.GridPoints, meta.HasCurvesA, meta.HasMatrix, meta.HasCurvesM, meta.HasClut, meta.HasCurvesB)
                {
                    TagType = meta.TagType,
                    IsAToB = false
                };
            }

            if (type == BigEndianReader.FourCC(IccConstants.TypeLut16))
            {
                IccLutMeta meta = ReadLut16Type(reader, tag, isAToB: false);
                if (meta == null)
                {
                    return null;
                }

                return new IccLutBToA(meta.InChannels, meta.OutChannels, meta.GridPoints, meta.HasCurvesA, meta.HasMatrix, meta.HasCurvesM, meta.HasClut, meta.HasCurvesB)
                {
                    TagType = meta.TagType,
                    IsAToB = false
                };
            }

            return null;
        }

        /// <summary>
        /// Bounds check helper for tag content.
        /// </summary>
        private static bool CanReadMinimum(BigEndianReader reader, IccTagEntry tag, int minimumSize)
        {
            return reader.CanRead(tag.Offset, minimumSize);
        }

        /// <summary>
        /// Parse structural flags from an mAB (multi-process) AToB tag.
        /// </summary>
        private static IccLutAToB ReadMabType(BigEndianReader reader, IccTagEntry tag)
        {
            // mABType per ICC v4: type(4), reserved(4), inCh(1), numOut(1), numGrid(1), reserved(1),
            // then offsets (u32) to B curves, matrix, M curves, CLUT, A curves (some may be zero when absent).
            if (!CanReadMinimum(reader, tag, 32))
            {
                return null;
            }

            byte inCh = reader.ReadByte(tag.Offset + 8);
            byte outCh = reader.ReadByte(tag.Offset + 9);
            byte grid = reader.ReadByte(tag.Offset + 10);

            uint bCurves = reader.ReadUInt32(tag.Offset + 12);
            uint matrix = reader.ReadUInt32(tag.Offset + 16);
            uint mCurves = reader.ReadUInt32(tag.Offset + 20);
            uint clut = reader.ReadUInt32(tag.Offset + 24);
            uint aCurves = reader.ReadUInt32(tag.Offset + 28);

            bool hasA = aCurves != 0;
            bool hasM = mCurves != 0;
            bool hasMatrix = matrix != 0;
            bool hasClut = clut != 0;
            bool hasB = bCurves != 0;

            return new IccLutAToB(inCh, outCh, grid, hasA, hasMatrix, hasM, hasClut, hasB)
            {
                TagType = IccConstants.TypeMAB,
                IsAToB = true
            };
        }

        /// <summary>
        /// Parse structural flags from an mBA (multi-process) BToA tag.
        /// </summary>
        private static IccLutBToA ReadMbaType(BigEndianReader reader, IccTagEntry tag)
        {
            if (!CanReadMinimum(reader, tag, 32))
            {
                return null;
            }

            byte inCh = reader.ReadByte(tag.Offset + 8);
            byte outCh = reader.ReadByte(tag.Offset + 9);
            byte grid = reader.ReadByte(tag.Offset + 10);

            uint aCurves = reader.ReadUInt32(tag.Offset + 12);
            uint matrix = reader.ReadUInt32(tag.Offset + 16);
            uint mCurves = reader.ReadUInt32(tag.Offset + 20);
            uint clut = reader.ReadUInt32(tag.Offset + 24);
            uint bCurves = reader.ReadUInt32(tag.Offset + 28);

            bool hasA = aCurves != 0;
            bool hasM = mCurves != 0;
            bool hasMatrix = matrix != 0;
            bool hasClut = clut != 0;
            bool hasB = bCurves != 0;

            return new IccLutBToA(inCh, outCh, grid, hasA, hasMatrix, hasM, hasClut, hasB)
            {
                TagType = IccConstants.TypeMBA,
                IsAToB = false
            };
        }

        /// <summary>
        /// Read legacy lut8 tag structural metadata. Actual tables are not parsed here (only existence recorded).
        /// </summary>
        private static IccLutMeta ReadLut8Type(BigEndianReader reader, IccTagEntry tag, bool isAToB)
        {
            if (!CanReadMinimum(reader, tag, 12))
            {
                return null;
            }

            byte inCh = reader.ReadByte(tag.Offset + 8);
            byte outCh = reader.ReadByte(tag.Offset + 9);
            byte grid = reader.ReadByte(tag.Offset + 10);

            // lut8 always contains: A (input) curves, matrix, CLUT, B (output) curves. No separate M curves stage.
            return new IccLutMeta(inCh, outCh, grid, hasCurvesA: true, hasMatrix: true, hasCurvesM: false, hasClut: true, hasCurvesB: true)
            {
                TagType = IccConstants.TypeLut8,
                IsAToB = isAToB
            };
        }

        /// <summary>
        /// Read legacy lut16 tag structural metadata. Actual tables are not parsed here (only existence recorded).
        /// </summary>
        private static IccLutMeta ReadLut16Type(BigEndianReader reader, IccTagEntry tag, bool isAToB)
        {
            if (!CanReadMinimum(reader, tag, 12))
            {
                return null;
            }

            byte inCh = reader.ReadByte(tag.Offset + 8);
            byte outCh = reader.ReadByte(tag.Offset + 9);
            byte grid = reader.ReadByte(tag.Offset + 10);

            return new IccLutMeta(inCh, outCh, grid, hasCurvesA: true, hasMatrix: true, hasCurvesM: false, hasClut: true, hasCurvesB: true)
            {
                TagType = IccConstants.TypeLut16,
                IsAToB = isAToB
            };
        }
    }
}
