using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D;

namespace Coocoo3DGraphics
{
    public class PixelShader
    {
        public PixelShader()
        {

        }
        public PixelShader(byte[] data)
        {
            Initialize(data);
        }
        public void Initialize(byte[] data)
        {
            this.compiledCode = new byte[data.Length];
            Array.Copy(data, this.compiledCode, data.Length);
        }
        public byte[] compiledCode;
    }
}
