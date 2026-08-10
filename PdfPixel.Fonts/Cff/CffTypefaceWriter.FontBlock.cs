using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Cff;

public partial class CffTypefaceWriter
{
    private static FontBlock BuildFontBlock(CffFont font)
    {
        FontBlock block = new();

        if (font.Encoding != null)
        {
            block.HasEncoding = true;
            block.EncodingIsPredefined = font.Encoding.PredefinedId.HasValue;
            block.EncodingPredefinedId = font.Encoding.PredefinedId ?? 0;
            block.EncodingData = (block.EncodingIsPredefined) ? Array.Empty<byte>() : BuildEncodingTable(font.Encoding);
        }

        if (font.Charset != null)
        {
            block.CharsetIsPredefined = font.Charset.PredefinedId.HasValue;
            block.CharsetPredefinedId = font.Charset.PredefinedId ?? 0;
            block.CharsetData = (block.CharsetIsPredefined) ? Array.Empty<byte>() : BuildCharsetTable(font.Charset);
        }

        if (font.Dict.TopDict.Ros != null)
        {
            block.CidCount = GetCidCount(font);
        }

        var charStringEntries = new ReadOnlyMemory<byte>[font.Characters.Length];
        for (int glyphIndex = 0; glyphIndex < font.Characters.Length; glyphIndex++)
        {
            charStringEntries[glyphIndex] = font.Characters[glyphIndex].CharString;
        }

        CffBinaryWriter charStringsWriter = new();
        charStringsWriter.WriteIndex(charStringEntries);
        block.CharStringsIndexData = charStringsWriter.ToArray();

        if (font.Dict.PrivateDict != null)
        {
            block.HasPrivateDict = true;
            block.PrivateDictData = BuildPrivateDict(font.Dict.PrivateDict);
        }

        if (font.FdArray.Length > 0)
        {
            block.HasFdArray = true;
            block.FdArrayFonts = font.FdArray;
            block.FdPrivateDictData = new byte[font.FdArray.Length][];
            for (int fdIndex = 0; fdIndex < font.FdArray.Length; fdIndex++)
            {
                CffPrivateDict? fdPrivateDict = font.FdArray[fdIndex].PrivateDict;
                block.FdPrivateDictData[fdIndex] = (fdPrivateDict != null) ? BuildPrivateDict(fdPrivateDict) : Array.Empty<byte>();
            }

            block.FdArrayIndexData = BuildFdArrayIndex(block.FdArrayFonts, block.FdPrivateDictData, new int[font.FdArray.Length]);
        }

        if (font.FdSelect != null)
        {
            block.HasFdSelect = true;
            block.FdSelectData = BuildFdSelectTable(font.FdSelect);
        }

        return block;
    }

    // In a CID-keyed font the charset holds CIDs rather than SIDs, and CIDCount declares how many are
    // valid. The written charset is the one built here, so the count comes from it rather than from
    // whatever the source font declared. Without a charset the CIDs are the glyph indices themselves.
    private static int GetCidCount(CffFont font)
    {
        ushort[]? cidsByGid = (font.Charset?.PredefinedId == null) ? font.Charset?.SidsByGid : null;

        if (cidsByGid == null || cidsByGid.Length == 0)
        {
            return font.Characters.Length;
        }

        ushort highestCid = 0;
        foreach (ushort cid in cidsByGid)
        {
            if (cid > highestCid)
            {
                highestCid = cid;
            }
        }

        return highestCid + 1;
    }

    private static byte[] BuildEncodingTable(CffEncoding encoding)
    {
        if (encoding.MainTableCodes == null)
        {
            throw new ArgumentException("Explicit CFF encoding is missing its main table codes.", nameof(encoding));
        }

        CffBinaryWriter writer = new();
        byte formatByte = (encoding.Supplements != null) ? CffConstants.EncodingSupplementFlag : (byte)0;
        writer.WriteByte(formatByte);
        writer.WriteByte((byte)encoding.MainTableCodes.Length);
        foreach (byte code in encoding.MainTableCodes)
        {
            writer.WriteByte(code);
        }

        if (encoding.Supplements != null)
        {
            writer.WriteByte((byte)encoding.Supplements.Length);
            foreach (CffEncodingSupplement supplement in encoding.Supplements)
            {
                writer.WriteByte(supplement.Code);
                writer.WriteByte((byte)((supplement.Sid >> 8) & 0xFF));
                writer.WriteByte((byte)(supplement.Sid & 0xFF));
            }
        }

        return writer.ToArray();
    }

    private static byte[] BuildCharsetTable(CffCharset charset)
    {
        CffBinaryWriter writer = new();
        writer.WriteByte(0); // format 0.
        for (int gid = 1; gid < charset.SidsByGid.Length; gid++)
        {
            ushort sid = charset.SidsByGid[gid];
            writer.WriteByte((byte)((sid >> 8) & 0xFF));
            writer.WriteByte((byte)(sid & 0xFF));
        }

        return writer.ToArray();
    }

    private static byte[] BuildFdSelectTable(CffFdSelect fdSelect)
    {
        CffBinaryWriter writer = new();
        writer.WriteByte(0); // format 0.
        foreach (int fdIndex in fdSelect.FdIndexByGid)
        {
            writer.WriteByte((byte)fdIndex);
        }

        return writer.ToArray();
    }

    private static byte[] BuildPrivateDict(CffPrivateDict privateDict)
    {
        CffBinaryWriter writer = new();

        if (privateDict.DefaultWidthX.HasValue)
        {
            writer.WriteDictNumber(privateDict.DefaultWidthX.Value);
            writer.WriteDictOperator(CffConstants.PrivateDictOperatorDefaultWidthX);
        }

        if (privateDict.NominalWidthX.HasValue)
        {
            writer.WriteDictNumber(privateDict.NominalWidthX.Value);
            writer.WriteDictOperator(CffConstants.PrivateDictOperatorNominalWidthX);
        }

        return writer.ToArray();
    }

    private static byte[] BuildFdMiniDict(CffTopDict fdTopDict, int privateSize, int privateOffset)
    {
        CffBinaryWriter writer = new();
        WriteTopDictContentFields(writer, fdTopDict);

        writer.WriteDictNumber(privateSize);
        writer.WriteDictOffset(privateOffset);
        writer.WriteDictOperator(CffConstants.TopDictOperatorPrivate);

        return writer.ToArray();
    }

    private static byte[] BuildFdArrayIndex(CffFontDict[] fdArray, byte[][] fdPrivateDictData, int[] fdPrivateDictOffsets)
    {
        var entries = new ReadOnlyMemory<byte>[fdArray.Length];
        for (int fdIndex = 0; fdIndex < fdArray.Length; fdIndex++)
        {
            entries[fdIndex] = BuildFdMiniDict(fdArray[fdIndex].TopDict, fdPrivateDictData[fdIndex].Length, fdPrivateDictOffsets[fdIndex]);
        }

        CffBinaryWriter writer = new();
        writer.WriteIndex(entries);
        return writer.ToArray();
    }

    private sealed class FontBlock
    {
        public bool HasEncoding;
        public bool EncodingIsPredefined;
        public int EncodingPredefinedId;
        public byte[] EncodingData = Array.Empty<byte>();
        public int EncodingOffsetValue;

        public bool CharsetIsPredefined;
        public int CharsetPredefinedId;
        public byte[] CharsetData = Array.Empty<byte>();
        public int CharsetOffsetValue;

        public int? CidCount;

        public byte[] CharStringsIndexData = Array.Empty<byte>();
        public int CharStringsOffsetValue;

        public bool HasPrivateDict;
        public byte[] PrivateDictData = Array.Empty<byte>();
        public int PrivateDictOffsetValue;

        public bool HasFdArray;
        public CffFontDict[] FdArrayFonts = Array.Empty<CffFontDict>();
        public byte[][] FdPrivateDictData = Array.Empty<byte[]>();
        public byte[] FdArrayIndexData = Array.Empty<byte>();
        public int FdArrayOffsetValue;
        public int[] FdPrivateDictOffsetValues = Array.Empty<int>();

        public bool HasFdSelect;
        public byte[] FdSelectData = Array.Empty<byte>();
        public int FdSelectOffsetValue;

        public int TotalLength
        {
            get
            {
                return EncodingData.Length
                    + CharsetData.Length
                    + CharStringsIndexData.Length
                    + PrivateDictData.Length
                    + FdArrayIndexData.Length
                    + SumLengths(FdPrivateDictData)
                    + FdSelectData.Length;
            }
        }

        public byte[] GetData()
        {
            List<byte> combined = new(TotalLength);
            combined.AddRange(EncodingData);
            combined.AddRange(CharsetData);
            combined.AddRange(CharStringsIndexData);
            combined.AddRange(PrivateDictData);
            combined.AddRange(FdArrayIndexData);
            foreach (byte[] fdPrivateDict in FdPrivateDictData)
            {
                combined.AddRange(fdPrivateDict);
            }

            combined.AddRange(FdSelectData);
            return combined.ToArray();
        }

        public void ResolveOffsets(int blockStart)
        {
            int position = blockStart;

            if (HasEncoding)
            {
                EncodingOffsetValue = EncodingIsPredefined ? EncodingPredefinedId : position;
            }

            position += EncodingData.Length;

            CharsetOffsetValue = CharsetIsPredefined ? CharsetPredefinedId : position;
            position += CharsetData.Length;

            CharStringsOffsetValue = position;
            position += CharStringsIndexData.Length;

            if (HasPrivateDict)
            {
                PrivateDictOffsetValue = position;
            }

            position += PrivateDictData.Length;

            if (HasFdArray)
            {
                FdArrayOffsetValue = position;
                position += FdArrayIndexData.Length;

                FdPrivateDictOffsetValues = new int[FdPrivateDictData.Length];
                for (int fdIndex = 0; fdIndex < FdPrivateDictData.Length; fdIndex++)
                {
                    FdPrivateDictOffsetValues[fdIndex] = position;
                    position += FdPrivateDictData[fdIndex].Length;
                }

                FdArrayIndexData = BuildFdArrayIndex(FdArrayFonts, FdPrivateDictData, FdPrivateDictOffsetValues);
            }

            if (HasFdSelect)
            {
                FdSelectOffsetValue = position;
            }
        }

        private static int SumLengths(byte[][] arrays)
        {
            int total = 0;
            foreach (byte[] array in arrays)
            {
                total += array.Length;
            }

            return total;
        }
    }
}
