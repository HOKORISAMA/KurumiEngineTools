using System.Text;
using System.Linq;
using System.Collections.Generic;

namespace Kurumi
{
	public class Entry
    {
        public string? Name { get; set; }
        public uint Offset { get; set; }
        public uint Size { get; set; }
        public uint UnpackedSize { get; set; }
        public bool IsPacked { get; set; }

        public bool CheckPlacement(long maxOffset)
        {
            return Offset < maxOffset && Size > 0 && Offset + Size <= maxOffset;
        }
    }
	
	public static class BinaryReaderExtensions
    {
        public static string ReadCString(this BinaryReader reader, int maxLength)
        {
            var bytes = new List<byte>();

            for (int i = 0; i < maxLength; i++)
            {
                byte b = reader.ReadByte();
                if (b == 0)
                    break;
                bytes.Add(b);
            }

            return Encoding.GetEncoding("Shift_jis").GetString(bytes.ToArray());
        }

        public static string ReadString(this BinaryReader reader, int length)
        {
            byte[] buffer = new byte[length];
            if (reader.Read(buffer, 0, length) != length)
                throw new EndOfStreamException();

            return Encoding.GetEncoding("Shift_jis").GetString(buffer.ToArray());
        }
    }
}
   