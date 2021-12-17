using Coocoo3D.Components;
using Coocoo3D.MMDSupport;
using Coocoo3D.Present;
using Coocoo3D.ResourceWarp;
using Coocoo3D.Numerics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Components
{
    public class MMDRendererComponent : Component
    {
        public string meshPath;
        public string motionPath = "";
        public Vector3 position;
        public Quaternion rotation;
        public MMDMorphStateComponent morphStateComponent = new MMDMorphStateComponent();
        public bool LockMotion;

        public int meshVertexCount;
        public int meshIndexCount;
        public Vector3[] meshPosData;

        public bool skinning;

        public List<RuntimeMaterial> Materials = new List<RuntimeMaterial>();

        public Vector3[] meshPosData1;
        public bool meshNeedUpdate;

        public void VertexMaterialMorph()
        {
            ComputeVertexMorph();
        }

        public void ComputeVertexMorph()
        {
            for (int i = 0; i < morphStateComponent.morphs.Count; i++)
            {
                if (morphStateComponent.morphs[i].Type == MorphType.Vertex)
                {
                    if (morphStateComponent.Weights.ComputedWeightNotEqualsPrev(i))
                        meshNeedUpdate = true;
                }
            }

            new Span<Vector3>(meshPosData).CopyTo(meshPosData1);

            for (int i = 0; i < morphStateComponent.morphs.Count; i++)
            {
                if (morphStateComponent.morphs[i].Type == MorphType.Vertex)
                {
                    MorphVertexDesc[] morphVertices = morphStateComponent.morphs[i].MorphVertexs;

                    float computedWeight = morphStateComponent.Weights.Computed[i];
                    if (computedWeight != 0)
                        for (int j = 0; j < morphVertices.Length; j++)
                        {
                            meshPosData1[morphVertices[j].VertexIndex] += morphVertices[j].Offset * computedWeight;
                        }
                }
            }
        }


        #region bone

        public Matrix4x4[] boneMatricesData;

        public List<BoneEntity> bones = new List<BoneEntity>();
        public List<BoneKeyFrame> cachedBoneKeyFrames = new List<BoneKeyFrame>();

        public List<RigidBodyDesc> rigidBodyDescs = new List<RigidBodyDesc>();
        public List<JointDesc> jointDescs = new List<JointDesc>();


        public Matrix4x4 LocalToWorld = Matrix4x4.Identity;
        public Matrix4x4 WorldToLocal = Matrix4x4.Identity;

        public Dictionary<int, List<List<int>>> IKNeedUpdateIndexs;
        public List<int> AppendNeedUpdateMatIndexs = new List<int>();
        public List<int> PhysicsNeedUpdateMatrixIndexs = new List<int>();
        public void WriteMatriticesData()
        {
            for (int i = 0; i < bones.Count; i++)
                boneMatricesData[i] = Matrix4x4.Transpose(bones[i].GeneratedTransform);
            //boneMatricesData[i] = bones[i].GeneratedTransform;
        }
        public void SetPoseWithMotion(float time, MMDMotion motion)
        {
            LocalToWorld = Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
            Matrix4x4.Invert(LocalToWorld, out WorldToLocal);
            lock (motion)
            {
                morphStateComponent.SetPose(motion, time);
                morphStateComponent.ComputeWeight();
                foreach (var bone in bones)
                {
                    var keyframe = motion.GetBoneMotion(bone.Name, time);
                    bone.rotation = keyframe.rotation;
                    bone.dynamicPosition = keyframe.translation;
                    cachedBoneKeyFrames[bone.index] = keyframe;
                }
            }
        }
        public void SetPoseDefault()
        {
            LocalToWorld = Matrix4x4.CreateFromQuaternion(rotation) * Matrix4x4.CreateTranslation(position);
            Matrix4x4.Invert(LocalToWorld, out WorldToLocal);
            morphStateComponent.SetPoseDefault();
            morphStateComponent.ComputeWeight();
            foreach (var bone in bones)
            {
                var keyframe = new BoneKeyFrame() { Rotation = Quaternion.Identity };
                bone.rotation = keyframe.rotation;
                bone.dynamicPosition = keyframe.translation;
                cachedBoneKeyFrames[bone.index] = keyframe;
            }
        }
        public void BoneMorphIKAppend()
        {
            for (int i = 0; i < morphStateComponent.morphs.Count; i++)
            {
                if (morphStateComponent.morphs[i].Type == MorphType.Bone)
                {
                    MorphBoneDesc[] morphBoneStructs = morphStateComponent.morphs[i].MorphBones;
                    float computedWeight = morphStateComponent.Weights.Computed[i];
                    for (int j = 0; j < morphBoneStructs.Length; j++)
                    {
                        var morphBoneStruct = morphBoneStructs[j];
                        bones[morphBoneStruct.BoneIndex].rotation *= Quaternion.Slerp(Quaternion.Identity, morphBoneStruct.Rotation, computedWeight);
                        bones[morphBoneStruct.BoneIndex].dynamicPosition += morphBoneStruct.Translation * computedWeight;
                    }
                }
            }

            for (int i = 0; i < bones.Count; i++)
            {
                IK(i, bones);
            }
            UpdateAppendBones();
        }

        public void SetPoseNoMotion()
        {
            morphStateComponent.ComputeWeight();
            for (int i = 0; i < bones.Count; i++)
            {
                var keyframe = cachedBoneKeyFrames[i];
                bones[i].rotation = keyframe.rotation;
                bones[i].dynamicPosition = keyframe.translation;
            }
        }

        public void UpdateAppendBones()
        {
            for (int i = 0; i < bones.Count; i++)
            {
                var bone = bones[i];
                if (bone.IsAppendTranslation || bone.IsAppendRotation)
                {
                    var mat1 = bones[bone.AppendParentIndex].GeneratedTransform;
                    Matrix4x4.Decompose(mat1, out _, out var rotation, out var translation);
                    if (bone.IsAppendTranslation)
                    {
                        bone.appendTranslation = translation * bone.AppendRatio;
                    }
                    if (bone.IsAppendRotation)
                    {
                        bone.appendRotation = Quaternion.Slerp(Quaternion.Identity, bones[bone.AppendParentIndex].rotation, bone.AppendRatio);
                    }
                }
            }
            UpdateMatrices(AppendNeedUpdateMatIndexs);
        }

        void IK(int boneIndex, List<BoneEntity> bones)
        {
            int ikTargetIndex = bones[boneIndex].IKTargetIndex;
            if (ikTargetIndex == -1) return;
            var entity = bones[boneIndex];
            var entitySource = bones[ikTargetIndex];

            entity.GetPosRot2(out var posTarget, out var rot0);


            int h1 = entity.CCDIterateLimit / 2;
            Vector3 posSource = entitySource.GetPos2();
            if ((posTarget - posSource).LengthSquared() < 1e-8f) return;
            for (int i = 0; i < entity.CCDIterateLimit; i++)
            {
                bool axis_lim = i < h1;
                for (int j = 0; j < entity.boneIKLinks.Length; j++)
                {
                    posSource = entitySource.GetPos2();
                    ref var IKLINK = ref entity.boneIKLinks[j];
                    BoneEntity itEntity = bones[IKLINK.LinkedIndex];

                    itEntity.GetPosRot2(out var itPosition, out var itRot);

                    Vector3 targetDirection = Vector3.Normalize(itPosition - posTarget);
                    Vector3 ikDirection = Vector3.Normalize(itPosition - posSource);
                    float dotV = Math.Clamp(Vector3.Dot(targetDirection, ikDirection), -1, 1);

                    Matrix4x4 matXi = Matrix4x4.Transpose(itEntity.GeneratedTransform);
                    Vector3 ikRotateAxis = SafeNormalize(Vector3.TransformNormal(Vector3.Cross(targetDirection, ikDirection), matXi));

                    if (axis_lim)
                        switch (IKLINK.FixTypes)
                        {
                            case AxisFixType.FixX:
                                ikRotateAxis.X = ikRotateAxis.X >= 0 ? 1 : -1;
                                ikRotateAxis.Y = 0;
                                ikRotateAxis.Z = 0;
                                break;
                            case AxisFixType.FixY:
                                ikRotateAxis.X = 0;
                                ikRotateAxis.Y = ikRotateAxis.Y >= 0 ? 1 : -1;
                                ikRotateAxis.Z = 0;
                                break;
                            case AxisFixType.FixZ:
                                ikRotateAxis.X = 0;
                                ikRotateAxis.Y = 0;
                                ikRotateAxis.Z = ikRotateAxis.Z >= 0 ? 1 : -1;
                                break;
                        }
                    //重命名函数以缩短函数名
                    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
                    Quaternion QAxisAngle(Vector3 axis, float angle) => Quaternion.CreateFromAxisAngle(axis, angle);

                    itEntity.rotation = Quaternion.Normalize(itEntity.rotation * QAxisAngle(ikRotateAxis, -Math.Min((float)Math.Acos(dotV), entity.CCDAngleLimit * (i + 1))));

                    if (IKLINK.HasLimit)
                    {
                        Vector3 angle = Vector3.Zero;
                        switch (IKLINK.TransformOrder)
                        {
                            case IKTransformOrder.Zxy:
                                {
                                    angle = MathHelper.QuaternionToZxy(itEntity.rotation);
                                    Vector3 cachedE = angle;
                                    angle = LimitAngle(angle, axis_lim, IKLINK.LimitMin, IKLINK.LimitMax);
                                    if (cachedE != angle)
                                        itEntity.rotation = Quaternion.Normalize(QAxisAngle(Vector3.UnitZ, angle.Z) * QAxisAngle(Vector3.UnitX, angle.X) * QAxisAngle(Vector3.UnitY, angle.Y));
                                    break;
                                }
                            case IKTransformOrder.Xyz:
                                {
                                    angle = MathHelper.QuaternionToXyz(itEntity.rotation);
                                    Vector3 cachedE = angle;
                                    angle = LimitAngle(angle, axis_lim, IKLINK.LimitMin, IKLINK.LimitMax);
                                    if (cachedE != angle)
                                        itEntity.rotation = Quaternion.Normalize(QAxisAngle(Vector3.UnitX, angle.X) * QAxisAngle(Vector3.UnitY, angle.Y) * QAxisAngle(Vector3.UnitZ, angle.Z));
                                    break;
                                }
                            case IKTransformOrder.Yzx:
                                {
                                    angle = MathHelper.QuaternionToYzx(itEntity.rotation);
                                    Vector3 cachedE = angle;
                                    angle = LimitAngle(angle, axis_lim, IKLINK.LimitMin, IKLINK.LimitMax);
                                    if (cachedE != angle)
                                        itEntity.rotation = Quaternion.Normalize(QAxisAngle(Vector3.UnitY, angle.Y) * QAxisAngle(Vector3.UnitZ, angle.Z) * QAxisAngle(Vector3.UnitX, angle.X));
                                    break;
                                }
                            default:
                                throw new NotImplementedException();
                        }
                    }
                    UpdateMatrices(IKNeedUpdateIndexs[boneIndex][j]);
                }
                posSource = entitySource.GetPos2();
                if ((posTarget - posSource).LengthSquared() < 1e-8f) return;
            }
        }

        public void BakeSequenceProcessMatrixsIndex()
        {
            IKNeedUpdateIndexs = new Dictionary<int, List<List<int>>>();
            bool[] bonesTest = new bool[bones.Count];

            for (int i = 0; i < bones.Count; i++)
            {
                int ikTargetIndex = bones[i].IKTargetIndex;
                if (ikTargetIndex != -1)
                {
                    List<List<int>> ax = new List<List<int>>();
                    var entity = bones[i];
                    var entitySource = bones[ikTargetIndex];
                    for (int j = 0; j < entity.boneIKLinks.Length; j++)
                    {
                        List<int> bx = new List<int>();

                        Array.Clear(bonesTest, 0, bones.Count);
                        bonesTest[entity.boneIKLinks[j].LinkedIndex] = true;
                        for (int k = 0; k < bones.Count; k++)
                        {
                            if (bones[k].ParentIndex != -1)
                            {
                                bonesTest[k] |= bonesTest[bones[k].ParentIndex];
                                if (bonesTest[k])
                                    bx.Add(k);
                            }
                        }
                        ax.Add(bx);
                    }
                    IKNeedUpdateIndexs[i] = ax;
                }
            }
            Array.Clear(bonesTest, 0, bones.Count);
            AppendNeedUpdateMatIndexs.Clear();
            for (int i = 0; i < bones.Count; i++)
            {
                var bone = bones[i];
                if (bones[i].ParentIndex != -1)
                    bonesTest[i] |= bonesTest[bones[i].ParentIndex];
                bonesTest[i] |= bone.IsAppendTranslation || bone.IsAppendRotation;
                if (bonesTest[i])
                {
                    AppendNeedUpdateMatIndexs.Add(i);
                }
            }
            Array.Clear(bonesTest, 0, bones.Count);
            PhysicsNeedUpdateMatrixIndexs.Clear();
            for (int i = 0; i < bones.Count; i++)
            {
                var bone = bones[i];
                if (bones[i].ParentIndex == -1)
                    continue;
                var parent = bones[bones[i].ParentIndex];
                bonesTest[i] |= bonesTest[bones[i].ParentIndex];
                bonesTest[i] |= parent.IsPhysicsFreeBone;
                if (bonesTest[i])
                {
                    PhysicsNeedUpdateMatrixIndexs.Add(i);
                }
            }
        }

        public void UpdateAllMatrix()
        {
            for (int i = 0; i < bones.Count; i++)
                bones[i].GetTransformMatrixG(bones);
        }
        public void UpdateMatrices(List<int> indexs)
        {
            for (int i = 0; i < indexs.Count; i++)
                bones[indexs[i]].GetTransformMatrixG(bones);
        }

        #region helper functions

        public static Quaternion ToQuaternion(Vector3 angle)
        {
            return Quaternion.CreateFromYawPitchRoll(angle.Y, angle.X, angle.Z);
        }

        private Vector3 LimitAngle(Vector3 angle, bool axis_lim, Vector3 low, Vector3 high)
        {
            if (!axis_lim)
            {
                return Vector3.Clamp(angle, low, high);
            }
            Vector3 vecL1 = 2.0f * low - angle;
            Vector3 vecH1 = 2.0f * high - angle;
            if (angle.X < low.X)
            {
                angle.X = (vecL1.X <= high.X) ? vecL1.X : low.X;
            }
            else if (angle.X > high.X)
            {
                angle.X = (vecH1.X >= low.X) ? vecH1.X : high.X;
            }
            if (angle.Y < low.Y)
            {
                angle.Y = (vecL1.Y <= high.Y) ? vecL1.Y : low.Y;
            }
            else if (angle.Y > high.Y)
            {
                angle.Y = (vecH1.Y >= low.Y) ? vecH1.Y : high.Y;
            }
            if (angle.Z < low.Z)
            {
                angle.Z = (vecL1.Z <= high.Z) ? vecL1.Z : low.Z;
            }
            else if (angle.Z > high.Z)
            {
                angle.Z = (vecH1.Z >= low.Z) ? vecH1.Z : high.Z;
            }
            return angle;
        }

        Vector3 SafeNormalize(Vector3 vector3)
        {
            float dp3 = Math.Max(0.00001f, Vector3.Dot(vector3, vector3));
            return vector3 / MathF.Sqrt(dp3);
        }
        #endregion

        #endregion
        public void SetMotionTime(float time, MMDMotion motion)
        {
            if (!LockMotion)
            {
                if (motion != null)
                    SetPoseWithMotion(time, motion);
                else
                    SetPoseDefault();
            }
            else
            {
                SetPoseNoMotion();
            }
            UpdateAllMatrix();
            BoneMorphIKAppend();
            VertexMaterialMorph();
        }
    }
    public class RuntimeMaterial
    {
        public string Name;

        public string unionShader;

        public int indexOffset;
        public int indexCount;
        public DrawFlag DrawFlags;
        public bool Transparent;
        public bool Skinning;
        public Dictionary<string, string> textures = new Dictionary<string, string>();
        public Dictionary<string, object> Parameters = new Dictionary<string, object>();

        public Vortice.Mathematics.BoundingBox boundingBox;

        public RuntimeMaterial GetClone()
        {
            var mat = (RuntimeMaterial)MemberwiseClone();
            mat.textures = new Dictionary<string, string>(textures);
            mat.Parameters = new Dictionary<string, object>(Parameters);
            return mat;
        }

        public override string ToString()
        {
            return Name;
        }
    }


    public class BoneEntity
    {
        public int index;
        public Vector3 staticPosition;
        public Vector3 dynamicPosition;
        public Quaternion rotation = Quaternion.Identity;
        public Vector3 appendTranslation;
        public Quaternion appendRotation = Quaternion.Identity;

        public Matrix4x4 _generatedTransform = Matrix4x4.Identity;
        public Matrix4x4 GeneratedTransform { get => _generatedTransform; }

        public int ParentIndex = -1;
        public string Name;
        public string NameEN;

        public int IKTargetIndex = -1;
        public int CCDIterateLimit = 0;
        public float CCDAngleLimit = 0;
        public IKLink[] boneIKLinks;

        public int AppendParentIndex = -1;
        public float AppendRatio;
        public bool IsAppendRotation;
        public bool IsAppendTranslation;
        public bool IsPhysicsFreeBone;
        public BoneFlags Flags;

        public void GetTransformMatrixG(List<BoneEntity> list)
        {
            if (ParentIndex != -1)
            {
                _generatedTransform = Matrix4x4.CreateTranslation(-staticPosition) *
                   Matrix4x4.CreateFromQuaternion(rotation * appendRotation) *
                   Matrix4x4.CreateTranslation(staticPosition + appendTranslation + dynamicPosition) * list[ParentIndex]._generatedTransform;
            }
            else
            {
                _generatedTransform = Matrix4x4.CreateTranslation(-staticPosition) *
                   Matrix4x4.CreateFromQuaternion(rotation * appendRotation) *
                   Matrix4x4.CreateTranslation(staticPosition + appendTranslation + dynamicPosition);
            }
        }
        public Vector3 GetPos2()
        {
            return Vector3.Transform(staticPosition, _generatedTransform);
        }

        public void GetPosRot2(out Vector3 pos, out Quaternion rot)
        {
            pos = Vector3.Transform(staticPosition, _generatedTransform);
            Matrix4x4.Decompose(_generatedTransform, out _, out rot, out _);
        }

        public struct IKLink
        {
            public int LinkedIndex;
            public bool HasLimit;
            public Vector3 LimitMin;
            public Vector3 LimitMax;
            public IKTransformOrder TransformOrder;
            public AxisFixType FixTypes;
        }
        public override string ToString()
        {
            return string.Format("{0}_{1}", Name, NameEN);
        }
    }
}
namespace Coocoo3D.FileFormat
{
    public static partial class PMXFormatExtension
    {
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

        public static void Reload2(this GameObject gameObject, ModelPack modelPack)
        {
            var modelResource = modelPack.pmx;
            gameObject.Name = string.Format("{0} {1}", modelResource.Name, modelResource.NameEN);
            gameObject.Description = string.Format("{0}\n{1}", modelResource.Description, modelResource.DescriptionEN);

            var rendererComponent = new MMDRendererComponent();
            gameObject.AddComponent(rendererComponent);
            rendererComponent.skinning = true;
            rendererComponent.morphStateComponent.Reload(modelResource);

            rendererComponent.Initialize2(modelPack);
            rendererComponent.ReloadModel(modelPack);
        }

        public static void ReloadModel(this MMDRendererComponent rendererComponent, ModelPack modelPack)
        {
            rendererComponent.Materials.Clear();
            for (int i = 0; i < modelPack.Materials.Count; i++)
            {
                var mat = modelPack.Materials[i].GetClone();
                rendererComponent.Materials.Add(mat);
            }

            var mesh = modelPack.GetMesh();
            rendererComponent.meshPath = modelPack.fullPath;
            rendererComponent.meshPosData = modelPack.position;
            rendererComponent.meshVertexCount = mesh.GetVertexCount();
            rendererComponent.meshIndexCount = mesh.GetIndexCount();
            rendererComponent.meshPosData1 = new Vector3[mesh.GetVertexCount()];

            var modelResource = modelPack.pmx;

            int morphCount = modelResource.Morphs.Count;
            for (int i = 0; i < morphCount; i++)
            {
                if (modelResource.Morphs[i].Type == PMX_MorphType.Vertex)
                {
                    MorphVertexDesc[] morphVertexStructs = new MorphVertexDesc[modelResource.Morphs[i].MorphVertexs.Length];
                    PMX_MorphVertexDesc[] sourceMorph = modelResource.Morphs[i].MorphVertexs;
                    for (int j = 0; j < morphVertexStructs.Length; j++)
                    {
                        morphVertexStructs[j].VertexIndex = sourceMorph[j].VertexIndex;
                    }
                }
            }
            rendererComponent.ComputeVertexMorph();
        }

        public static void Initialize2(this MMDRendererComponent rendererComponent, ModelPack modelPack)
        {
            PMXFormat modelResource = modelPack.pmx;
            rendererComponent.bones.Clear();
            rendererComponent.cachedBoneKeyFrames.Clear();
            var _bones = modelResource.Bones;
            rendererComponent.boneMatricesData = new Matrix4x4[_bones.Count];
            for (int i = 0; i < _bones.Count; i++)
            {
                var _bone = _bones[i];
                BoneEntity boneEntity = new BoneEntity();
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
                        var ikLink = new BoneEntity.IKLink();
                        ikLink.HasLimit = _bone.boneIK.IKLinks[j].HasLimit;
                        ikLink.LimitMax = _bone.boneIK.IKLinks[j].LimitMax;
                        ikLink.LimitMin = _bone.boneIK.IKLinks[j].LimitMin;
                        ikLink.LinkedIndex = _bone.boneIK.IKLinks[j].LinkedIndex;


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

                        boneEntity.boneIKLinks[j] = ikLink;
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
                rendererComponent.bones.Add(boneEntity);
                rendererComponent.cachedBoneKeyFrames.Add(new BoneKeyFrame());
            }

            rendererComponent.BakeSequenceProcessMatrixsIndex();
            rendererComponent.rigidBodyDescs.Clear();
            rendererComponent.rigidBodyDescs.AddRange(modelPack.rigidBodyDescs);
            for (int i = 0; i < modelPack.rigidBodyDescs.Count; i++)
            {
                var rigidBodyDesc = rendererComponent.rigidBodyDescs[i];

                if (rigidBodyDesc.Type != RigidBodyType.Kinematic && rigidBodyDesc.AssociatedBoneIndex != -1)
                    rendererComponent.bones[rigidBodyDesc.AssociatedBoneIndex].IsPhysicsFreeBone = true;
            }
            rendererComponent.jointDescs.Clear();
            rendererComponent.jointDescs.AddRange(modelPack.jointDescs);
        }
    }
}