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
        MMDMesh meshInstance;

        public GraphicsObjectStatus Status;

        public void Reload(BinaryReader reader)
        {
            pmx.Reload(reader);

            verticesDataAnotherPart = new byte[pmx.Vertices.Length * c_vertexStride];
            verticesDataPosPart = new byte[pmx.Vertices.Length * c_vertexStride2];
            for (int i = 0; i < pmx.Vertices.Length; i++)
            {
                Span<PMX_Vertex.VertexStruct> vertData = MemoryMarshal.Cast<byte, PMX_Vertex.VertexStruct>(new Span<byte>(verticesDataAnotherPart, i * c_vertexStride, 24));
                Span<ushort> boneIdSpan = MemoryMarshal.Cast<byte, ushort>(new Span<byte>(verticesDataAnotherPart, i * c_vertexStride + 24, 8));
                Span<Vector4> boneWeightSpan = MemoryMarshal.Cast<byte, Vector4>(new Span<byte>(verticesDataAnotherPart, i * c_vertexStride + 32, 16));
                vertData[0] = pmx.Vertices[i].innerStruct;
                boneIdSpan[0] = (ushort)pmx.Vertices[i].boneId0;
                boneIdSpan[1] = (ushort)pmx.Vertices[i].boneId1;
                boneIdSpan[2] = (ushort)pmx.Vertices[i].boneId2;
                boneIdSpan[3] = (ushort)pmx.Vertices[i].boneId3;
                boneWeightSpan[0] = pmx.Vertices[i].Weights;
            }

            Span<Vector3> vector3s = MemoryMarshal.Cast<byte, Vector3>(verticesDataPosPart);
            for (int i = 0; i < pmx.Vertices.Length; i++)
            {
                vector3s[i] = pmx.Vertices[i].Coordinate;
            }

        }
        public MMDMesh GetMesh()
        {
            if (meshInstance == null)
                meshInstance = MMDMesh.Load1(verticesDataAnotherPart, pmx.TriangleIndexs, c_vertexStride, PrimitiveTopology._POINTLIST);
            return meshInstance;
        }
    }
}
