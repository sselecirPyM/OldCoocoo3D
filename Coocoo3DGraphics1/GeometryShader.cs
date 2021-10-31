using System;
using System.Collections.Generic;
using System.Text;
using Vortice.D3DCompiler;
using Vortice.Direct3D;

namespace Coocoo3DGraphics
{
    public class GeometryShader
    {
        public static GeometryShader CompileAndCreate(byte[] source)
        {
            return CompileAndCreate(source, "main");
        }
        public static GeometryShader CompileAndCreate(byte[] source, string entryPoint)
        {
            GeometryShader geometryShader = new GeometryShader();
            var hr = Compiler.Compile(source, entryPoint, null, "gs_5_0", out Blob compiled, out Blob errorBlob);

            geometryShader.compiledCode = compiled.GetBytes();
            compiled.Dispose();
            errorBlob.Dispose();
            return geometryShader;
        }
        public void Initialize(byte[] data)
        {
            this.compiledCode = new byte[data.Length];
            Array.Copy(data, this.compiledCode, data.Length);
        }
        public byte[] compiledCode;
    }
}
