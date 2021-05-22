using Coocoo3D.FileFormat;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Coocoo3D.ResourceWarp
{
    public class ModelPack
    {
        const int c_vertexStride = 64;
        const int c_vertexStride2 = 12;

        public PMXFormat pmx = new PMXFormat();

        public DateTimeOffset lastModifiedTime;
        public StorageFolder folder;
        public string fullPath;
        public string relativePath;
        public SingleLocker loadLocker;
        public volatile Task LoadTask;

        public byte[] verticesDataAnotherPart;
        public byte[] verticesDataPosPart;
        GCHandle gch_vertAnother;
        MMDMesh meshInstance;

        public GraphicsObjectStatus Status;

        public void Reload2(BinaryReader reader)
        {
            pmx.Reload(reader);

            if (gch_vertAnother.IsAllocated) gch_vertAnother.Free();
            verticesDataAnotherPart = new byte[pmx.Vertices.Length * c_vertexStride];
            verticesDataPosPart = new byte[pmx.Vertices.Length * c_vertexStride2];
            gch_vertAnother = GCHandle.Alloc(verticesDataAnotherPart);
            IntPtr ptr = Marshal.UnsafeAddrOfPinnedArrayElement(verticesDataAnotherPart, 0);
            for (int i = 0; i < pmx.Vertices.Length; i++)
            {
                Marshal.StructureToPtr(pmx.Vertices[i].innerStruct, ptr, true);

                Marshal.WriteInt32(ptr + 24 + 0 * 2, pmx.Vertices[i].boneId0);
                Marshal.WriteInt32(ptr + 24 + 1 * 2, pmx.Vertices[i].boneId1);
                Marshal.WriteInt32(ptr + 24 + 2 * 2, pmx.Vertices[i].boneId2);
                Marshal.WriteInt32(ptr + 24 + 3 * 2, pmx.Vertices[i].boneId3);//ushort
                Marshal.StructureToPtr(pmx.Vertices[i].Weights, ptr + 24 + 8, true);

                ptr += c_vertexStride;
            }
            var gch_vertPos = GCHandle.Alloc(verticesDataPosPart);
            IntPtr ptr2 = Marshal.UnsafeAddrOfPinnedArrayElement(verticesDataPosPart, 0);
            for (int i = 0; i < pmx.Vertices.Length; i++)
            {
                Marshal.StructureToPtr(pmx.Vertices[i].Coordinate, ptr2 + i * c_vertexStride2, true);
            }
            if (gch_vertPos.IsAllocated) gch_vertPos.Free();
        }
        public MMDMesh GetMesh()
        {
            if (meshInstance == null)
                meshInstance = MMDMesh.Load1(verticesDataAnotherPart, pmx.TriangleIndexs, c_vertexStride, PrimitiveTopology._POINTLIST);
            return meshInstance;
        }

        ~ModelPack()
        {
            if (gch_vertAnother.IsAllocated) gch_vertAnother.Free();
        }
    }
}
