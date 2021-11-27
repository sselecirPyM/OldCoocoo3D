using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.Present
{
    public struct CameraData
    {
        public Matrix4x4 vMatrix;
        public Matrix4x4 pMatrix;
        public Matrix4x4 vpMatrix;
        public Matrix4x4 pvMatrix;
        public Vector3 LookAtPoint;
        public float Distance;
        public Vector3 Angle;
        public float Fov;
        public float AspectRatio;
        public Vector3 Position;
    }
    public class Camera
    {
        public Vector3 LookAtPoint = new Vector3(0, 10, 0);
        public float Distance = -45;
        public Vector3 Angle;
        public float Fov = MathF.PI / 6;
        public float AspectRatio = 1;
        public float farClip = 3000.0f;
        public float nearClip = 2.0f;
        public CameraMotion cameraMotion = new CameraMotion();
        public bool CameraMotionOn = false;

        public void SetCameraMotion(float time)
        {
            var keyFrame = cameraMotion.GetCameraMotion(time);
            Distance = keyFrame.distance;
            Angle = keyFrame.rotation;
            Fov = Math.Clamp(keyFrame.FOV, 0.1f, 179.9f) / 180 * MathF.PI;
            LookAtPoint = keyFrame.position;
        }

        public void RotateDelta(Vector3 delta)
        {
            Angle += delta;
        }

        public void MoveDelta(Vector3 delta)
        {
            Matrix4x4 rotateMatrix = Matrix4x4.CreateFromYawPitchRoll(-Angle.Y, -Angle.X, -Angle.Z);
            LookAtPoint += Vector3.Transform(delta, rotateMatrix);
        }
        public CameraData GetCameraData()
        {
            Matrix4x4 rotateMatrix = Matrix4x4.CreateFromYawPitchRoll(-Angle.Y, -Angle.X, -Angle.Z);
            var pos = Vector3.Transform(Vector3.UnitZ * Distance, rotateMatrix * Matrix4x4.CreateTranslation(LookAtPoint));
            var up = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, rotateMatrix));
            Matrix4x4 vMatrix = Matrix4x4.CreateLookAt(pos, LookAtPoint, up);
            Matrix4x4 pMatrix = Matrix4x4.CreatePerspectiveFieldOfView(Fov, AspectRatio, MathF.Max(nearClip, 0.001f), MathF.Max(MathF.Max(farClip, nearClip + 1e-2f), 0.002f));
            Matrix4x4 vpMatrix = Matrix4x4.Multiply(vMatrix, pMatrix);
            Matrix4x4.Invert(vpMatrix, out Matrix4x4 pvMatrix);
            return new CameraData()
            {
                Angle = Angle,
                AspectRatio = AspectRatio,
                Distance = Distance,
                Fov = Fov,
                LookAtPoint = LookAtPoint,
                Position = pos,
                vMatrix = vMatrix,
                pMatrix = pMatrix,
                vpMatrix = vpMatrix,
                pvMatrix = pvMatrix,
            };
        }
    }
}
