using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics
{
    public class RTPSO
    {
        public string[] rayGenShaders;
        public string[] hitShaders;
        public string[] missShaders;
        public byte[] datas;
        public ID3D12StateObject so;
    }
}
