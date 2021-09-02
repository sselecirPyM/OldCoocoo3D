using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Coocoo3D.Utility
{
    public static class CooUtility
    {
        public static int Write(byte[] array, int startIndex, Matrix4x4 value)
        {
            MemoryMarshal.Write(new Span<byte>(array, startIndex, 64), ref value);
            return 64;
        }
        public static int Write(byte[] array, int startIndex, Vector4 value)
        {
            MemoryMarshal.Write(new Span<byte>(array, startIndex, 16), ref value);
            return 16;
        }
        public static int Write(byte[] array, int startIndex, Vector3 value)
        {
            MemoryMarshal.Write(new Span<byte>(array, startIndex, 12), ref value);
            return 12;
        }
        public static int Write(byte[] array, int startIndex, Vector2 value)
        {
            MemoryMarshal.Write(new Span<byte>(array, startIndex, 8), ref value);
            return 8;
        }
        public static int Write(byte[] array, int startIndex, uint value)
        {
            MemoryMarshal.Write(new Span<byte>(array, startIndex, 4), ref value);
            return 4;
        }
        public static int Write(byte[] array, int startIndex, int value)
        {
            MemoryMarshal.Write(new Span<byte>(array, startIndex, 4), ref value);
            return 4;
        }
        public static int Write(byte[] array, int startIndex, float value)
        {
            MemoryMarshal.Write(new Span<byte>(array, startIndex, 4), ref value);
            return 4;
        }
    }
}
