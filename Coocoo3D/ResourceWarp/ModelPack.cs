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

namespace Coocoo3D.ResourceWarp
{
    public class ModelPack
    {
        public PMXFormat pmx = new PMXFormat();

        public string fullPath;

        public Vector3[] position;
        public Vector3[] normal;
        public Vector2[] uv;
        public ushort[] boneId;
        public float[] boneWeights;
        public Vector3[] tangent;
        MMDMesh meshInstance;
        public int vertexCount;

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

        public void Reload(BinaryReader reader, string storageFolder)
        {
            pmx.Reload(reader);
            vertexCount = pmx.Vertices.Length;
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

            int indexOffset = 0;
            for (int i = 0; i < pmx.Materials.Count; i++)
            {
                var mmdMat = pmx.Materials[i];

                RuntimeMaterial mat = new RuntimeMaterial
                {
                    Name = mmdMat.Name,
                    indexCount = mmdMat.TriangeIndexNum,
                    indexOffset = indexOffset,
                    innerStruct =
                    {
                        DiffuseColor = mmdMat.DiffuseColor,
                        SpecularColor = mmdMat.SpecularColor,
                        EdgeSize = mmdMat.EdgeScale,
                        EdgeColor = mmdMat.EdgeColor,
                        AmbientColor = new Vector3(MathF.Pow(mmdMat.AmbientColor.X, 2.2f), MathF.Pow(mmdMat.AmbientColor.Y, 2.2f), MathF.Pow(mmdMat.AmbientColor.Z, 2.2f)),
                        Roughness = 0.8f,
                        Specular = 0.5f,
                    },
                    DrawFlags = (DrawFlag)mmdMat.DrawFlags,
                    skinning = true,
                };
                indexOffset += mmdMat.TriangeIndexNum;
                if (pmx.Textures.Count > mmdMat.TextureIndex && mmdMat.TextureIndex >= 0)
                {
                    string relativePath = pmx.Textures[mmdMat.TextureIndex].TexturePath.Replace("//", "\\").Replace('/', '\\');
                    string texPath = Path.GetFullPath(relativePath, storageFolder);

                    mat.textures["_Albedo"] = texPath;
                }
                else
                {
                    if (i > 0)
                    {
                        var prevMat = Materials[i - 1];
                        mat.textures["_Albedo"] = prevMat.textures["_Albedo"];
                    }
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
