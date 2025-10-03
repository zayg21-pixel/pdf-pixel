using System;

namespace PdfReader.Icc
{
    internal sealed class IccProfileHeader
    {
        public uint Size { get; set; }
        public string CmmType { get; set; }
        public Version Version { get; set; }
        public string DeviceClass { get; set; }
        public string ColorSpace { get; set; }
        public string Pcs { get; set; }
        public DateTime? CreationTime { get; set; }
        public string Platform { get; set; }
        public uint FlagsRaw { get; set; }
        public string DeviceManufacturer { get; set; }
        public uint DeviceModel { get; set; }
        public uint RenderingIntent { get; set; }
        public IccXyz? Illuminant { get; set; }
        public string Creator { get; set; }

        public static IccProfileHeader Read(BigEndianReader r)
        {
            var h = new IccProfileHeader();
            uint size = r.ReadUInt32(0);
            var cmm = BigEndianReader.FourCCToString(r.ReadUInt32(4));
            uint verRaw = r.ReadUInt32(8);
            var deviceClass = BigEndianReader.FourCCToString(r.ReadUInt32(12));
            var colorSpace = BigEndianReader.FourCCToString(r.ReadUInt32(16));
            var pcs = BigEndianReader.FourCCToString(r.ReadUInt32(20));

            int y = r.ReadUInt16(24);
            int mo = r.ReadUInt16(26);
            int d = r.ReadUInt16(28);
            int hhh = r.ReadUInt16(30);
            int mi = r.ReadUInt16(32);
            int s = r.ReadUInt16(34);
            DateTime? created = null;
            try { created = new DateTime(y, Math.Max(1, mo), Math.Max(1, d), Math.Min(23, hhh), Math.Min(59, mi), Math.Min(59, s), DateTimeKind.Utc); }
            catch { created = null; }

            var sig = BigEndianReader.FourCCToString(r.ReadUInt32(36));
            var platform = BigEndianReader.FourCCToString(r.ReadUInt32(40));
            uint flags = r.ReadUInt32(44);
            var manuf = BigEndianReader.FourCCToString(r.ReadUInt32(48));
            uint model = r.ReadUInt32(52);
            ulong attributes = r.ReadUInt64(56);

            h.Size = size;
            h.CmmType = cmm;
            h.Version = new Version((int)((verRaw >> 24) & 0xFF), (int)((verRaw >> 20) & 0x0F), (int)((verRaw >> 16) & 0x0F));
            h.DeviceClass = deviceClass;
            h.ColorSpace = colorSpace;
            h.Pcs = pcs;
            h.CreationTime = created;
            h.Platform = platform;
            h.FlagsRaw = flags;
            h.DeviceManufacturer = manuf;
            h.DeviceModel = model;
            h.RenderingIntent = r.ReadUInt32(64);

            int ix = r.ReadInt32(68);
            int iy = r.ReadInt32(72);
            int iz = r.ReadInt32(76);
            try { h.Illuminant = new IccXyz(BigEndianReader.S15Fixed16ToSingle(ix), BigEndianReader.S15Fixed16ToSingle(iy), BigEndianReader.S15Fixed16ToSingle(iz)); }
            catch { h.Illuminant = null; }
            h.Creator = BigEndianReader.FourCCToString(r.ReadUInt32(80));

            return h;
        }
    }
}
