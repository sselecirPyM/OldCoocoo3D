﻿using Coocoo3D.Components;
using Coocoo3D.Present;
using Coocoo3D.ResourceWarp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.FileFormat
{
    public static class PMXFormatExtension
    {
        public static void LoadPmx(this GameObject gameObject, ModelPack modelPack)
        {
            gameObject.Name = modelPack.name;
            gameObject.Description = modelPack.description;

            var renderer = new MMDRendererComponent();
            gameObject.AddComponent(renderer);
            renderer.skinning = true;
            renderer.morphStateComponent.LoadMorphStates(modelPack);

            renderer.Initialize(modelPack);
            renderer.LoadMesh(modelPack);
            renderer.SetTransform(gameObject.Transform);
        }

        static void Initialize(this MMDRendererComponent renderer, ModelPack modelPack)
        {
            renderer.bones.Clear();
            renderer.boneMatricesData = new Matrix4x4[modelPack.bones.Count];
            renderer.bones.AddRange(modelPack.bones);

            renderer.cachedBoneKeyFrames.Clear();
            for (int i = 0; i < modelPack.bones.Count; i++)
                renderer.cachedBoneKeyFrames.Add(new BoneKeyFrame());

            renderer.BakeSequenceProcessMatrixsIndex();
            renderer.rigidBodyDescs.Clear();
            renderer.rigidBodyDescs.AddRange(modelPack.rigidBodyDescs);
            for (int i = 0; i < modelPack.rigidBodyDescs.Count; i++)
            {
                var rigidBodyDesc = renderer.rigidBodyDescs[i];

                if (rigidBodyDesc.Type != RigidBodyType.Kinematic && rigidBodyDesc.AssociatedBoneIndex != -1)
                    renderer.bones[rigidBodyDesc.AssociatedBoneIndex].IsPhysicsFreeBone = true;
            }
            renderer.jointDescs.Clear();
            renderer.jointDescs.AddRange(modelPack.jointDescs);
        }

        static void LoadMesh(this MMDRendererComponent renderer, ModelPack modelPack)
        {
            renderer.Materials.Clear();
            for (int i = 0; i < modelPack.Materials.Count; i++)
            {
                var mat = modelPack.Materials[i].GetClone();
                renderer.Materials.Add(mat);
            }

            var mesh = modelPack.GetMesh();
            renderer.meshPath = modelPack.fullPath;
            renderer.meshPosData = modelPack.position;
            renderer.meshPosData1 = new Vector3[mesh.GetVertexCount()];
            new Span<Vector3>(renderer.meshPosData).CopyTo(renderer.meshPosData1);
        }

        public static MorphSubMorphDesc Translate(PMX_MorphSubMorphDesc desc)
        {
            return new MorphSubMorphDesc()
            {
                GroupIndex = desc.GroupIndex,
                Rate = desc.Rate,
            };
        }
        public static MorphMaterialDesc Translate(PMX_MorphMaterialDesc desc)
        {
            return new MorphMaterialDesc()
            {
                Ambient = desc.Ambient,
                Diffuse = desc.Diffuse,
                EdgeColor = desc.EdgeColor,
                EdgeSize = desc.EdgeSize,
                MaterialIndex = desc.MaterialIndex,
                MorphMethon = (MorphMaterialMethon)desc.MorphMethon,
                Specular = desc.Specular,
                SubTexture = desc.SubTexture,
                Texture = desc.Texture,
                ToonTexture = desc.ToonTexture,
            };
        }
        public static MorphVertexDesc Translate(PMX_MorphVertexDesc desc)
        {
            return new MorphVertexDesc()
            {
                Offset = desc.Offset,
                VertexIndex = desc.VertexIndex,
            };
        }
        public static MorphUVDesc Translate(PMX_MorphUVDesc desc)
        {
            return new MorphUVDesc()
            {
                Offset = desc.Offset,
                VertexIndex = desc.VertexIndex,
            };
        }
        public static MorphBoneDesc Translate(PMX_MorphBoneDesc desc)
        {
            return new MorphBoneDesc()
            {
                BoneIndex = desc.BoneIndex,
                Rotation = desc.Rotation,
                Translation = desc.Translation,
            };
        }

        public static MorphDesc Translate(PMX_Morph desc)
        {
            MorphSubMorphDesc[] subMorphDescs = null;
            if (desc.SubMorphs != null)
            {
                subMorphDescs = new MorphSubMorphDesc[desc.SubMorphs.Length];
                for (int i = 0; i < desc.SubMorphs.Length; i++)
                    subMorphDescs[i] = Translate(desc.SubMorphs[i]);
            }
            MorphMaterialDesc[] morphMaterialDescs = null;
            if (desc.MorphMaterials != null)
            {
                morphMaterialDescs = new MorphMaterialDesc[desc.MorphMaterials.Length];
                for (int i = 0; i < desc.MorphMaterials.Length; i++)
                    morphMaterialDescs[i] = Translate(desc.MorphMaterials[i]);
            }
            MorphVertexDesc[] morphVertexDescs = null;
            if (desc.MorphVertexs != null)
            {
                morphVertexDescs = new MorphVertexDesc[desc.MorphVertexs.Length];
                for (int i = 0; i < desc.MorphVertexs.Length; i++)
                    morphVertexDescs[i] = Translate(desc.MorphVertexs[i]);
            }
            MorphUVDesc[] morphUVDescs = null;
            if (desc.MorphUVs != null)
            {
                morphUVDescs = new MorphUVDesc[desc.MorphUVs.Length];
                for (int i = 0; i < desc.MorphUVs.Length; i++)
                    morphUVDescs[i] = Translate(desc.MorphUVs[i]);
            }
            MorphBoneDesc[] morphBoneDescs = null;
            if (desc.MorphBones != null)
            {
                morphBoneDescs = new MorphBoneDesc[desc.MorphBones.Length];
                for (int i = 0; i < desc.MorphBones.Length; i++)
                    morphBoneDescs[i] = Translate(desc.MorphBones[i]);
            }

            return new MorphDesc()
            {
                Name = desc.Name,
                NameEN = desc.NameEN,
                Category = (MorphCategory)desc.Category,
                Type = (MorphType)desc.Type,
                MorphBones = morphBoneDescs,
                MorphMaterials = morphMaterialDescs,
                MorphUVs = morphUVDescs,
                MorphVertexs = morphVertexDescs,
                SubMorphs = subMorphDescs,
            };
        }

        public static BoneEntity Translate(PMX_Bone _bone, int index, int boneCount)
        {
            BoneEntity boneEntity = new();
            boneEntity.ParentIndex = (_bone.ParentIndex >= 0 && _bone.ParentIndex < boneCount) ? _bone.ParentIndex : -1;
            boneEntity.staticPosition = _bone.Position;
            boneEntity.rotation = Quaternion.Identity;
            boneEntity.index = index;

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
            if (_bone.AppendBoneIndex >= 0 && _bone.AppendBoneIndex < boneCount)
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
            return boneEntity;
        }
        static void LoadMorphStates(this MMDMorphStateComponent component, ModelPack modelPack)
        {
            int morphCount = modelPack.morphs.Count;

            component.Weights.Load(morphCount);
            component.stringToMorphIndex.Clear();
            for (int i = 0; i < morphCount; i++)
                component.stringToMorphIndex[modelPack.morphs[i].Name] = i;
            component.morphs.Clear();
            component.morphs.AddRange(modelPack.morphs);
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
    }
}
