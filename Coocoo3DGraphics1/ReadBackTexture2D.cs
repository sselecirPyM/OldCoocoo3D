using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics
{
    public class ReadBackTexture2D
    {
        public void Reload(int width, int height, int bytesPerPixel)
        {
            m_width = width;
            m_height = height;
            this.bytesPerPixel = bytesPerPixel;
        }
        unsafe public void GetRaw<T>(int index, T[] bitmapData) where T : unmanaged
        {
            GetRaw(index, bitmapData);
        }
        unsafe public void GetRaw<T>(int index, Span<T> bitmapData) where T : unmanaged
        {
            IntPtr ptr = m_textureReadBack[index].Map(0);
            int size = Marshal.SizeOf(typeof(T));
            Span<T> a = new Span<T>(ptr.ToPointer(), m_width * m_height * bytesPerPixel / size);
            a.CopyTo(bitmapData);
            m_textureReadBack[index].Unmap(0);
        }
        public int GetWidth()
        {
            return m_width;
        }
        public int GetHeight()
        {
            return m_height;
        }
        public ID3D12Resource[] m_textureReadBack;
        public int m_width;
        public int m_height;
        public int bytesPerPixel;
    }
}
