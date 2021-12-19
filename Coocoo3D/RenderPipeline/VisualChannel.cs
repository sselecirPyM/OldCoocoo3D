﻿using Coocoo3D.Numerics;
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
        public GraphicsContext graphicsContext;
        public Texture2D FinalOutput = new Texture2D();
        public Texture2D OutputRTV = new Texture2D();
        public Dictionary<string, object> CustomValue = new Dictionary<string, object>();

        public void Onframe(RenderPipelineContext RPContext)
        {
            if (camera.CameraMotionOn) camera.SetCameraMotion((float)RPContext.gameDriverContext.PlayTime);
            cameraData = camera.GetCameraData();
            CustomValue.Clear();
        }

        public void Dispose()
        {
            FinalOutput.Dispose();
            OutputRTV.Dispose();
        }

        public string GetTexName(string texName)
        {
            return string.Format("SceneView/{0}/{1}", Name, texName);
        }
    }
}
