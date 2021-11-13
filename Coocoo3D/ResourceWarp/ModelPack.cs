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
using Vortice.Direct3D;

namespace Coocoo3D.ResourceWarp
{
    public class ModelPack
    {
        const int c_vertexStride2 = 12;

        public PMXFormat pmx = new PMXFormat();

        public string fullPath;
        public volatile Task LoadTask;

        public byte[] verticesDataPosPart;
        public Vector3[] position;
        public Vector3[] normal;
        public Vector2[] uv;
        public ushort[] boneId;
        public float[] boneWeights;
        public Vector3[] tangent;
        MMDMesh meshInstance;
        public int vertexCount;

        public GraphicsObjectStatus Status;

        public void Reload(BinaryReader reader)
        {
            pmx.Reload(reader);
            vertexCount = pmx.Vertices.Length;
            verticesDataPosPart = new byte[pmx.Vertices.Length * c_vertexStride2];
            position = new Vector3[pmx.Vertices.Length];
            normal = new Vector3[pmx.Vertices.Length];
            uv = new Vector2[pmx.Vertices.Length];
            boneId = new ushort[pmx.Vertices.Length * 4];
            boneWeights = new float[pmx.Vertices.Length * 4];
            tangent = new Vector3[pmx.Vertices.Length];
            for (int i = 0; i < pmx.Vertices.Length; i++)
            {
                position[i] = pmx.Vertices[i].Coordinate;
                normal[i] = pmx.Vertices[i].Normal;
                uv[i] = pmx.Vertices[i].UvCoordinate;
                boneId[i * 4 + 0] = (ushort)pmx.Vertices[i].boneId0;
                boneId[i * 4 + 1] = (ushort)pmx.Vertices[i].boneId1;
                boneId[i * 4 + 2] = (ushort)pmx.Vertices[i].boneId2;
                boneId[i * 4 + 3] = (ushort)pmx.Vertices[i].boneId3;
                float weightTotal = 0;
                boneWeights[i * 4 + 0] = pmx.Vertices[i].Weights.X;
                boneWeights[i * 4 + 1] = pmx.Vertices[i].Weights.Y;
                boneWeights[i * 4 + 2] = pmx.Vertices[i].Weights.Z;
                boneWeights[i * 4 + 3] = pmx.Vertices[i].Weights.W;
                weightTotal = boneWeights[i * 4 + 0] + boneWeights[i * 4 + 1] + boneWeights[i * 4 + 2] + boneWeights[i * 4 + 3];
                boneWeights[i * 4 + 0] /= weightTotal;
                boneWeights[i * 4 + 1] /= weightTotal;
                boneWeights[i * 4 + 2] /= weightTotal;
                boneWeights[i * 4 + 3] /= weightTotal;
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
            {
                meshInstance = new MMDMesh();
                meshInstance.ReloadIndex<int>(vertexCount, pmx.TriangleIndexs);
                meshInstance.AddBuffer<Vector3>(position, 0);
                meshInstance.AddBuffer<Vector3>(normal, 1);
                meshInstance.AddBuffer<Vector2>(uv, 2);
                meshInstance.AddBuffer<ushort>(boneId, 3);
                meshInstance.AddBuffer<float>(boneWeights, 4);
                meshInstance.AddBuffer<Vector3>(tangent, 5);
            }
            return meshInstance;
        }
    }
}
