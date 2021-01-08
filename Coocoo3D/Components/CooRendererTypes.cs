using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Components
{
    public enum BoneFlags
    {
        ChildUseId = 1,
        Rotatable = 2,
        Movable = 4,
        Visible = 8,
        Controllable = 16,
        HasIK = 32,
        AcquireRotate = 256,
        AcquireTranslate = 512,
        RotAxisFixed = 1024,
        UseLocalAxis = 2048,
        PostPhysics = 4096,
        ReceiveTransform = 8192
    }
    public enum MorphType
    {
        Group = 0,
        Vertex = 1,
        Bone = 2,
        UV = 3,
        ExtUV1 = 4,
        ExtUV2 = 5,
        ExtUV3 = 6,
        ExtUV4 = 7,
        Material = 8
    }
    public struct MorphVertexDesc
    {
        public int VertexIndex;
        public Vector3 Offset;
    }
    public struct MorphBoneDesc
    {
        public int BoneIndex;
        public Vector3 Translation;
        public Quaternion Rotation;
    }
}
