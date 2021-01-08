using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public struct PresentData
    {
        public void UpdateCameraData(Present.CameraData camera)
        {
            wpMatrix = Matrix4x4.Transpose(camera.vpMatrix);
            Matrix4x4.Invert(camera.vpMatrix, out pwMatrix);
            pwMatrix = Matrix4x4.Transpose(pwMatrix);
            CameraPosition = Vector3.Zero;
            AspectRatio = camera.AspectRatio;
        }

        public Matrix4x4 wpMatrix;
        public Matrix4x4 pwMatrix;
        public Vector3 CameraPosition;
        public float AspectRatio;
        public float PlayTime;
        public float DeltaTime;
        public int RandomValue1;
        public int RandomValue2;
        public InShaderSettings inShaderSettings;
    }
}
