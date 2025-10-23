using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Kurumi
{
    public partial class Kurumi
    {
        public void Pack(string foldername, string filepath, bool compressFiles = true, bool compressIndex = false)
        {
            if (!Directory.Exists(foldername))
                throw new DirectoryNotFoundException($"Source folder '{foldername}' not found.");

            var files = Directory.GetFiles(foldername);
            if (files.Length == 0)
                throw new InvalidOperationException("No files found in the source folder.");

            var rawIndex = new List<byte>();

            // Build initial index structure
            foreach (var file in files)
            {
                var filename = Path.GetFileName(file);
                var nameBytes = Encoding.GetEncoding("shift_jis").GetBytes(filename);
                var nameEntry = new byte[0xF8];
                Array.Copy(nameBytes, nameEntry, Math.Min(nameBytes.Length, 0xF7)); // Leave last byte as null terminator
                rawIndex.AddRange(nameEntry);
                rawIndex.AddRange(new byte[12]); // Placeholder for offset, size, unpacked size
            }

            // Calculate base offsets
            int headerSize = 12; // MP + version + file count + data offset
            var indexHeaderSize = 9; // unpacked size + reserved + packed flag
            var rawIndexData = rawIndex.ToArray();
            byte[] packedIndex = null;

            // Pack index if requested
            if (compressIndex)
            {
                var indexPacker = new MpkPacking(rawIndexData);
                packedIndex = indexPacker.Pack();
            }

            int indexTotalSize = indexHeaderSize + (compressIndex ? packedIndex.Length : rawIndexData.Length);
            int dataOffset = headerSize + indexTotalSize;
            int currentFileOffset = 0;

            // Process files and update index
            var processedFiles = new List<(byte[] data, int unpackedSize, bool isPacked)>();
            for (int i = 0; i < files.Length; i++)
            {
                var file = files[i];
                var rawData = File.ReadAllBytes(file);
                byte[] finalData;
                bool filePacked = false;

                if (compressFiles)
                {
                    try
                    {
                        var packer = new MpkPacking(rawData);
                        finalData = packer.Pack();
                        filePacked = true;
                        
                        // Only use compressed data if it's actually smaller
                        if (finalData.Length >= rawData.Length)
                        {
                            finalData = rawData;
                            filePacked = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Fall back to uncompressed if compression fails
                        Console.WriteLine($"Warning: Compression failed for {Path.GetFileName(file)}: {ex.Message}");
                        finalData = rawData;
                        filePacked = false;
                    }
                }
                else
                {
                    finalData = rawData;
                    filePacked = false;
                }

                // Update index entries in rawIndexData
                int indexEntryPos = i * (0xF8 + 12);
                BitConverter.GetBytes(currentFileOffset)
                    .CopyTo(rawIndexData, indexEntryPos + 0xF8);
                BitConverter.GetBytes(finalData.Length + 9) // +9 for file header
                    .CopyTo(rawIndexData, indexEntryPos + 0xF8 + 4);
                BitConverter.GetBytes(rawData.Length)
                    .CopyTo(rawIndexData, indexEntryPos + 0xF8 + 8);

                processedFiles.Add((finalData, rawData.Length, filePacked));
                currentFileOffset += finalData.Length + 9; // +9 for file header
            }

            // Re-pack index after updating entries if compression is enabled
            if (compressIndex)
            {
                try
                {
                    var indexPacker = new MpkPacking(rawIndexData);
                    packedIndex = indexPacker.Pack();
                    
                    // Only use compressed index if it's actually smaller
                    if (packedIndex.Length >= rawIndexData.Length)
                    {
                        packedIndex = null;
                        compressIndex = false;
                        indexTotalSize = indexHeaderSize + rawIndexData.Length;
                        
                        // Recalculate data offset
                        dataOffset = headerSize + indexTotalSize;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Index compression failed: {ex.Message}");
                    packedIndex = null;
                    compressIndex = false;
                    indexTotalSize = indexHeaderSize + rawIndexData.Length;
                    dataOffset = headerSize + indexTotalSize;
                }
            }

            using (var fs = new FileStream(filepath, FileMode.Create, FileAccess.Write))
            {
                // Write main header
                fs.Write(Encoding.ASCII.GetBytes("MP"), 0, 2);
                fs.Write(new byte[] { 0, 0 }, 0, 2); // Version (2 bytes)
                fs.Write(BitConverter.GetBytes(files.Length), 0, 4);
                fs.Write(BitConverter.GetBytes(dataOffset), 0, 4);

                // Write index header (big-endian unpacked size)
                var unpackedSizeBytes = BitConverter.GetBytes(rawIndexData.Length);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(unpackedSizeBytes);
                fs.Write(unpackedSizeBytes, 0, 4);
                
                fs.Write(new byte[4], 0, 4); // Reserved
                fs.WriteByte((byte)(compressIndex ? 1 : 0)); // Packed flag

                // Write index data
                if (compressIndex && packedIndex != null)
                    fs.Write(packedIndex, 0, packedIndex.Length);
                else
                    fs.Write(rawIndexData, 0, rawIndexData.Length);

                // Write file data
                foreach (var (fileData, unpackedSize, isPacked) in processedFiles)
                {
                    // Write file header (big-endian unpacked size)
                    var fileSizeBytes = BitConverter.GetBytes(unpackedSize);
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(fileSizeBytes);
                    fs.Write(fileSizeBytes, 0, 4);
                    
                    fs.Write(new byte[4], 0, 4); // Reserved
                    fs.WriteByte((byte)(isPacked ? 1 : 0)); // Packed flag
                    fs.Write(fileData, 0, fileData.Length);
                }
            }

            Console.WriteLine($"Successfully packed {files.Length} files to {filepath}");
        }

        // Overload for backward compatibility
        public void Pack(string foldername, string filepath)
        {
            Pack(foldername, filepath, compressFiles: true, compressIndex: false);
        }
    }
}