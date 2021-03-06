using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.RenderPipeline;
using Vortice.Mathematics;

namespace Coocoo3D.Present
{
    public enum LightingType : uint
    {
        Directional = 0,
        Point = 1,
    }
    public struct PointLightData
    {
        public Vector3 Position;
        public Vector3 Color;
        public float Range;
    }
    public struct DirectionalLightData
    {
        public Vector3 Direction;
        public Vector3 Color;
        public Quaternion Rotation;

        public Matrix4x4 GetLightingMatrix(Matrix4x4 cameraInvert, float start, float end)
        {
            Matrix4x4 rotateMatrix = Matrix4x4.CreateFromQuaternion(Rotation);
            Matrix4x4.Invert(rotateMatrix, out Matrix4x4 iRot);
            var up = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, rotateMatrix));
            Vector4 v1x = Vector4.Transform(new Vector4(-1, -1, start, 1), cameraInvert);
            Vector3 v2x = new Vector3(v1x.X / v1x.W, v1x.Y / v1x.W, v1x.Z / v1x.W);
            Vector3 v3x = Vector3.Transform(v2x, iRot);
            Vector3 whMin = v3x;
            Vector3 whMax = v3x;

            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    for (int k = 0; k <= 1; k += 1)
                    {
                        Vector4 v1 = Vector4.Transform(new Vector4(i, j, ((k == 0) ? start : end), 1), cameraInvert);
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

        public Matrix4x4 GetLightingMatrix(RenderPipelineDynamicContext dc)
        {
            Matrix4x4 rotateMatrix = Matrix4x4.CreateFromQuaternion(Rotation);
            Matrix4x4.Invert(rotateMatrix, out Matrix4x4 iRot);
            var pos = Vector3.Transform(-Vector3.UnitZ * 512, rotateMatrix);
            var up = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, rotateMatrix));
            Vector3 whMin = Vector3.Zero;
            Vector3 whMax = Vector3.Zero;
            if (dc.volumes.Count > 0)
            {
                var volume = dc.volumes[0];
                var size1 = Vector3.Abs(volume.Size);
                var bb = new BoundingBox(volume.Position - size1 * 0.5f, volume.Position + size1 * 0.5f);

                Vector3 v1 = Vector3.Transform(bb.Extent * new Vector3(-1, -1, -1), iRot) + bb.Center;
                whMin = v1;
                whMax = v1;
            }
            foreach (var volume in dc.volumes)
            {
                var size1 = Vector3.Abs(volume.Size);
                var bb = new BoundingBox(volume.Position - size1 * 0.5f, volume.Position + size1 * 0.5f);

                for (int i = -1; i <= 1; i += 2)
                    for (int j = -1; j <= 1; j += 2)
                        for (int k = -1; k <= 1; k += 2)
                        {
                            Vector3 v1 = Vector3.Transform(bb.Extent * new Vector3(i, j, k), iRot) + bb.Center;
                            whMin = Vector3.Min(v1, whMin);
                            whMax = Vector3.Max(v1, whMax);
                        }
            }


            Vector3 range = whMax - whMin;
            Vector3 pos1 = whMin + range * 0.5f;
            Matrix4x4 v2 = Matrix4x4.CreateLookAt(pos + pos1, pos1, up);
            Matrix4x4 p = Matrix4x4.CreateOrthographic(range.X, range.Y, 0.0f, 1024) * Matrix4x4.CreateScale(-1, 1, 1);
            Matrix4x4 vp = v2 * p;

            return vp;
        }
    }
}
