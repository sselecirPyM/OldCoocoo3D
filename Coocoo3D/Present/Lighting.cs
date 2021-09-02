using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Coocoo3DGraphics;
using Coocoo3D.Components;
using Coocoo3D.Numerics;

namespace Coocoo3D.Present
{
    public enum LightingType : uint
    {
        Directional = 0,
        Point = 1,
    }
    public struct LightingData : IComparable<LightingData>
    {
        public LightingType LightingType;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector4 Color;
        public float Range;

        public int CompareTo(LightingData other)
        {
            return ((int)LightingType).CompareTo((int)other.LightingType);
        }

        public Matrix4x4 GetLightingMatrix(float ExtendRange, Vector3 cameraLookAt, Vector3 cameraRotation, float cameraDistance)
        {
            Matrix4x4 vp = Matrix4x4.Identity;
            if (LightingType == LightingType.Directional)
            {
                Matrix4x4 rot = Matrix4x4.CreateFromYawPitchRoll(-cameraRotation.Y, -cameraRotation.X, -cameraRotation.Z);
                bool extendY = ((cameraRotation.X + MathF.PI / 4) % MathF.PI + MathF.PI) % MathF.PI < MathF.PI / 2;


                Matrix4x4 rotateMatrix = Matrix4x4.CreateFromQuaternion(Rotation);
                var pos = Vector3.Transform(-Vector3.UnitZ * 512, rotateMatrix);
                var up = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, rotateMatrix));
                Matrix4x4 pMatrix;

                float a = MathF.Abs((cameraRotation.X % MathF.PI + MathF.PI) % MathF.PI - MathF.PI / 2) / (MathF.PI / 4) - 0.5f;
                a = Math.Clamp(a * a - 0.25f, 0, 1);
                float dist = MathF.Abs(cameraDistance) * 1.5f;
                if (!extendY)
                    pMatrix = Matrix4x4.CreateOrthographic(dist + ExtendRange, dist + ExtendRange, 0.0f, 1024) * Matrix4x4.CreateScale(-1, 1, 1);
                else
                {
                    pMatrix = Matrix4x4.CreateOrthographic(dist + ExtendRange * (3 * a + 1), dist + ExtendRange * (3 * a + 1), 0.0f, 1024) * Matrix4x4.CreateScale(-1, 1, 1);
                }
                Vector3 viewdirXZ = Vector3.Normalize(Vector3.Transform(new Vector3(0, 0, 1), rot));
                Vector3 lookat = cameraLookAt + Vector3.UnitY * 8 + a * viewdirXZ * ExtendRange * 2;
                Matrix4x4 vMatrix = Matrix4x4.CreateLookAt(pos + lookat, lookat, up);
                vp = Matrix4x4.Multiply(vMatrix, pMatrix);

            }
            else if (LightingType == LightingType.Point)
            {

            }
            return vp;
        }
        public Matrix4x4 GetLightingMatrix(Matrix4x4 cameraInvert)
        {
            Matrix4x4 rotateMatrix = Matrix4x4.CreateFromQuaternion(Rotation);
            Matrix4x4.Invert(rotateMatrix, out Matrix4x4 iRot);
            var up = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, rotateMatrix));
            Vector4 v1x = Vector4.Transform(new Vector4(-1, -1, 0.0f, 1), cameraInvert);
            Vector3 v2x = new Vector3(v1x.X / v1x.W, v1x.Y / v1x.W, v1x.Z / v1x.W);
            Vector3 v3x = Vector3.Transform(v2x, iRot);
            Vector3 whMin = v3x;
            Vector3 whMax = v3x;
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    for (int k = 0; k <= 1; k += 1)
                    {
                        Vector4 v1 = Vector4.Transform(new Vector4(i, j, k * 0.993f, 1), cameraInvert);
                        Vector3 v2 = new Vector3(v1.X / v1.W, v1.Y / v1.W, v1.Z / v1.W);
                        Vector3 v3 = Vector3.Transform(v2, iRot);
                        whMin = Vector3.Min(v3, whMin);
                        whMax = Vector3.Max(v3, whMax);
                    }
            Vector3 whMax2 = whMax - whMin;

            var pos = Vector3.Transform(-Vector3.UnitZ * 512, rotateMatrix);
            Vector3 real = Vector3.Transform((whMax + whMin) * 0.5f, rotateMatrix);
            return Matrix4x4.CreateLookAt(real + pos, real, up) * Matrix4x4.CreateOrthographic(whMax2.X, whMax2.Y, 0.0f, 1024) * Matrix4x4.CreateScale(-1, 1, 1);
        }
        public Matrix4x4 GetLightingMatrix(BoundingBox bb)
        {
            Matrix4x4 vp = Matrix4x4.Identity;
            if (LightingType == LightingType.Directional)
            {
                Matrix4x4 rotateMatrix = Matrix4x4.CreateFromQuaternion(Rotation);
                Matrix4x4.Invert(rotateMatrix, out Matrix4x4 iRot);
                var pos = Vector3.Transform(-Vector3.UnitZ * 512, rotateMatrix);
                var up = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, rotateMatrix));
                Matrix4x4 v = Matrix4x4.CreateLookAt(pos + bb.position, bb.position, up);
                Vector3 whMin = Vector3.Zero;
                Vector3 whMax = Vector3.Zero;
                for (int i = -1; i <= 1; i += 2)
                    for (int j = -1; j <= 1; j += 2)
                        for (int k = -1; k <= 1; k += 2)
                        {
                            Vector3 v1 = Vector3.Transform(bb.extension * new Vector3(i, j, k) * 0.5f, iRot);
                            whMin = Vector3.Min(v1, whMin);
                            whMax = Vector3.Max(v1, whMax);
                        }

                whMax = whMax - whMin;
                Matrix4x4 p = Matrix4x4.CreateOrthographic(whMax.X, whMax.Y, 0.0f, 1024) * Matrix4x4.CreateScale(-1, 1, 1);
                vp = v * p;
            }
            return vp;
        }
        public Vector3 GetPositionOrDirection()
        {
            Vector3 result = LightingType == LightingType.Directional ? Vector3.Transform(-Vector3.UnitZ, Rotation) : Position;
            return result;
        }
        public LStruct GetLStruct()
        {
            return new LStruct { PosOrDir = GetPositionOrDirection(), Type = (uint)LightingType, Color = Color };
        }
        public struct LStruct
        {
            public Vector3 PosOrDir;
            public uint Type;
            public Vector4 Color;
        }
    }
}
