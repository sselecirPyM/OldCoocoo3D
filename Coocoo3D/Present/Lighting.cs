using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Coocoo3DGraphics;

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

        public Matrix4x4 GetLightingMatrix(float ExtendRange, Vector3 cameraLookAt, float cameraDistance)
        {
            Matrix4x4 vpMatrix = Matrix4x4.Identity;
            if (LightingType == LightingType.Directional)
            {
                Vector3 lookat = cameraLookAt + Vector3.UnitY * 4;


                Matrix4x4 rotateMatrix = Matrix4x4.CreateFromQuaternion(Rotation);
                var pos = Vector3.Transform(-Vector3.UnitZ * 512, rotateMatrix);
                var up = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, rotateMatrix));
                Matrix4x4 vMatrix = Matrix4x4.CreateLookAt(pos + lookat, lookat, up);
                Matrix4x4 pMatrix;
                float dist = MathF.Abs(cameraDistance);
                pMatrix = Matrix4x4.CreateOrthographic(dist + ExtendRange, dist + ExtendRange, 0.0f, 1024) * Matrix4x4.CreateScale(-1, 1, 1);
                vpMatrix = Matrix4x4.Multiply(vMatrix, pMatrix);

            }
            else if (LightingType == LightingType.Point)
            {

            }
            return vpMatrix;
        }
        public Matrix4x4 GetLightingMatrix(float ExtendRange, Vector3 cameraLookAt, Vector3 cameraRotation, float cameraDistance)
        {
            Matrix4x4 vpMatrix = Matrix4x4.Identity;
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
                vpMatrix = Matrix4x4.Multiply(vMatrix, pMatrix);

            }
            else if (LightingType == LightingType.Point)
            {

            }
            return vpMatrix;
        }
        public Vector3 GetPositionOrDirection(Vector3 pos1)
        {
            Vector3 result = LightingType == LightingType.Directional ? Vector3.Transform(-Vector3.UnitZ, Rotation) : Position - pos1;
            return result;
        }
    }

    public class Lighting : ISceneObject, INotifyPropertyChanged
    {
        public string Name = "";
        public LightingType LightingType;
        public Vector3 Position;
        public Quaternion Rotation = Quaternion.Identity;
        public Vector4 Color;

        public Matrix4x4 vpMatrix;
        public Matrix4x4 rotateMatrix;
        public float Range;
        public event PropertyChangedEventHandler PropertyChanged;

        public void PropChange(PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, e);
        }

        public override string ToString()
        {
            return Name;
        }

        public LightingData GetLightingData()
        {
            return new LightingData
            {
                LightingType = LightingType,
                Position = Position,
                Rotation = Rotation,
                Color = Color,
                Range = Range,
            };
        }
    }
}
