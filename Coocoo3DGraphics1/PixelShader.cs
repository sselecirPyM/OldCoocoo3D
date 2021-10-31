using System;
using System.Collections.Generic;
using System.Text;
using Vortice.D3DCompiler;
using Vortice.Direct3D;

namespace Coocoo3DGraphics
{
    public class PixelShader
    {
        public static PixelShader CompileAndCreate(byte[] source)
        {
            return CompileAndCreate(source, "main");
        }
        public static PixelShader CompileAndCreate(byte[] source, string entryPoint)
        {
            PixelShader pixelShader = new PixelShader();
            var hr = Compiler.Compile(source, entryPoint, null, "ps_5_0", out Blob compiled, out Blob errorBlob);

            pixelShader.compiledCode = compiled.GetBytes();
            compiled.Dispose();
            errorBlob.Dispose();
            return pixelShader;
        }
        public void Initialize(byte[] data)
        {
            this.compiledCode = new byte[data.Length];
            Array.Copy(data, this.compiledCode, data.Length);
        }
        public byte[] compiledCode;
    }
}
