using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics
{
    public class RTBottomLevelAccelerationStruct : IDisposable
    {
        public void Dispose()
        {
            resource?.Release();
            resource = null;
        }

        public bool initialized = false;
        public int startIndex;
        public int indexCount;
        public MMDMesh mesh;
        public MMDMesh meshOverride;
        public ID3D12Resource resource;
        internal int size;
    }
}
