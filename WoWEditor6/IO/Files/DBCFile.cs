﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WoWEditor6.IO.Files
{
    unsafe class DbcRecord : IDataStorageRecord
    {
        private readonly int mSize;
        private readonly byte[] mData;
        private readonly Dictionary<int, string> mStringTable;

        // ReSharper disable UnusedParameter.Local
        private void AssertValid(int offset, int size)
        {
            if (offset + size > mSize)
                throw new IndexOutOfRangeException("Trying to read past the end of a dbc record");
        }

        public DbcRecord(int size, int offset, BinaryReader reader, Dictionary<int, string> stringTable)
        {
            mSize = size;
            mData = new byte[size];
            reader.BaseStream.Position = offset;
            reader.BaseStream.Read(mData, 0, mSize);
            mStringTable = stringTable;
        }

        public T Get<T>(int offset) where T : struct
        {
            AssertValid(offset, SizeCache<T>.Size);
            var ret = new T();
            var ptr = SizeCache<T>.GetUnsafePtr(ref ret);
            fixed (byte* data = mData)
                UnsafeNativeMethods.CopyMemory((byte*)ptr, data, SizeCache<T>.Size);

            return ret;
        }

        public int GetInt32(int field)
        {
            AssertValid(field * 4, 4);
            fixed(byte* ptr = mData)
                return *(int*) (ptr + field * 4);
        }

        public uint GetUint32(int field)
        {
            AssertValid(field * 4, 4);
            fixed (byte* ptr = mData)
                return *(uint*) (ptr + field * 4);
        }

        public float GetFloat(int field)
        {
            AssertValid(field * 4, 4);
            fixed (byte* ptr = mData)
                return *(float*)(ptr + field * 4);
        }

        public string GetString(int field)
        {
            AssertValid(field * 4, 4);
            int offset;
            fixed (byte* ptr = mData)
                offset = *(int*) (ptr + field * 4);

            if (offset == 0)
                return null;

            string str;
            mStringTable.TryGetValue(offset, out str);
            return str;    
        }
    }

    class DbcFile : IDataStorageFile
    {
        private int mRecordSize;
        private int mStringTableSize;
        private Stream mStream;
        private BinaryReader mReader;
        private Dictionary<int, string> mStringTable = new Dictionary<int, string>();
        private Dictionary<int, int> mIdLookup = new Dictionary<int, int>();

        public int NumRows { get; private set; }
        public int NumFields { get; private set; }

        public void Load(string file)
        {
            mStream = FileManager.Instance.Provider.OpenFile(file);
            if (mStream == null)
                throw new FileNotFoundException(file);

            mReader = new BinaryReader(mStream);
            mReader.ReadInt32(); // signature
            NumRows = mReader.ReadInt32();
            NumFields = mReader.ReadInt32();
            mRecordSize = mReader.ReadInt32();
            mStringTableSize = mReader.ReadInt32();

            mStream.Position = NumRows * mRecordSize + 20;
            var strBytes = mReader.ReadBytes(mStringTableSize);
            var curOffset = 0;
            var curBytes = new List<byte>();
            for(var i = 0; i < strBytes.Length; ++i)
            {
                if (strBytes[i] != 0)
                {
                    curBytes.Add(strBytes[i]);
                    continue;
                }

                mStringTable.Add(curOffset, Encoding.UTF8.GetString(curBytes.ToArray()));
                curBytes.Clear();
                curOffset = i + 1;
            }

            for(var i = 0; i < NumRows; ++i)
                mIdLookup.Add(GetRow(i).GetInt32(0), i);
        }

        public IDataStorageRecord GetRow(int index)
        {
            return new DbcRecord(mRecordSize, 20 + index * mRecordSize, mReader, mStringTable);
        }

        public IDataStorageRecord GetRowById(int id)
        {
            int index;
            return mIdLookup.TryGetValue(id, out index) ? GetRow(index) : null;
        }

        public int AddString(string value)
        {
            var maxIndex = 0;
            var maxLen = 0;

            foreach (var pair in mStringTable)
            {
                if (pair.Value.Equals(value))
                    return pair.Key;

                if (pair.Key <= maxIndex) continue;

                maxIndex = pair.Key;
                maxLen = pair.Value.Length;
            }

            maxIndex += maxLen + 1;
            mStringTable.Add(maxIndex, value);
            return maxIndex;
        }

        public unsafe void AddRowToDbc(byte[] content)
        {
            if(content.Length != mRecordSize)
                throw new ArgumentException("Cannot add row to DBC. Size of row does not match record size");

            fixed (byte* bptr = content)
                *(int*) bptr = mIdLookup.Keys.Max() + 1;
        }

        ~DbcFile()
        {
            Dispose(false);
        }

        private void Dispose(bool disposing)
        {
            if (mStream != null)
            {
                mStream.Dispose();
                mStream = null;
            }

            if (mReader != null)
            {
                mReader.Dispose();
                mReader = null;
            }

            if (mStringTable != null)
            {
                mStringTable.Clear();
                mStringTable = null;
            }

            if (mIdLookup != null)
            {
                mIdLookup.Clear();
                mIdLookup = null;
            }
        }

        public virtual void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
