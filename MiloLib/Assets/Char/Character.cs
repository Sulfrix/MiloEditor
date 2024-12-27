﻿using MiloLib.Assets.Rnd;
using MiloLib.Classes;
using MiloLib.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiloLib.Assets.Char
{
    [Name("Character"), Description("Base class for Character objects. Contains Geometry, Outfit Loaders, and LOD + Sphere concepts.")]
    public class Character : RndDir
    {
        public class CharacterTest
        {
            public uint revision;
            public Symbol driver = new(0, "");
            public Symbol clip1 = new(0, "");
            public Symbol clip2 = new(0, "");
            public Symbol teleportTo = new(0, "");
            public Symbol teleportFrom = new(0, "");
            public Symbol distMap = new(0, "");
            public uint unk;
            public bool unkBool1;
            public bool unkBool2;
            public bool unkBool3;
            public bool unkBool4;
            public bool unkBool5;
            public bool unkBool6;
            public uint unk3;
            public bool unk4;
            public uint unk5;
            public uint unk6;
            public uint bpm;
            public float unkFloat;
            public Symbol unkSymbol = new(0, "");
            public Symbol unkSymbol2 = new(0, "");

            public CharacterTest Read(EndianReader reader)
            {
                revision = reader.ReadUInt32();
                driver = Symbol.Read(reader);
                clip1 = Symbol.Read(reader);
                clip2 = Symbol.Read(reader);
                teleportTo = Symbol.Read(reader);
                teleportFrom = Symbol.Read(reader);
                distMap = Symbol.Read(reader);

                if (revision <= 6)
                {
                    return this;
                }

                unk = reader.ReadUInt32();
                unkBool1 = reader.ReadBoolean();
                unk3 = reader.ReadUInt32();

                if (revision == 15)
                {
                    unk4 = reader.ReadBoolean();
                    unk5 = reader.ReadUInt32();
                }

                if (revision == 8)
                {

                    unk6 = reader.ReadUInt32();

                    unkBool2 = reader.ReadBoolean();
                    unkBool3 = reader.ReadBoolean();
                    unkBool4 = reader.ReadBoolean();

                    unkSymbol = Symbol.Read(reader);

                    unkBool5 = reader.ReadBoolean();
                    unkBool6 = reader.ReadBoolean();

                    bpm = reader.ReadUInt32();

                    unkSymbol2 = Symbol.Read(reader);

                    unkFloat = reader.ReadFloat();
                }
                return this;
            }

            public void Write(EndianWriter writer)
            {
                writer.WriteUInt32(revision);
                Symbol.Write(writer, driver);
                Symbol.Write(writer, clip1);
                Symbol.Write(writer, clip2);
                Symbol.Write(writer, teleportTo);
                Symbol.Write(writer, teleportFrom);
                Symbol.Write(writer, distMap);

                if (revision <= 6)
                {
                    return;
                }

                writer.WriteUInt32(unk);
                writer.WriteByte(unkBool1 ? (byte)1 : (byte)0);
                writer.WriteUInt32(unk3);
                writer.WriteByte(unk4 ? (byte)1 : (byte)0);
                writer.WriteUInt32(unk5);
            }
        }

        public uint revision;

        private uint lodCount;
        public List<Symbol> lods = new();

        private uint shadowCount;
        public List<Symbol> shadows = new();

        public bool selfShadow;

        public Symbol sphereBase = new(0, "");

        public Sphere bounding = new();

        public bool frozen;

        [Name("Minimum LOD"), Description("Forces LOD, kLODPerFrame is normal behavior of picking per frame, the others force the lod (0 is highest res lod, 2 is lowest res lod)")]
        public int minLod;

        public Symbol translucentGroup = new(0, "");

        public CharacterTest charTest = new();

        public Character Read(EndianReader reader, bool standalone)
        {
            revision = reader.ReadUInt32();

            base.Read(reader, false);

            lodCount = reader.ReadUInt32();
            for (int i = 0; i < lodCount; i++)
            {
                lods.Add(Symbol.Read(reader));
            }

            if (revision < 18)
            {
                shadowCount = 1;
                shadows.Add(Symbol.Read(reader));
            }
            else
            {
                shadowCount = reader.ReadUInt32();
                for (int i = 0; i < shadowCount; i++)
                {
                    shadows.Add(Symbol.Read(reader));
                }
            }

            if (revision > 2)
                selfShadow = reader.ReadBoolean();

            sphereBase = Symbol.Read(reader);

            if (revision <= 9)
                return this;

            if (revision > 10)
            {
                bounding = new();
                bounding.Read(reader);
            }

            if (revision > 0xC)
                frozen = reader.ReadBoolean();

            if (revision > 0xE)
                minLod = reader.ReadInt32();

            if (revision > 0x10)
                translucentGroup = Symbol.Read(reader);

            if (revision <= 15)
                charTest = new CharacterTest().Read(reader);

            if (standalone)
                if ((reader.Endianness == Endian.BigEndian ? 0xADDEADDE : 0xDEADDEAD) != reader.ReadUInt32()) throw new Exception("Got to end of standalone asset but didn't find the expected end bytes, read likely did not succeed");

            return this;
        }

        public override void Write(EndianWriter writer, bool standalone)
        {
            writer.WriteUInt32(revision);

            base.Write(writer, false);

            writer.WriteUInt32((uint)lods.Count);
            foreach (var lod in lods)
            {
                Symbol.Write(writer, lod);
            }

            writer.WriteUInt32((uint)shadows.Count);
            foreach (var shadow in shadows)
            {
                Symbol.Write(writer, shadow);
            }

            writer.WriteBoolean(selfShadow);

            Symbol.Write(writer, sphereBase);

            bounding.Write(writer);

            writer.WriteBoolean(frozen);

            writer.WriteInt32(minLod);

            Symbol.Write(writer, translucentGroup);

            charTest.Write(writer);

            if (standalone)
            {
                writer.WriteBlock(new byte[4] { 0xAD, 0xDE, 0xAD, 0xDE });
            }
        }




        public override bool IsDirectory()
        {
            return true;
        }
    }
}
