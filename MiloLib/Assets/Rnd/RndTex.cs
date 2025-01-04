﻿using MiloLib.Utils;
using MiloLib.Classes;

namespace MiloLib.Assets.Rnd
{

    // this is a misleading description from Harmonix lol, I guess I could add this though so it is no longer false
    [Name("Tex"), Description("Tex perObjs represent bitmaps used by materials. These can be created automatically with 'import tex' on the file menu.")]
    public class RndTex : Object
    {
        private ushort altRevision;
        private ushort revision;

        [Name("Width"), Description("Width of the texture in pixels.")]
        public uint width;
        [Name("Height"), Description("Height of the texture in pixels.")]
        public uint height;

        [Name("BPP"), Description("Bits per pixel.")]
        public uint bpp;

        [Name("External Path"), Description("Path to the texture to be loaded externally.")]
        public Symbol externalPath = new(0, "");

        [MinVersion(8)]
        public float indexFloat;
        public uint index2;

        [MinVersion(11)]
        public bool optimizeForPS3;

        [Name("Use External Path"), Description("Whether or not to use the external path.")]
        public bool useExternalPath;

        [Name("Bitmap"), Description("The bitmap data.")]
        public RndBitmap bitmap = new();

        public ushort unkShort;

        public uint unkInt;
        public uint unkInt2;
        public ushort unkShort2;

        public RndTex Read(EndianReader reader, bool standalone, DirectoryMeta parent, DirectoryMeta.Entry entry)
        {
            uint combinedRevision = reader.ReadUInt32();
            if (BitConverter.IsLittleEndian) (revision, altRevision) = ((ushort)(combinedRevision & 0xFFFF), (ushort)((combinedRevision >> 16) & 0xFFFF));
            else (altRevision, revision) = ((ushort)(combinedRevision & 0xFFFF), (ushort)((combinedRevision >> 16) & 0xFFFF));

            if (revision > 8)
                base.Read(reader, false, parent, entry);

            width = reader.ReadUInt32();
            height = reader.ReadUInt32();

            bpp = reader.ReadUInt32();

            externalPath = Symbol.Read(reader);

            if (revision >= 8)
                indexFloat = reader.ReadFloat();
            index2 = reader.ReadUInt32();

            if (revision >= 11)
                optimizeForPS3 = reader.ReadBoolean();

            if (revision != 7)
                useExternalPath = reader.ReadBoolean();
            else
                useExternalPath = reader.ReadUInt32() == 1;

            if (parent.platform == DirectoryMeta.Platform.Wii && revision > 10)
                unkShort = reader.ReadUInt16();


            // bitmaps are stored as Little endian on Wii? wack
            Endian origEndian = reader.Endianness;

            if (parent.platform == DirectoryMeta.Platform.Wii && revision > 10)
                reader.Endianness = Endian.LittleEndian;

            bitmap = new RndBitmap().Read(reader, false, parent, entry);

            reader.Endianness = origEndian;

            if (parent.platform == DirectoryMeta.Platform.Wii && revision > 10)
            {
                unkInt = reader.ReadUInt32();
                unkInt2 = reader.ReadUInt32();
                unkShort2 = reader.ReadUInt16();
            }

            if (standalone)
            {
                if ((reader.Endianness == Endian.BigEndian ? 0xADDEADDE : 0xDEADDEAD) != reader.ReadUInt32()) throw new Exception("Got to end of standalone asset but didn't find the expected end bytes, read likely did not succeed");
            }

            return this;
        }

        public override void Write(EndianWriter writer, bool standalone, DirectoryMeta parent, DirectoryMeta.Entry? entry)
        {
            writer.WriteUInt32(BitConverter.IsLittleEndian ? (uint)((altRevision << 16) | revision) : (uint)((revision << 16) | altRevision));

            if (revision > 8)
                base.Write(writer, false, parent, entry);

            writer.WriteUInt32(width);
            writer.WriteUInt32(height);

            writer.WriteUInt32(bpp);

            Symbol.Write(writer, externalPath);

            if (revision >= 8)
                writer.WriteFloat(indexFloat);
            writer.WriteUInt32(index2);

            if (revision >= 11)
                writer.WriteBoolean(optimizeForPS3);

            if (revision != 7)
                writer.WriteBoolean(useExternalPath);
            else
                writer.WriteUInt32(useExternalPath ? 1u : 0u);

            if (parent.platform == DirectoryMeta.Platform.Wii && altRevision == 1)
                writer.WriteUInt16(unkShort);

            Endian origEndian = writer.Endianness;

            if (parent.platform == DirectoryMeta.Platform.Wii && revision > 10)
                writer.Endianness = Endian.LittleEndian;

            bitmap.Write(writer, false, parent, entry);

            writer.Endianness = origEndian;

            if (parent.platform == DirectoryMeta.Platform.Wii && revision > 10)
            {
                writer.WriteUInt32(unkInt);
                writer.WriteUInt32(unkInt2);
                writer.WriteUInt16(unkShort2);
            }

            if (standalone)
            {
                writer.WriteBlock(new byte[4] { 0xAD, 0xDE, 0xAD, 0xDE });
            }

        }

    }
}
