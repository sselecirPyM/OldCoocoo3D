using Coocoo3D.Numerics;
using Coocoo3D.Present;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public class VisualChannel : IDisposable
    {
        public string Name;
        public Camera camera = new Camera();
        public CameraData cameraData;
        public Int2 outputSize = new Int2(100, 100);
        public Int2 sceneViewSize = new Int2(100, 100);
        public Dictionary<string, int> customDataInt = new Dictionary<string, int>();
        public GraphicsContext graphicsContext;
        public Texture2D FinalOutput;
        public RenderPipeline renderPipeline;

        public void Onframe(RenderPipelineContext RPContext)
        {
            if (camera.CameraMotionOn) camera.SetCameraMotion((float)RPContext.gameDriverContext.PlayTime);
            camera.AspectRatio = RPContext.gameDriverContext.AspectRatio;

        }

        public void Dispose()
        {
            renderPipeline = null;

        }
    }
}
