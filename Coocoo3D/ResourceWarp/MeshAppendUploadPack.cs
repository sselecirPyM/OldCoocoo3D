using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.ResourceWarp
{
    public class MeshAppendUploadPack
    {
        public MMDMeshAppend mesh;
        public byte[] data;
        public MeshAppendUploadPack(MMDMeshAppend mesh, byte[] data)
        {
            this.mesh = mesh;
            this.data = data;
        }
    }
}
