using System;
using System.Collections.Generic;
using System.Text;
using Vortice.D3DCompiler;
using Vortice.Direct3D;

namespace Coocoo3DGraphics1
{
    public class PixelShader
    {
        public static PixelShader CompileAndCreate(GraphicsDevice graphicsDevice, byte[] source)
        {
            return CompileAndCreate(graphicsDevice, source, "main");
        }
        public static PixelShader CompileAndCreate(GraphicsDevice graphicsDevice, byte[] source, string entryPoint)
        {
            PixelShader vertexShader = new PixelShader();
            var hr = Compiler.Compile(source, entryPoint, null, "ps_5_0", out vertexShader.compiledCode, out Blob errorBlob);

            return vertexShader;
        }
        public Blob compiledCode;
    }
}
