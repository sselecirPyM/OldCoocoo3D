using Coocoo3D.Components;
using Coocoo3D.FileFormat;
using Coocoo3D.Utility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Mesh = Coocoo3DGraphics.Mesh;
using glTFLoader;
using glTFLoader.Schema;
using System.Runtime.InteropServices;
using Coocoo3D.Present;

namespace Coocoo3D.ResourceWarp
{
    public class ModelPack
    {
        public PMXFormat pmx;

        public string fullPath;

        public string name;
        public string description;

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

        public List<RenderMaterial> Materials = new List<RenderMaterial>();

        public List<RigidBodyDesc> rigidBodyDescs;
        public List<JointDesc> jointDescs;

        public List<BoneEntity> bones;
        public List<MorphDesc> morphs;

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
            var dir = Path.GetDirectoryName(fileName);
            var deserializedFile = Interface.LoadModel(fileName);
            byte[][] buffers = new byte[(int)deserializedFile.Buffers?.Length][];
            for (int i = 0; i < deserializedFile.Buffers?.Length; i++)
            {
                var expectedLength = deserializedFile.Buffers[i].ByteLength;

                var bufferBytes = deserializedFile.LoadBinaryBuffer(i, fileName);
                buffers[i] = bufferBytes;
            }
            textures = new List<string>();
            for (int i = 0; i < deserializedFile.Images?.Length; i++)
            {
                var image = deserializedFile.Images[i];
                string name = Path.GetFullPath(image.Uri, dir);
                textures.Add(name);
            }
            string whiteTexture = Path.GetFullPath("Assets/Textures/white.png");
            List<RenderMaterial> _materials = new();
            for (int i = 0; i < deserializedFile.Materials?.Length; i++)
            {
                var material = deserializedFile.Materials[i];
                var renderMaterial = new RenderMaterial()
                {
                    Name = material.Name,
                    textures = new Dictionary<string, string>(),
                };
                if (material.PbrMetallicRoughness.BaseColorTexture != null)
                {
                    int index = material.PbrMetallicRoughness.BaseColorTexture.Index;
                    renderMaterial.textures["_Albedo"] = textures[GLTFGetTexture(deserializedFile, index)];
                }
                else
                {
                    renderMaterial.textures["_Albedo"] = whiteTexture;
                }
                if (material.PbrMetallicRoughness.MetallicRoughnessTexture != null)
                {
                    int index = material.PbrMetallicRoughness.MetallicRoughnessTexture.Index;
                    renderMaterial.textures["_MetallicRoughness"] = textures[GLTFGetTexture(deserializedFile, index)];
                }
                else
                {
                    renderMaterial.textures["_MetallicRoughness"] = whiteTexture;
                }
                if (material.EmissiveTexture != null)
                {
                    int index = material.EmissiveTexture.Index;
                    renderMaterial.textures["_Emissive"] = textures[GLTFGetTexture(deserializedFile, index)];
                    renderMaterial.Parameters["Emissive"] = 1.0f;
                }
                if (material.NormalTexture != null)
                {
                    int index = material.NormalTexture.Index;
                    renderMaterial.textures["_Normal"] = textures[GLTFGetTexture(deserializedFile, index)];
                    renderMaterial.Parameters["UseNormalMap"] = true;
                }

                renderMaterial.Parameters["Metallic"] = material.PbrMetallicRoughness.MetallicFactor;
                renderMaterial.Parameters["Roughness"] = material.PbrMetallicRoughness.RoughnessFactor;

                _materials.Add(renderMaterial);
            }
            Span<T> GetBuffer<T>(int accessorIndex) where T : struct
            {
                var accessor = deserializedFile.Accessors[accessorIndex];
                var bufferView = deserializedFile.BufferViews[(int)accessor.BufferView];
                var buffer = buffers[bufferView.Buffer];
                return MemoryMarshal.Cast<byte, T>(new Span<byte>(buffer, bufferView.ByteOffset, bufferView.ByteLength));
            }

            Accessor.ComponentTypeEnum GetAccessorComponentType(int accessorIndex)
            {
                var accessor = deserializedFile.Accessors[accessorIndex];
                return accessor.ComponentType;
            }

            Vector3 scale = Vector3.One;
            if (deserializedFile.Nodes.Length == 1)
            {
                var scale1 = deserializedFile.Nodes[0].Scale;
                scale = new Vector3(scale1[0], scale1[1], scale1[2]);
            }

            int vertexCount = 0;
            int indexCount = 0;
            for (int i = 0; i < deserializedFile.Meshes?.Length; i++)
            {
                var mesh = deserializedFile.Meshes[i];
                for (int j = 0; j < mesh.Primitives.Length; j++)
                {
                    var primitive = mesh.Primitives[j];
                    primitive.Attributes.TryGetValue("POSITION", out int pos1);

                    var position = GetBuffer<Vector3>(pos1);
                    vertexCount += position.Length;
                    var format = GetAccessorComponentType(primitive.Indices.Value);
                    if (format == Accessor.ComponentTypeEnum.UNSIGNED_SHORT)
                    {
                        var indices = GetBuffer<ushort>(primitive.Indices.Value);
                        indexCount += indices.Length;
                    }
                    else if (format == Accessor.ComponentTypeEnum.UNSIGNED_INT)
                    {
                        var indices = GetBuffer<uint>(primitive.Indices.Value);
                        indexCount += indices.Length;
                    }
                }
            }
            indices = new int[indexCount];
            this.vertexCount = vertexCount;
            position = new Vector3[vertexCount];
            normal = new Vector3[vertexCount];
            uv = new Vector2[vertexCount];
            tangent = new Vector4[vertexCount];
            var positionWriter = new SpanWriter<Vector3>(position);
            var normalWriter = new SpanWriter<Vector3>(normal);
            var uvWriter = new SpanWriter<Vector2>(uv);
            var indicesWriter = new SpanWriter<int>(indices);
            var tangentWriter = new SpanWriter<Vector4>(tangent);
            for (int i = 0; i < deserializedFile.Meshes?.Length; i++)
            {
                var mesh = deserializedFile.Meshes[i];
                for (int j = 0; j < mesh.Primitives.Length; j++)
                {
                    var primitive = mesh.Primitives[j];

                    var material = _materials[primitive.Material.Value].GetClone();
                    material.Name = Materials.Count.ToString();
                    int vertexStart = positionWriter.currentPosition;
                    material.vertexStart = vertexStart;
                    //material.vertexStart = 0;
                    material.indexOffset = indicesWriter.currentPosition;
                    Materials.Add(material);

                    primitive.Attributes.TryGetValue("POSITION", out int pos1);
                    var bufferPos1 = GetBuffer<Vector3>(pos1);
                    positionWriter.Write(bufferPos1);
                    primitive.Attributes.TryGetValue("NORMAL", out int norm1);
                    normalWriter.Write(GetBuffer<Vector3>(norm1));

                    primitive.Attributes.TryGetValue("TEXCOORD_0", out int uv1);
                    uvWriter.Write(GetBuffer<Vector2>(uv1));

                    material.vertexCount = bufferPos1.Length;
                    //material.vertexCount = vertexCount;
                    var format = GetAccessorComponentType(primitive.Indices.Value);
                    if (format == Accessor.ComponentTypeEnum.UNSIGNED_SHORT)
                    {
                        var indices = GetBuffer<ushort>(primitive.Indices.Value);
                        for (int k = 0; k < indices.Length; k++)
                            indicesWriter.Write(indices[k]);
                        material.indexCount = indices.Length;
                    }
                    else if (format == Accessor.ComponentTypeEnum.UNSIGNED_INT)
                    {
                        var indices = GetBuffer<uint>(primitive.Indices.Value);
                        for (int k = 0; k < indices.Length; k++)
                            indicesWriter.Write((int)indices[k]);
                        material.indexCount = indices.Length;
                    }

                    for (int k = material.indexOffset; k < material.indexOffset + material.indexCount; k += 3)
                    {
                        (indices[k], indices[k + 1], indices[k + 2]) = (indices[k + 2], indices[k + 1], indices[k]);
                    }

                    for (int k = vertexStart; k < vertexStart + bufferPos1.Length; k++)
                    {
                        position[k] *= scale;
                    }

                    if (primitive.Attributes.TryGetValue("TANGENT", out int tan1))
                    {
                        tangentWriter.Write(GetBuffer<Vector4>(tan1));
                    }
                    else
                    {
                        ComputeTangent(vertexStart, bufferPos1.Length, material.indexOffset, material.indexCount);
                    }
                    //for (int k = material.indexOffset; k < material.indexOffset + material.indexCount; k++)
                    //{
                    //    indices[k] += vertexStart;
                    //}
                }
            }

            //for (int k = 0; k < vertexCount; k++)
            //{
            //    position[k] *= scale;
            //}

        }

        static int GLTFGetTexture(Gltf gltf, int index)
        {
            return (int)gltf.Textures[index].Source;
        }

        public void LoadPMX(string fileName)
        {
            BinaryReader reader = new BinaryReader(new FileInfo(fileName).OpenRead());
            string folder = Path.GetDirectoryName(fileName);

            pmx = new PMXFormat();
            pmx.Reload(reader);
            reader.Dispose();
            name = string.Format("{0} {1}", pmx.Name, pmx.NameEN);
            description = string.Format("{0}\n{1}", pmx.Description, pmx.DescriptionEN);
            textures = new List<string>();
            foreach (var tex in pmx.Textures)
            {
                string relativePath = tex.TexturePath.Replace("//", "\\").Replace('/', '\\');
                string texPath = Path.GetFullPath(relativePath, folder);
                textures.Add(texPath);
            }
            skinning = true;
            vertexCount = pmx.Vertices.Length;
            position = new Vector3[vertexCount];
            normal = new Vector3[vertexCount];
            uv = new Vector2[vertexCount];
            tangent = new Vector4[vertexCount];
            boneId = new ushort[vertexCount * 4];
            boneWeights = new float[vertexCount * 4];
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

            string whiteTexture = Path.GetFullPath("Assets/Textures/white.png");
            int indexOffset = 0;
            for (int i = 0; i < pmx.Materials.Count; i++)
            {
                var mmdMat = pmx.Materials[i];

                RenderMaterial material = new RenderMaterial
                {
                    Name = mmdMat.Name,
                    indexCount = mmdMat.TriangeIndexNum,
                    indexOffset = indexOffset,
                    vertexCount = vertexCount
                };
                Vector3 min;
                Vector3 max;
                min = position[pmx.TriangleIndexs[indexOffset]];
                max = position[pmx.TriangleIndexs[indexOffset]];
                for (int k = 0; k < material.indexCount; k++)
                {
                    min = Vector3.Min(min, position[pmx.TriangleIndexs[indexOffset + k]]);
                    max = Vector3.Max(max, position[pmx.TriangleIndexs[indexOffset + k]]);
                }

                material.boundingBox = new Vortice.Mathematics.BoundingBox(min, max);
                material.DrawDoubleFace = mmdMat.DrawFlags.HasFlag(PMX_DrawFlag.DrawDoubleFace);
                material.Parameters["DiffuseColor"] = mmdMat.DiffuseColor;
                material.Parameters["SpecularColor"] = mmdMat.SpecularColor;
                material.Parameters["EdgeSize"] = mmdMat.EdgeScale;
                material.Parameters["AmbientColor"] = mmdMat.AmbientColor;
                material.Parameters["EdgeColor"] = mmdMat.EdgeColor;
                material.Parameters["CastShadow"] = mmdMat.DrawFlags.HasFlag(PMX_DrawFlag.CastSelfShadow);
                material.Parameters["ReceiveShadow"] = mmdMat.DrawFlags.HasFlag(PMX_DrawFlag.DrawSelfShadow);
                indexOffset += mmdMat.TriangeIndexNum;
                if (pmx.Textures.Count > mmdMat.TextureIndex && mmdMat.TextureIndex >= 0)
                {
                    string relativePath = pmx.Textures[mmdMat.TextureIndex].TexturePath.Replace("//", "\\").Replace('/', '\\');
                    string texPath = Path.GetFullPath(relativePath, folder);

                    material.textures["_Albedo"] = texPath;
                }
                else if (i > 0)
                {
                    var prevMat = Materials[i - 1];
                    material.textures["_Albedo"] = prevMat.textures["_Albedo"];
                }
                material.textures["_MetallicRoughness"] = whiteTexture;

                Materials.Add(material);
            }

            var rigidBodys = pmx.RigidBodies;
            rigidBodyDescs = new List<RigidBodyDesc>();
            for (int i = 0; i < rigidBodys.Count; i++)
            {
                var rigidBodyData = rigidBodys[i];
                var rigidBodyDesc = GetRigidBodyDesc(rigidBodyData);

                rigidBodyDescs.Add(rigidBodyDesc);
            }
            var joints = pmx.Joints;
            jointDescs = new List<JointDesc>();
            for (int i = 0; i < joints.Count; i++)
                jointDescs.Add(GetJointDesc(joints[i]));

            ComputeTangent(0, vertexCount, 0, indices.Length);

            {
                bones = new List<BoneEntity>();
                var _bones = pmx.Bones;
                for (int i = 0; i < _bones.Count; i++)
                {
                    var _bone = _bones[i];
                    BoneEntity boneEntity = new();
                    boneEntity.ParentIndex = (_bone.ParentIndex >= 0 && _bone.ParentIndex < _bones.Count) ? _bone.ParentIndex : -1;
                    boneEntity.staticPosition = _bone.Position;
                    boneEntity.rotation = Quaternion.Identity;
                    boneEntity.index = i;

                    boneEntity.Name = _bone.Name;
                    boneEntity.NameEN = _bone.NameEN;
                    boneEntity.Flags = (BoneFlags)_bone.Flags;

                    if (boneEntity.Flags.HasFlag(BoneFlags.HasIK))
                    {
                        boneEntity.IKTargetIndex = _bone.boneIK.IKTargetIndex;
                        boneEntity.CCDIterateLimit = _bone.boneIK.CCDIterateLimit;
                        boneEntity.CCDAngleLimit = _bone.boneIK.CCDAngleLimit;
                        boneEntity.boneIKLinks = new BoneEntity.IKLink[_bone.boneIK.IKLinks.Length];
                        for (int j = 0; j < boneEntity.boneIKLinks.Length; j++)
                        {
                            boneEntity.boneIKLinks[j] = IKLink(_bone.boneIK.IKLinks[j]);
                        }
                    }
                    if (_bone.AppendBoneIndex >= 0 && _bone.AppendBoneIndex < _bones.Count)
                    {
                        boneEntity.AppendParentIndex = _bone.AppendBoneIndex;
                        boneEntity.AppendRatio = _bone.AppendBoneRatio;
                        boneEntity.IsAppendRotation = boneEntity.Flags.HasFlag(BoneFlags.AcquireRotate);
                        boneEntity.IsAppendTranslation = boneEntity.Flags.HasFlag(BoneFlags.AcquireTranslate);
                    }
                    else
                    {
                        boneEntity.AppendParentIndex = -1;
                        boneEntity.AppendRatio = 0;
                        boneEntity.IsAppendRotation = false;
                        boneEntity.IsAppendTranslation = false;
                    }
                    bones.Add(boneEntity);
                }
                morphs = new();
                for (int i = 0; i < pmx.Morphs.Count; i++)
                {
                    morphs.Add(PMXFormatExtension.Translate(pmx.Morphs[i]));
                }
            }
        }

        void ComputeTangent(int vertexStart, int vertexCount, int indexStart, int indexCount)
        {
            int vertexEnd = vertexStart + vertexCount;
            int indexEnd = indexStart + indexCount;

            Vector3[] bitangent = new Vector3[vertexCount];
            for (int i = vertexStart; i < vertexEnd; i++)
            {
                tangent[i] = new Vector4(0.0F, 0.0F, 0.0F, 0.0F);
            }
            for (int i = 0; i < vertexCount; i++)
            {
                bitangent[i] = new Vector3(0.0F, 0.0F, 0.0F);
            }

            // Calculate tangent and bitangent for each triangle and add to all three vertices.
            for (int k = indexStart; k < indexEnd; k += 3)
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
                tangent[i0 + vertexStart] += new Vector4(t, 0);
                tangent[i1 + vertexStart] += new Vector4(t, 0);
                tangent[i2 + vertexStart] += new Vector4(t, 0);
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
            for (int i = vertexStart; i < vertexEnd; i++)
            {
                float factor;
                Vector3 t1 = Vector3.Cross(bitangent[i - vertexStart], normal[i]);
                if (Vector3.Dot(t1, new Vector3(tangent[i].X, tangent[i].Y, tangent[i].Z)) > 0)
                    factor = 1;
                else
                    factor = -1;
                tangent[i] = new Vector4(Vector3.Normalize(t1) * factor, 1);
            }
        }

        static BoneEntity.IKLink IKLink(in PMX_BoneIKLink ikLink1)
        {
            var ikLink = new BoneEntity.IKLink();

            ikLink.HasLimit = ikLink1.HasLimit;
            ikLink.LimitMax = ikLink1.LimitMax;
            ikLink.LimitMin = ikLink1.LimitMin;
            ikLink.LinkedIndex = ikLink1.LinkedIndex;

            Vector3 tempMin = ikLink.LimitMin;
            Vector3 tempMax = ikLink.LimitMax;
            ikLink.LimitMin = Vector3.Min(tempMin, tempMax);
            ikLink.LimitMax = Vector3.Max(tempMin, tempMax);

            if (ikLink.LimitMin.X > -Math.PI * 0.5 && ikLink.LimitMax.X < Math.PI * 0.5)
                ikLink.TransformOrder = IKTransformOrder.Zxy;
            else if (ikLink.LimitMin.Y > -Math.PI * 0.5 && ikLink.LimitMax.Y < Math.PI * 0.5)
                ikLink.TransformOrder = IKTransformOrder.Xyz;
            else
                ikLink.TransformOrder = IKTransformOrder.Yzx;

            const float epsilon = 1e-6f;
            if (ikLink.HasLimit)
            {
                if (Math.Abs(ikLink.LimitMin.X) < epsilon &&
                    Math.Abs(ikLink.LimitMax.X) < epsilon &&
                    Math.Abs(ikLink.LimitMin.Y) < epsilon &&
                    Math.Abs(ikLink.LimitMax.Y) < epsilon &&
                    Math.Abs(ikLink.LimitMin.Z) < epsilon &&
                    Math.Abs(ikLink.LimitMax.Z) < epsilon)
                {
                    ikLink.FixTypes = AxisFixType.FixAll;
                }
                else if (Math.Abs(ikLink.LimitMin.Y) < epsilon &&
                         Math.Abs(ikLink.LimitMax.Y) < epsilon &&
                         Math.Abs(ikLink.LimitMin.Z) < epsilon &&
                         Math.Abs(ikLink.LimitMax.Z) < epsilon)
                {
                    ikLink.FixTypes = AxisFixType.FixX;
                }
                else if (Math.Abs(ikLink.LimitMin.X) < epsilon &&
                         Math.Abs(ikLink.LimitMax.X) < epsilon &&
                         Math.Abs(ikLink.LimitMin.Z) < epsilon &&
                         Math.Abs(ikLink.LimitMax.Z) < epsilon)
                {
                    ikLink.FixTypes = AxisFixType.FixY;
                }
                else if (Math.Abs(ikLink.LimitMin.X) < epsilon &&
                         Math.Abs(ikLink.LimitMax.X) < epsilon &&
                         Math.Abs(ikLink.LimitMin.Y) < epsilon &&
                         Math.Abs(ikLink.LimitMax.Y) < epsilon)
                {
                    ikLink.FixTypes = AxisFixType.FixZ;
                }
            }
            return ikLink;
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
                meshInstance.AddBuffer<Vector4>(tangent, 3);
                if (boneId != null)
                    meshInstance.AddBuffer<ushort>(boneId, 4);
                if (boneWeights != null)
                    meshInstance.AddBuffer<float>(boneWeights, 5);
            }
            return meshInstance;
        }

        public void LoadMeshComponent(GameObject gameObject)
        {
            var meshRenderer = new MeshRendererComponent();
            gameObject.AddComponent(meshRenderer);
            meshRenderer.meshPath = fullPath;
            meshRenderer.transform = gameObject.Transform;
            foreach (var material in Materials)
                meshRenderer.Materials.Add(material.GetClone());
        }
    }
}
