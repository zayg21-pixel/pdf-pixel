using System;
using System.Collections.Generic;

namespace PdfReader.Icc
{
    /// <summary>
    /// Minimal ICC profile object model with fields commonly needed for PDF:
    /// - Header (profile class, color spaces, PCS, version)
    /// - Tag directory
    /// - Common tags: wtpt, bkpt, rXYZ/gXYZ/bXYZ, rTRC/gTRC/bTRC or kTRC, A2B0/B2A0 (presence only), chad, desc/mluc
    /// NOTE: This does not implement color transforms. It parses for diagnostics and basic decisions.
    /// </summary>
    internal sealed partial class IccProfile
    {
        public IccProfileHeader Header { get; set; }
        public IReadOnlyList<IccTagEntry> Tags { get; set; }

        // Selected parsed data for quick access
        public IccXyz? WhitePoint { get; set; }   // wtpt
        public IccXyz? BlackPoint { get; set; }   // bkpt (rare)
        public IccXyz? RedMatrix { get; set; }    // rXYZ (matrix/TRC RGB)
        public IccXyz? GreenMatrix { get; set; }  // gXYZ
        public IccXyz? BlueMatrix { get; set; }   // bXYZ
        public IccTrc RedTrc { get; set; }        // rTRC
        public IccTrc GreenTrc { get; set; }      // gTRC
        public IccTrc BlueTrc { get; set; }       // bTRC
        public IccTrc GrayTrc { get; set; }       // kTRC (for gray profiles)
        public float[,] ChromaticAdaptation { get; set; } // chad (Bradford matrix)
        public string Description { get; set; }    // desc or mluc

        // CMYK-related LUTs for device <-> PCS transforms
        public IccLutAToB A2B0 { get; set; }
        public IccLutAToB A2B1 { get; set; }
        public IccLutAToB A2B2 { get; set; }
        public IccLutBToA B2A0 { get; set; }
        public IccLutBToA B2A1 { get; set; }
        public IccLutBToA B2A2 { get; set; }

        public bool HasA2B0 { get; set; }
        public bool HasB2A0 { get; set; }

        // Parsed LUT pipelines (raw) for CMYK A2B per rendering intent
        public IccLutPipeline A2BLut0 { get; set; } // Perceptual
        public IccLutPipeline A2BLut1 { get; set; } // Media-relative colorimetric
        public IccLutPipeline A2BLut2 { get; set; } // Saturation

        public IccProfile()
        {
        }

        public static IccProfile Parse(byte[] data)
        {
            if (data == null || data.Length < 132)
            {
                throw new ArgumentException("Invalid ICC profile: too short");
            }

            var r = new BigEndianReader(data);
            var profile = new IccProfile
            {
                Header = IccProfileHeader.Read(r),
            };

            // Tag table
            int tagCount = r.ReadInt32(128);
            var tags = new List<IccTagEntry>(Math.Max(0, tagCount));
            int cursor = 128 + 4;
            for (int i = 0; i < tagCount; i++)
            {
                if (!SafeHas(r, cursor, 12)) break; // avoid OOR
                uint sig = r.ReadUInt32(cursor); // 4CC tag signature
                int offset = r.ReadInt32(cursor + 4);
                int size = r.ReadInt32(cursor + 8);
                tags.Add(new IccTagEntry(sig, offset, size));
                cursor += 12;
            }
            profile.Tags = tags;

            // Parse selected known tags
            foreach (var t in tags)
            {
                switch (t.SignatureString)
                {
                    case IccConstants.TagWtpt: profile.WhitePoint = ReadXyzType(r, t); break;
                    case IccConstants.TagBkpt: profile.BlackPoint = ReadXyzType(r, t); break;
                    case IccConstants.Tag_rXYZ: profile.RedMatrix = ReadXyzType(r, t); break;
                    case IccConstants.Tag_gXYZ: profile.GreenMatrix = ReadXyzType(r, t); break;
                    case IccConstants.Tag_bXYZ: profile.BlueMatrix = ReadXyzType(r, t); break;
                    case IccConstants.Tag_rTRC: profile.RedTrc = ReadTrcType(r, t); break;
                    case IccConstants.Tag_gTRC: profile.GreenTrc = ReadTrcType(r, t); break;
                    case IccConstants.Tag_bTRC: profile.BlueTrc = ReadTrcType(r, t); break;
                    case IccConstants.Tag_kTRC: profile.GrayTrc = ReadTrcType(r, t); break;
                    case IccConstants.TagChad: profile.ChromaticAdaptation = ReadChadMatrix(r, t); break;
                    case IccConstants.TagDesc: profile.Description = ReadDescType(r, t); break;
                    case IccConstants.TagMluc: if (string.IsNullOrEmpty(profile.Description)) profile.Description = ReadMlucType(r, t); break;

                    // CMYK/device LUT pipelines
                    case IccConstants.TagA2B0:
                        profile.A2B0 = ReadAToB(r, t); profile.HasA2B0 = profile.A2B0 != null; profile.A2BLut0 = ParseA2BLut(r, t); break;
                    case IccConstants.TagA2B1:
                        profile.A2B1 = ReadAToB(r, t); profile.A2BLut1 = ParseA2BLut(r, t); break;
                    case IccConstants.TagA2B2:
                        profile.A2B2 = ReadAToB(r, t); profile.A2BLut2 = ParseA2BLut(r, t); break;
                    case IccConstants.TagB2A0: profile.B2A0 = ReadBToA(r, t); profile.HasB2A0 = profile.B2A0 != null; break; // TODO: parse full LUT contents if needed
                    case IccConstants.TagB2A1: profile.B2A1 = ReadBToA(r, t); break;
                    case IccConstants.TagB2A2: profile.B2A2 = ReadBToA(r, t); break;
                }
            }

            return profile;
        }

        /// <summary>
        /// Convenience factory to create a minimal Gray ICC profile instance in code (no parsing).
        /// You can further customize any properties after creation.
        /// </summary>
        public static IccProfile CreateGrayProfile(float? gamma = null, IccTrc grayTrc = null, IccXyz? whitePoint = null)
        {
            var p = new IccProfile
            {
                Header = new IccProfileHeader
                {
                    ColorSpace = IccConstants.SpaceGray,
                    Pcs = IccConstants.TypeXYZ,
                    RenderingIntent = 1 // Relative colorimetric by default
                },
                WhitePoint = whitePoint ?? new IccXyz(0.9642f, 1.0f, 0.8249f),
            };

            if (grayTrc != null)
            {
                p.GrayTrc = grayTrc;
            }
            else if (gamma.HasValue)
            {
                p.GrayTrc = IccTrc.FromGamma(gamma.Value);
            }

            return p;
        }

        private static bool SafeHas(BigEndianReader r, int offset, int count)
        {
            try { r.Ensure(offset, count); return true; } catch { return false; }
        }

        private static IccXyz? ReadXyzType(BigEndianReader r, IccTagEntry tag)
        {
            if (!SafeHas(r, tag.Offset, 20)) return null;
            uint type = r.ReadUInt32(tag.Offset + 0); // 'XYZ '
            // skip 4 reserved
            if (type != BigEndianReader.FourCC(IccConstants.TypeXYZ)) return null;
            int x = r.ReadInt32(tag.Offset + 8);
            int y = r.ReadInt32(tag.Offset + 12);
            int z = r.ReadInt32(tag.Offset + 16);
            return new IccXyz(
                BigEndianReader.S15Fixed16ToSingle(x),
                BigEndianReader.S15Fixed16ToSingle(y),
                BigEndianReader.S15Fixed16ToSingle(z));
        }

        private static IccTrc ReadTrcType(BigEndianReader r, IccTagEntry tag)
        {
            if (!SafeHas(r, tag.Offset, 12)) return null;
            uint type = r.ReadUInt32(tag.Offset + 0); // 'curv' or 'para'
            if (type == BigEndianReader.FourCC(IccConstants.TypeCurv))
            {
                // CurveType: count at +8 (uInt32). If 1 -> gamma (u8Fixed8), else curve samples uInt16
                if (!SafeHas(r, tag.Offset, 12)) return null;
                uint count = r.ReadUInt32(tag.Offset + 8);
                if (count == 1)
                {
                    if (!SafeHas(r, tag.Offset + 12, 2)) return null;
                    ushort u8f8 = r.ReadUInt16(tag.Offset + 12);
                    return IccTrc.FromGamma(BigEndianReader.U8Fixed8ToSingle(u8f8));
                }
                else
                {
                    int n = (int)Math.Min(int.MaxValue, count);
                    if (!SafeHas(r, tag.Offset + 12, n * 2)) return IccTrc.Sampled(n);
                    var samples = new float[n];
                    int pos = tag.Offset + 12;
                    for (int i = 0; i < n; i++)
                    {
                        samples[i] = r.ReadUInt16(pos + i * 2) / 65535f;
                    }
                    return IccTrc.FromSamples(samples);
                }
            }
            if (type == BigEndianReader.FourCC(IccConstants.TypePara))
            {
                // Parametric curve. Implement types 0..4 as parameter storage; evaluation done later.
                if (!SafeHas(r, tag.Offset, 16)) return null;
                ushort funcType = r.ReadUInt16(tag.Offset + 8);
                int paramCount = GetParamCount(funcType);
                if (!SafeHas(r, tag.Offset + 12, paramCount * 4)) return IccTrc.UnsupportedParametric(funcType);
                var pars = new float[paramCount];
                for (int i = 0; i < paramCount; i++)
                {
                    pars[i] = BigEndianReader.S15Fixed16ToSingle(r.ReadInt32(tag.Offset + 12 + i * 4));
                }
                return IccTrc.FromParametric(funcType, pars);
            }
            return null;
        }

        private static float[,] ReadChadMatrix(BigEndianReader r, IccTagEntry tag)
        {
            // chad type is s15Fixed16MatrixType in v4.3 spec (but many profiles store as 'sf32' matrix without explicit type)
            // In practice: 3x3 s15Fixed16 in row-major, starting at +8
            if (!SafeHas(r, tag.Offset, 8 + 9 * 4)) return null;
            // Some profiles omit the type signature; try to tolerate if size matches
            int baseOff = tag.Offset + 8;
            var m = new float[3, 3];
            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    int v = r.ReadInt32(baseOff + (i * 3 + j) * 4);
                    m[i, j] = BigEndianReader.S15Fixed16ToSingle(v);
                }
            }
            return m;
        }

        private static string ReadDescType(BigEndianReader r, IccTagEntry tag)
        {
            if (!SafeHas(r, tag.Offset, 12)) return null;
            uint type = r.ReadUInt32(tag.Offset + 0); // 'desc'
            if (type != BigEndianReader.FourCC(IccConstants.TypeDesc)) return null;
            uint asciiCount = r.ReadUInt32(tag.Offset + 8);
            if (asciiCount > 0 && SafeHas(r, tag.Offset + 12, (int)asciiCount))
            {
                var bytes = r.ReadBytes(tag.Offset + 12, (int)Math.Min(int.MaxValue, asciiCount));
                try { return System.Text.Encoding.ASCII.GetString(bytes).TrimEnd('\0'); }
                catch { return null; }
            }
            return null;
        }

        private static string ReadMlucType(BigEndianReader r, IccTagEntry tag)
        {
            if (!SafeHas(r, tag.Offset, 16)) return null;
            uint type = r.ReadUInt32(tag.Offset + 0); // 'mluc'
            if (type != BigEndianReader.FourCC(IccConstants.TypeMluc)) return null;
            uint count = r.ReadUInt32(tag.Offset + 8);
            uint recSize = r.ReadUInt32(tag.Offset + 12);
            int recBase = tag.Offset + 16;

            // Read first record only
            if (count == 0 || recSize < 12) return null;
            if (!SafeHas(r, recBase, (int)recSize)) return null;

            // record: lang(2), country(2), length(u32), offset(u32)
            ushort lang = r.ReadUInt16(recBase + 0);
            ushort country = r.ReadUInt16(recBase + 2);
            uint length = r.ReadUInt32(recBase + 4);
            uint strOffset = r.ReadUInt32(recBase + 8);

            int strPos = tag.Offset + (int)strOffset;
            if (!SafeHas(r, strPos, (int)length)) return null;
            // UTF-16BE
            var raw = r.ReadBytes(strPos, (int)length);
            try { return System.Text.Encoding.BigEndianUnicode.GetString(raw).TrimEnd('\0'); }
            catch { return null; }
        }

        private static int GetParamCount(int funcType)
        {
            switch (funcType)
            {
                case 0: return 1; // g
                case 1: return 3; // g, a, b
                case 2: return 4; // g, a, b, c
                case 3: return 5; // g, a, b, c, d
                case 4: return 7; // g, a, b, c, d, e, f
                default: return 1; // best effort
            }
        }
    }
}
