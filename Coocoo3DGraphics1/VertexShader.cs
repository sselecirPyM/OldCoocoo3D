using System;
using System.Collections.Generic;
using System.Text;
using Vortice.D3DCompiler;
using Vortice.Direct3D;

namespace Coocoo3DGraphics
{
    public class VertexShader
    {
        public static VertexShader CompileAndCreate(byte[] source)
        {
            return CompileAndCreate(source, "main");
        }
        public static VertexShader CompileAndCreate(byte[] source, string entryPoint)
        {
            VertexShader vertexShader = new VertexShader();
            var hr = Compiler.Compile(source, entryPoint, null, "vs_5_0", out Blob compiled, out Blob errorBlob);

            vertexShader.compiledCode = compiled.GetBytes();
            compiled.Dispose();
            errorBlob.Dispose();
            return vertexShader;
        }
        public void Initialize(byte[] data)
        {
            this.compiledCode = new byte[data.Length];
            Array.Copy(data, this.compiledCode, data.Length);
        }
        public byte[] compiledCode;
    }
}
