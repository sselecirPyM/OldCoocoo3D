using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics
{
    public class CBuffer
    {
        public int size;
        public bool Mutable;
        public int lastUpdateIndex;
        public ID3D12Resource resource;
        public ID3D12Resource resourceUpload;
        public IntPtr mappedResource;

        public ulong GetCurrentVirtualAddress()
        {
            if (Mutable)
                return resource.GPUVirtualAddress + (ulong)(size * lastUpdateIndex);
            else
                return resource.GPUVirtualAddress;
        }
    }
}
