using System;

namespace Kurumi
{
    public static class Utils
    {
        /// <summary>
        /// Validates that entry count is within a sane range.
        /// </summary>
        public static bool IsSaneCount(int count) =>
            count > 0 && count <= 400000;

        /// <summary>
        /// Swaps endian order of a 32-bit unsigned integer.
        /// </summary>
        public static uint SwapEndian(uint value) =>
            (value >> 24) |
            ((value >> 8) & 0x0000FF00) |
            ((value << 8) & 0x00FF0000) |
            (value << 24);
    }
}
