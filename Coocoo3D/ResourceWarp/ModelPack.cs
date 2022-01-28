using Coocoo3D.Components;
using Coocoo3D.FileFormat;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Mesh = Coocoo3DGraphics.Mesh;

namespace Coocoo3D.ResourceWarp
{
    public class ModelPack
    {
        public PMXFormat pmx = new PMXFormat();

        public string fullPath;

        public Vector3[] position;
        public Vector3[] normal;
        public Vector4[] tangent;
        public Vector2[] uv;
        public ushort[] boneId;
        public float[] boneWeights;
        public int[] indices;
        Mesh meshInstance;
        public int vertexCount;
        public bool skinning;
        public List<string> textures;

        public List<RuntimeMaterial> Materials = new List<RuntimeMaterial>();

        public List<RigidBodyDesc> rigidBodyDescs = new List<RigidBodyDesc>();
        public List<JointDesc> jointDescs = new List<JointDesc>();

        public static RigidBodyDesc GetRigidBodyDesc(PMX_RigidBody rigidBody)
        {
            RigidBodyDesc desc = new RigidBodyDesc();
            desc.AssociatedBoneIndex = rigidBody.AssociatedBoneIndex;
            desc.CollisionGroup = rigidBody.CollisionGroup;
            desc.CollisionMask = rigidBody.CollisionMask;
            desc.Shape = (RigidBodyShape)rigidBody.Shape;
            desc.Dimemsions = rigidBody.Dimemsions;
            desc.Position = rigidBody.Position;
            desc.Rotation = MMDRendererComponent.ToQuaternion(rigidBody.Rotation);
            desc.Mass = rigidBody.Mass;
            desc.LinearDamping = rigidBody.TranslateDamp;
            desc.AngularDamping = rigidBody.RotateDamp;
            desc.Restitution = rigidBody.Restitution;
            desc.Friction = rigidBody.Friction;
            desc.Type = (RigidBodyType)rigidBody.Type;
            return desc;
        }
        public static JointDesc GetJointDesc(PMX_Joint joint)
        {
            JointDesc desc = new JointDesc();
            desc.Type = joint.Type;
            desc.AssociatedRigidBodyIndex1 = joint.AssociatedRigidBodyIndex1;
            desc.AssociatedRigidBodyIndex2 = joint.AssociatedRigidBodyIndex2;
            desc.Position = joint.Position;
            desc.Rotation = joint.Rotation;
            desc.PositionMinimum = joint.PositionMinimum;
            desc.PositionMaximum = joint.PositionMaximum;
            desc.RotationMinimum = joint.RotationMinimum;
            desc.RotationMaximum = joint.RotationMaximum;
            desc.PositionSpring = joint.PositionSpring;
            desc.RotationSpring = joint.RotationSpring;
            return desc;
        }

        public void LoadModel(string fileName)
        {
            //textures = new List<string>();
            //unsafe
            //{
            //    using var assimp = Assimp.GetApi();

            //    var scene = assimp.ImportFile(fileName, (uint)PostProcessSteps.CalculateTangentSpace);
            //    Console.WriteLine("meshes: " + scene->MNumMeshes);
            //    for (int i = 0; i < scene->MNumMeshes; i++)
            //    {
            //        int vertCount = (int)scene->MMeshes[i]->MNumVertices;
            //        Console.WriteLine("verts: " + vertCount + " totalVerts: " + pmx.Vertices.Length);
            //    }
            //    for (int i = 0; i < scene->MNumTextures; i++)
            //        Console.WriteLine(scene->MTextures[i]->MFilename.AsString);
            //}
        }
        public void LoadPMX(BinaryReader reader, string fileName)
        {
            string storageFolder = Path.GetDirectoryName(fileName);
            pmx.Reload(reader);
            textures = new List<string>();
            foreach (var tex in pmx.Textures)
            {
                string relativePath = tex.TexturePath.Replace("//", "\\").Replace('/', '\\');
                string texPath = Path.GetFullPath(relativePath, storageFolder);
                textures.Add(texPath);
            }
            skinning = true;
            vertexCount = pmx.Vertices.Length;
            position = new Vector3[pmx.Vertices.Length];
            normal = new Vector3[pmx.Vertices.Length];
            uv = new Vector2[pmx.Vertices.Length];
            boneId = new ushort[pmx.Vertices.Length * 4];
            boneWeights = new float[pmx.Vertices.Length * 4];
            tangent = new Vector4[pmx.Vertices.Length];
            for (int i = 0; i < pmx.Vertices.Length; i++)
            {
                position[i] = pmx.Vertices[i].Coordinate;
                normal[i] = pmx.Vertices[i].Normal;
                uv[i] = pmx.Vertices[i].UvCoordinate;
                boneId[i * 4 + 0] = (ushort)pmx.Vertices[i].boneId0;
                boneId[i * 4 + 1] = (ushort)pmx.Vertices[i].boneId1;
                boneId[i * 4 + 2] = (ushort)pmx.Vertices[i].boneId2;
                boneId[i * 4 + 3] = (ushort)pmx.Vertices[i].boneId3;

                boneWeights[i * 4 + 0] = pmx.Vertices[i].Weights.X;
                boneWeights[i * 4 + 1] = pmx.Vertices[i].Weights.Y;
                boneWeights[i * 4 + 2] = pmx.Vertices[i].Weights.Z;
                boneWeights[i * 4 + 3] = pmx.Vertices[i].Weights.W;
                float weightTotal = boneWeights[i * 4 + 0] + boneWeights[i * 4 + 1] + boneWeights[i * 4 + 2] + boneWeights[i * 4 + 3];
                boneWeights[i * 4 + 0] /= weightTotal;
                boneWeights[i * 4 + 1] /= weightTotal;
                boneWeights[i * 4 + 2] /= weightTotal;
                boneWeights[i * 4 + 3] /= weightTotal;
            }

            indices = new int[pmx.TriangleIndexs.Length];
            pmx.TriangleIndexs.CopyTo(new Span<int>(indices));
            int indexOffset = 0;
            for (int i = 0; i < pmx.Materials.Count; i++)
            {
                var mmdMat = pmx.Materials[i];

                RuntimeMaterial mat = new RuntimeMaterial
                {
                    Name = mmdMat.Name,
                    indexCount = mmdMat.TriangeIndexNum,
                    indexOffset = indexOffset,
                };
                Vector3 min;
                Vector3 max;
                min = position[pmx.TriangleIndexs[indexOffset]];
                max = position[pmx.TriangleIndexs[indexOffset]];
                for (int k = 0; k < mat.indexCount; k++)
                {
                    min = Vector3.Min(min, position[pmx.TriangleIndexs[indexOffset + k]]);
                    max = Vector3.Max(max, position[pmx.TriangleIndexs[indexOffset + k]]);
                }

                mat.boundingBox = new Vortice.Mathematics.BoundingBox(min, max);
                mat.DrawDoubleFace = mmdMat.DrawFlags.HasFlag(PMX_DrawFlag.DrawDoubleFace);
                mat.Parameters["DiffuseColor"] = mmdMat.DiffuseColor;
                mat.Parameters["SpecularColor"] = mmdMat.SpecularColor;
                mat.Parameters["EdgeSize"] = mmdMat.EdgeScale;
                mat.Parameters["AmbientColor"] = mmdMat.AmbientColor;
                mat.Parameters["EdgeColor"] = mmdMat.EdgeColor;
                mat.Parameters["CastShadow"] = mmdMat.DrawFlags.HasFlag(PMX_DrawFlag.CastSelfShadow);
                mat.Parameters["ReceiveShadow"] = mmdMat.DrawFlags.HasFlag(PMX_DrawFlag.DrawSelfShadow);
                indexOffset += mmdMat.TriangeIndexNum;
                if (pmx.Textures.Count > mmdMat.TextureIndex && mmdMat.TextureIndex >= 0)
                {
                    string relativePath = pmx.Textures[mmdMat.TextureIndex].TexturePath.Replace("//", "\\").Replace('/', '\\');
                    string texPath = Path.GetFullPath(relativePath, storageFolder);

                    mat.textures["_Albedo"] = texPath;
                }
                else if (i > 0)
                {
                    var prevMat = Materials[i - 1];
                    mat.textures["_Albedo"] = prevMat.textures["_Albedo"];
                }

                Materials.Add(mat);
            }

            var rigidBodys = pmx.RigidBodies;
            for (int i = 0; i < rigidBodys.Count; i++)
            {
                var rigidBodyData = rigidBodys[i];
                var rigidBodyDesc = GetRigidBodyDesc(rigidBodyData);

                rigidBodyDescs.Add(rigidBodyDesc);
            }
            var joints = pmx.Joints;
            for (int i = 0; i < joints.Count; i++)
                jointDescs.Add(GetJointDesc(joints[i]));

            {
                //Vector3[] tangent = new Vector3[vertexCount];
                Vector3[] bitangent = new Vector3[vertexCount];
                for (int i = 0; i < vertexCount; i++)
                {
                    tangent[i] = new Vector4(0.0F, 0.0F, 0.0F, 0.0F);
                    bitangent[i] = new Vector3(0.0F, 0.0F, 0.0F);
                }
                // Calculate tangent and bitangent for each triangle and add to all three vertices.
                for (int k = 0; k < indices.Length; k += 3)
                {
                    int i0 = indices[k];
                    int i1 = indices[k + 1];
                    int i2 = indices[k + 2];
                    Vector3 p0 = position[i0];
                    Vector3 p1 = position[i1];
                    Vector3 p2 = position[i2];
                    Vector2 w0 = uv[i0];
                    Vector2 w1 = uv[i1];
                    Vector2 w2 = uv[i2];
                    Vector3 e1 = p1 - p0;
                    Vector3 e2 = p2 - p0;
                    float x1 = w1.X - w0.X, x2 = w2.X - w0.X;
                    float y1 = w1.Y - w0.Y, y2 = w2.Y - w0.Y;
                    float r = 1.0F / (x1 * y2 - x2 * y1);
                    Vector3 t = (e1 * y2 - e2 * y1) * r;
                    Vector3 b = (e2 * x1 - e1 * x2) * r;
                    tangent[i0] += new Vector4(t, 0);
                    tangent[i1] += new Vector4(t, 0);
                    tangent[i2] += new Vector4(t, 0);
                    bitangent[i0] += b;
                    bitangent[i1] += b;
                    bitangent[i2] += b;
                }
                //// Orthonormalize each tangent and calculate the handedness.
                //for (int i = 0; i < vertexCount; i++)
                //{
                //    Vector3 t = tangent[i];
                //    Vector3 b = bitangent[i];
                //    Vector3 n = normalArray[i];
                //    tangentArray[i].xyz() = Vector3.Normalize(Reject(t, n));
                //    tangentArray[i].w = (Vector3.Dot(Vector3.Cross(t, b), n) > 0.0F) ? 1.0F : -1.0F;
                //}
                for (int i = 0; i < vertexCount; i++)
                {
                    float factor = 1.0f;
                    Vector3 t1 = Vector3.Cross(bitangent[i], normal[i]);
                    if (Vector3.Dot(t1, new Vector3(tangent[i].X, tangent[i].Y, tangent[i].Z)) > 0)
                        factor = -1;
                    else
                        factor = 1;
                    tangent[i] = new Vector4(Vector3.Normalize(t1) * factor, factor);
                }
            }
        }

        public Mesh GetMesh()
        {
            if (meshInstance == null)
            {
                meshInstance = new Mesh();
                meshInstance.ReloadIndex<int>(vertexCount, indices);
                meshInstance.AddBuffer<Vector3>(position, 0);
                meshInstance.AddBuffer<Vector3>(normal, 1);
                meshInstance.AddBuffer<Vector2>(uv, 2);
                meshInstance.AddBuffer<ushort>(boneId, 3);
                meshInstance.AddBuffer<float>(boneWeights, 4);
                meshInstance.AddBuffer<Vector4>(tangent, 5);
            }
            return meshInstance;
        }
    }
}
