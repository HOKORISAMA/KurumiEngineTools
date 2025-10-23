using System;
using System.IO;
using System.Collections.Generic;

namespace Kurumi
{
    public partial class Kurumi
    {
        private const string Signature = "MP";

        public void Unpack(string filePath, string folderName)
        {
            using var file = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var reader = new BinaryReader(file);

            // Check signature
            if (reader.ReadString(2) != Signature)
                throw new InvalidDataException("Invalid file signature.");

            // Check version
            int version = reader.ReadByte();
            if (version > 1)
                throw new InvalidDataException($"Unsupported version: {version}");

            // Read entry count
            file.Position = 4;
            int count = reader.ReadInt32();
            if (!Utils.IsSaneCount(count))
                throw new InvalidDataException($"Invalid entry count: {count}");

            // Read data offset
            uint dataOffset = reader.ReadUInt32();
            if (dataOffset <= 12 || dataOffset >= file.Length)
                throw new InvalidDataException("Invalid data offset.");

            var entries = ReadEntries(reader, count, dataOffset);
            ExtractEntries(reader, entries, folderName);
        }

        private List<Entry> ReadEntries(BinaryReader reader, int count, uint dataOffset)
        {
            using var indexData = Decompress(reader, 12, dataOffset - 12);
            using var indexReader = new BinaryReader(indexData);

            var entries = new List<Entry>(count);

            for (int i = 0; i < count; ++i)
            {
                long currentPosition = indexReader.BaseStream.Position;

                string name = indexReader.ReadCString(0xF8);

                long remainingNameBytes = 0xF8 - (indexReader.BaseStream.Position - currentPosition);
                if (remainingNameBytes > 0)
                {
                    indexReader.BaseStream.Position += remainingNameBytes;
                }

                uint offset = indexReader.ReadUInt32();
                uint size = indexReader.ReadUInt32();
                uint unpackedSize = indexReader.ReadUInt32();

                var entry = new Entry
                {
                    Name = name,
                    Offset = offset + dataOffset,
                    Size = size,
                    UnpackedSize = unpackedSize,
                    IsPacked = true
                };

                if (size == 0)
                {
                    throw new InvalidDataException(
                        $"Zero size detected for entry {entry.Name} at position {currentPosition}");
                }

                if (!entry.CheckPlacement(reader.BaseStream.Length))
                {
                    throw new InvalidDataException(
                        $"Invalid file placement for entry {entry.Name} " +
                        $"(Offset: {entry.Offset}, Size: {size}, " +
                        $"MaxOffset: {reader.BaseStream.Length}, " +
                        $"Position in index: {currentPosition})");
                }

                entries.Add(entry);
            }

            return entries;
        }

        private void ExtractEntries(BinaryReader reader, List<Entry> entries, string folderName)
        {
            Directory.CreateDirectory(folderName);

            foreach (var entry in entries)
            {
                string outputPath = Path.Combine(folderName, entry.Name);
                string? outputDir = Path.GetDirectoryName(outputPath);

                if (!string.IsNullOrEmpty(outputDir))
                    Directory.CreateDirectory(outputDir);

                using var outputStream = File.Create(outputPath);

                reader.BaseStream.Position = entry.Offset;

                if (entry.IsPacked)
                {
                    using var unpackedData = Decompress(reader, entry.Offset, entry.Size);
                    unpackedData.CopyTo(outputStream);
                }
                else
                {
                    var data = reader.ReadBytes((int)entry.Size);
                    outputStream.Write(data, 0, data.Length);
                }
            }
        }

        private Stream Decompress(BinaryReader reader, long offset, uint packedSize)
        {
            reader.BaseStream.Position = offset;

            uint unpackedSize = Utils.SwapEndian(reader.ReadUInt32());

            reader.BaseStream.Position = offset + 8;
            bool isPacked = reader.ReadByte() != 0;

            reader.BaseStream.Position = offset + 9;

            if (!isPacked)
            {
                var data = reader.ReadBytes((int)unpackedSize);
                return new MemoryStream(data);
            }

            var compressedData = reader.ReadBytes((int)(packedSize - 9));
            using var input = new MemoryStream(compressedData);
            var compression = new MpkCompression(input, (int)unpackedSize);
            return new MemoryStream(compression.Unpack());
        }
    }
}
