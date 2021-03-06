﻿using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System;
using WDBXLib.FileTypes;

namespace WDBXLib
{
    public class DBHeader
    {
        //Standard Fields
        public string Signature { get; set; }
        public uint RecordCount { get; set; }
        public uint FieldCount { get; set; }
        public uint RecordSize { get; set; }
        public uint StringBlockSize { get; set; }

        public ushort IdIndex { get; set; } = 0;
        public int AutoGeneratedColumns { get; set; } = 0;
        public int[] OffsetLengths { get; set; } = new int[0];
        public int HeaderSize = 0x20;
        public int StringTableOffset = 0x10;

        public bool IsTypeOf<T>() => this is T;
        public bool IsLegionFile => this is WDB5;
        public bool IsValidFile => (IsTypeOf<WDB2>() || IsTypeOf<WDBC>() || IsLegionFile);

        public virtual bool ExtendedStringTable => false;
        public virtual bool HasIndexTable => false;
        public virtual bool HasOffsetTable => false;
        public virtual bool HasSecondIndex => false;        

        public Dictionary<int, int> OffsetDuplicates = new Dictionary<int, int>();

        public virtual void ReadHeader(ref BinaryReader dbReader, string signature)
        {
            this.Signature = signature;
            RecordCount = dbReader.ReadUInt32();            
            FieldCount = dbReader.ReadUInt32();
            RecordSize = dbReader.ReadUInt32();
            StringBlockSize = dbReader.ReadUInt32();
        }

        public virtual byte[] ReadData(BinaryReader dbReader, long pos) { return new byte[0]; }

        public virtual void WriteHeader<T>(BinaryWriter bw, DBFile<T> entry)
        {
            //Signature
            bw.Write(Encoding.UTF8.GetBytes(Signature));

            //Record count
            if (IsTypeOf<WDB5>() && !(this as WDB5).HasOffsetTable)
                bw.Write(entry.GetUniqueRows().Count());
            else
                bw.Write(entry.Data.Rows.Count);
            
            //FieldCount
            if (IsTypeOf<WDB5>())
                bw.Write(((WDB5)this).HasIndexTable ? FieldCount - 1 : FieldCount); //Index Table
            else
                bw.Write(FieldCount);

            //Record size
            bw.Write(RecordSize);

            //StringBlockSize placeholder
            if (IsTypeOf<WDB5>())
                bw.Write((uint)2);
            else
                bw.Write((uint)1);
        }

        public virtual void WriteOffsetMap<T>(BinaryWriter bw, DBFile<T> entry, List<Tuple<int, short>> OffsetMap) { }

        public virtual void WriteIndexTable<T>(BinaryWriter bw, DBFile<T> entry) { }

        public virtual void WriteRecordPadding<T>(BinaryWriter bw, DBFile<T> entry, long offset)
        {
            if (bw.BaseStream.Position - offset < RecordSize)
                bw.BaseStream.Position += (RecordSize - (bw.BaseStream.Position - offset));
        }

        [Flags]
        public enum HeaderFlags : short
        {
            None = 0x0,
            OffsetMap = 0x1,
            SecondIndex = 0x2,
            IndexMap = 0x4
        }
    }
}
