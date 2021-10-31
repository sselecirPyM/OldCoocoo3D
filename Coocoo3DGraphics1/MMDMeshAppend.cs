using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D12;

namespace Coocoo3DGraphics
{
    public class MMDMeshAppend
    {
        public const int c_vertexStride = 12;

        public ID3D12Resource vertexBufferPos;
        public ID3D12Resource vertexBufferPosUpload;
        public VertexBufferView vertexBufferPosViews;
        public int lastUpdateIndexs = 0;
        public int posCount;
        public int bufferSize;

        public void Reload(int count)
        {
            posCount = count;
            bufferSize = (count * c_vertexStride + 255) & ~255;
        }
    }
}
