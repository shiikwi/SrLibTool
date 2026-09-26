using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SrLibTool
{
    public enum SrPackFileStreamType
    {
        None = 0,
        FileStream = 1,
        MemoryStream = 2,
    }

    public class SrException : Exception
    {
        public SrException(string message) : base(message) { }
    }

    public class SrPackFileInfo
    {
        public string FilePath;
        public SrPackFileStreamType StreamType;
        public byte[] OnMemoryData;
    }

    public class SrPackFileItemInfo
    {
        public string FilePath;
        public long DataOffset;
        public long DataSize;
    }

    public class SrPackReadInfo
    {
        public readonly string FilePath;
        public readonly SrPackFileStreamType StreamType;
        public readonly byte[]? OnMemoryData;
        public readonly long DataOffset;
        public readonly long DataSize;

        public SrPackReadInfo(string filePath, SrPackFileStreamType streamType, byte[]? onMemoryData, long dataOffset, long dataSize)
        {
            FilePath = filePath;
            StreamType = streamType;
            OnMemoryData = onMemoryData;
            DataOffset = dataOffset;
            DataSize = dataSize;
        }
    }

    public class SrPackFileManager
    {
        private readonly Dictionary<string, SrPackFileInfo> _fileMap = new();
        private readonly Dictionary<string, SrPackFileItemInfo> _itemMap = new();

        public void RegisterPackFile(string filePath, bool isOnMemory = false)
        {
            if (isOnMemory)
            {
                var allBytes = File.ReadAllBytes(filePath);
                using var ms = new MemoryStream(allBytes);
                _RegisterPackFile(filePath, SrPackFileStreamType.MemoryStream, allBytes, ms);
            }
            else
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                _RegisterPackFile(filePath, SrPackFileStreamType.FileStream, null, fs);
            }
        }

        private bool _RegisterPackFile(string filePath, SrPackFileStreamType streamType, byte[]? onMemoryData, Stream stream)
        {
            if (!_fileMap.ContainsKey(filePath))
            {
                _fileMap.Add(filePath, new SrPackFileInfo
                {
                    FilePath = filePath,
                    StreamType = streamType,
                    OnMemoryData = onMemoryData,
                });
            }

            var header = new byte[12];
            stream.Seek(0, SeekOrigin.Begin);
            if (stream.Read(header, 0, 12) < 12)
                return false;

            int version = BitConverter.ToUInt16(header, 0x00);
            if (version != 0x01)
                throw new SrException("Unsupport version" + version);

            int entryCount = (int)BitConverter.ToUInt32(header, 0x04);
            int nameTableSize = (int)BitConverter.ToUInt32(header, 0x08);
            if (nameTableSize < 4 * entryCount)
                return false;

            var table = new byte[nameTableSize];
            stream.Seek(32, SeekOrigin.Begin);
            if (stream.Read(table, 0, nameTableSize) < nameTableSize)
                return false;

            int nameBytesTotal = 0;
            for (int i = 0; i < entryCount; i++)
                nameBytesTotal += (int)BitConverter.ToUInt32(table, 4 * i);
            int nameDataPos = 4 * entryCount;
            int entryPos = nameBytesTotal % 4 != 0
                ? 4 * entryCount + nameBytesTotal + (4 - nameBytesTotal % 4)
                : 4 * entryCount + nameBytesTotal;

            for (int i = 0; i < entryCount; i++)
            {
                int nameLen = (int)BitConverter.ToUInt32(table, 4 * i);
                string name = Encoding.Unicode.GetString(table, nameDataPos, nameLen).ToUpperInvariant();
                long dataOffset = BitConverter.ToInt64(table, entryPos);
                long dataSize = BitConverter.ToInt64(table, entryPos + 8);

                if (_itemMap.TryGetValue(name, out var item))
                {
                    item.FilePath = filePath;
                    item.DataOffset = dataOffset;
                    item.DataSize = dataSize;
                }
                else
                {
                    _itemMap.Add(name, new SrPackFileItemInfo
                    {
                        FilePath = filePath,
                        DataOffset = dataOffset,
                        DataSize = dataSize,
                    });
                }

                nameDataPos += nameLen;
                entryPos += 16;
            }
            return true;
        }

        public bool IsExistInfo(string fileName)
        {
            return _itemMap.ContainsKey(fileName.ToUpperInvariant());
        }

        public IEnumerable<string> GetFileNamesByPrefix(string prefix)
        {
            var upperPrefix = prefix.ToUpperInvariant();
            foreach (var name in _itemMap.Keys)
            {
                if (name.StartsWith(upperPrefix))
                    yield return name;
            }
        }

        public SrPackReadInfo GetReadInfo(string fileName)
        {
            if (!_itemMap.TryGetValue(fileName.ToUpperInvariant(), out var item))
                throw new SrException("Not found pack file info: " + fileName);
            return new SrPackReadInfo(item.FilePath, SrPackFileStreamType.FileStream, null, item.DataOffset, item.DataSize);
        }

        public byte[] LoadBytes(string fileName)
        {
            if (!_itemMap.TryGetValue(fileName.ToUpperInvariant(), out var item))
                throw new SrException("Not found pack file: " + fileName);

            var bytes = new byte[item.DataSize];
            using var fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read);
            fs.Seek(item.DataOffset, SeekOrigin.Begin);
            ReadExactly(fs, bytes);
            return bytes;
        }

        private static void ReadExactly(FileStream fs, byte[] bytes)
        {
            int total = 0;
            while (total < bytes.Length)
            {
                int read = fs.Read(bytes, total, bytes.Length - total);
                if (read <= 0)
                    throw new EndOfStreamException();
                total += read;
            }
        }

        public string LoadString(string fileName)
        {
            if (!_itemMap.TryGetValue(fileName.ToUpperInvariant(), out var item))
                throw new SrException("Not found pack file: " + fileName);

            var bytes = new byte[item.DataSize];
            using var fs = new FileStream(item.FilePath, FileMode.Open, FileAccess.Read);
            fs.Seek(item.DataOffset, SeekOrigin.Begin);
            ReadExactly(fs, bytes);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
