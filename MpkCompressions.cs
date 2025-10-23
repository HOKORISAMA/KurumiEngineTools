using System;
using System.IO;

namespace Kurumi
{
    internal sealed class MpkPacking
    {
        private byte[] m_input;
        
        public MpkPacking(byte[] input)
        {
            m_input = input;
        }
        
        public byte[] Pack()
        {
            using (var output = new MemoryStream())
            using (var writer = new BinaryWriter(output))
            {
                // Create blocks of data that the unpacker can process
                int offset = 0;
                
                while (offset < m_input.Length)
                {
                    int blockSize = Math.Min(0x8000, m_input.Length - offset); // 32KB blocks
                    WriteBlock(writer, m_input, offset, blockSize);
                    offset += blockSize;
                }
                
                return output.ToArray();
            }
        }
        
        private void WriteBlock(BinaryWriter writer, byte[] data, int offset, int length)
        {
            // Block header: number of symbols in this block (16-bit)
            writer.Write((ushort)length);
            
            // Write tree information for literals (simplified - all literals have same bit length)
            // Tree count for code lengths (5 bits for 19 symbols)
            WriteBits(writer, 1, 5); // Only 1 code length symbol (means all have same length)
            WriteBits(writer, 0, 5); // First symbol index
            WriteBits(writer, 0, 2); // No repetition
            
            // Build a simple Huffman table - all literals use 8 bits
            // For simplicity, we'll create a degenerate tree where each byte maps to itself
            
            // Tree information for main tree (simplified)
            WriteBits(writer, length > 0 ? 9 : 0, 9); // Number of symbols
            
            if (length > 0)
            {
                // Create a simple mapping where each literal is encoded as itself
                for (int i = 0; i < 256; i++)
                {
                    // Each literal gets 8 bits depth
                    WriteBits(writer, 8 - 2, 4); // depth - 2 (since we subtract 2 in the decoder)
                }
                
                // Pad remaining symbols with 0
                for (int i = 256; i < 0x1FE; i++)
                {
                    WriteBits(writer, 0, 4);
                }
            }
            
            // Write the actual literal data
            for (int i = 0; i < length; i++)
            {
                WriteBits(writer, data[offset + i], 8);
            }
            
            FlushBits(writer);
        }
        
        private int m_bitBuffer = 0;
        private int m_bitsInBuffer = 0;
        
        private void WriteBits(BinaryWriter writer, int value, int bitCount)
        {
            m_bitBuffer |= (value << m_bitsInBuffer);
            m_bitsInBuffer += bitCount;
            
            while (m_bitsInBuffer >= 8)
            {
                writer.Write((byte)(m_bitBuffer & 0xFF));
                m_bitBuffer >>= 8;
                m_bitsInBuffer -= 8;
            }
        }
        
        private void FlushBits(BinaryWriter writer)
        {
            if (m_bitsInBuffer > 0)
            {
                writer.Write((byte)(m_bitBuffer & 0xFF));
                m_bitBuffer = 0;
                m_bitsInBuffer = 0;
            }
        }
    }
}