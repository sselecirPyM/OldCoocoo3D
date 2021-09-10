using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics1
{
    public class CBuffer
    {
        public int size;
        public bool Mutable;
        public ID3D12Resource resource;
    }
}
