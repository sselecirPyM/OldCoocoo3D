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
        GCHandle gch_vertPos;
        MMDMesh meshInstance;

        public GraphicsObjectStatus Status;

        public void Reload2(BinaryReader reader)
        {
            pmx.Reload(reader);

            if (gch_vertAnother.IsAllocated) gch_vertAnother.Free();
            if (gch_vertPos.IsAllocated) gch_vertPos.Free();
            verticesDataAnotherPart = new byte[pmx.Vertices.Length * c_vertexStride];
            verticesDataPosPart = new byte[pmx.Vertices.Length * c_vertexStride2];
            gch_vertAnother = GCHandle.Alloc(verticesDataAnotherPart);
            gch_vertPos = GCHandle.Alloc(verticesDataPosPart);
            IntPtr ptr = Marshal.UnsafeAddrOfPinnedArrayElement(verticesDataAnotherPart, 0);
            int[] test = new int[4];
            float[] test1 = new float[4];
            void _SortVertexSkinning(ref PMX_Vertex vertex, out Vector4 v)//for optimization
            {
                test[0] = vertex.boneId0;
                test[1] = vertex.boneId1;
                test[2] = vertex.boneId2;
                test[3] = vertex.boneId3;
                test1[0] = vertex.Weights.X;
                test1[1] = vertex.Weights.Y;
                test1[2] = vertex.Weights.Z;
                test1[3] = vertex.Weights.W;
                for (int i = 0; i < 3; i++)
                {
                    for (int j = i; j < 3; j++)
                    {
                        if ((uint)test[j] > (uint)test[j + 1])
                        {
                            int a = test[j];
                            test[j] = test[j + 1];
                            test[j + 1] = a;
                            float b = test1[j];
                            test1[j] = test1[j + 1];
                            test1[j + 1] = b;
                        }
                    }
                }
                v = new Vector4(test1[0], test1[1], test1[2], test1[3]);
            }
            for (int i = 0; i < pmx.Vertices.Length; i++)
            {
                Marshal.StructureToPtr(pmx.Vertices[i].innerStruct, ptr, true);

                _SortVertexSkinning(ref pmx.Vertices[i], out Vector4 weights);
                Marshal.WriteInt32(ptr + 24 + 0 * 2, test[0]);
                Marshal.WriteInt32(ptr + 24 + 1 * 2, test[1]);
                Marshal.WriteInt32(ptr + 24 + 2 * 2, test[2]);
                Marshal.WriteInt32(ptr + 24 + 3 * 2, test[3]);//ushort
                Marshal.StructureToPtr(weights, ptr + 24 + 8, true);

                ptr += c_vertexStride;
            }
            IntPtr ptr2 = Marshal.UnsafeAddrOfPinnedArrayElement(verticesDataPosPart, 0);
            for (int i = 0; i < pmx.Vertices.Length; i++)
            {
                Marshal.StructureToPtr(pmx.Vertices[i].Coordinate, ptr2 + i * c_vertexStride2, true);
            }
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
            if (gch_vertPos.IsAllocated) gch_vertPos.Free();
        }
    }
}
