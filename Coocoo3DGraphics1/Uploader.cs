using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Vortice.DXGI;
using static Coocoo3DGraphics.DXHelper;

namespace Coocoo3DGraphics
{
    public class Uploader
    {
        public int m_width;
        public int m_height;
        public int m_mipLevels;
        public Format m_format;

        public byte[] m_data;
        public void Texture2DRaw(byte[] rawData, Format format, int width, int height)
        {
            m_width = width;
            m_height = height;
            m_format = format;
            m_mipLevels = 1;
            m_data = new byte[rawData.Length];
            Array.Copy(rawData, m_data, rawData.Length);
        }

        public void Texture2DRaw(byte[] rawData, Format format, int width, int height, int mipLevel)
        {
            m_width = width;
            m_height = height;
            m_format = format;
            m_mipLevels = mipLevel;
            m_data = new byte[rawData.Length];
            Array.Copy(rawData, m_data, rawData.Length);
        }

        public void Texture2DPure(int width, int height, Vector4 color)
        {
            m_width = width;
            m_height = height;
            m_format = Format.R32G32B32A32_Float;
            m_mipLevels = 1;
            int count = width * height;
            m_data = new byte[count * 16];
            var d1 = MemoryMarshal.Cast<byte, Vector4>(m_data);
            for (int i = 0; i < d1.Length; i++)
            {
                d1[i] = color;
            }
        }

        public void TextureCubeRaw(byte[] rawData, Format format, int width, int height, int mipLevel)
        {

            m_width = width;
            m_height = height;
            m_format = format;
            m_mipLevels = mipLevel;
            m_data = new byte[rawData.Length];
            Array.Copy(rawData, m_data, rawData.Length);
        }

        public void TextureCubePure(int width, int height, Vector4[] color)
        {
            m_width = width;
            m_height = height;
            m_format = Format.R32G32B32A32_Float;
            m_mipLevels = 1;
            int count = width * height;
            if (count < 256) throw new NotImplementedException("Texture too small");
            m_data = new byte[count * 16 * 6];
            var d1 = MemoryMarshal.Cast<byte, Vector4>(m_data);
            for (int j = 0; j < 6; j++)
                for (int i = 0; i < count; i++)
                {
                    d1[i + j * count] = color[j];
                }
        }
    }
}
