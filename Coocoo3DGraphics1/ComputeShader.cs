using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics
{
    public class ComputeShader
    {
        public byte[] data;
        public Dictionary<ID3D12RootSignature, ID3D12PipelineState> computeShaders = new Dictionary<ID3D12RootSignature, ID3D12PipelineState>();
        public void Initialize(byte[]data)
        {
            this.data = new byte[data.Length];
            Array.Copy(data, this.data, data.Length);
        }
    }
}
