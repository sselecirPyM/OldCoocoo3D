﻿using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Present;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;

namespace Coocoo3D.RenderPipeline
{
    //readonly when rendering
    public class RenderPipelineDynamicContext
    {
        public RenderPipelineDynamicContext()
        {
            for (int i = 0; i < 4; i++)
                lightMatrixCaches.Add(new Dictionary<Matrix4x4, Matrix4x4>());
        }

        public Settings settings;
        public List<GameObject> gameObjects = new List<GameObject>();
        public List<MMDRendererComponent> renderers = new List<MMDRendererComponent>();
        public List<VolumeComponent> volumes = new List<VolumeComponent>();

        public List<DirectionalLightData> directionalLights = new List<DirectionalLightData>();
        public List<PointLightData> pointLights = new List<PointLightData>();

        public Dictionary<MMDRendererComponent, int> findRenderer = new Dictionary<MMDRendererComponent, int>();
        public PassSetting currentPassSetting;
        public int frameRenderIndex;
        public double Time;
        public double DeltaTime;
        public double RealDeltaTime;
        public bool EnableDisplay;

        List<Dictionary<Matrix4x4, Matrix4x4>> lightMatrixCaches = new List<Dictionary<Matrix4x4, Matrix4x4>>();

        static float[] lightMatrixLevel = { 0.0f, 0.975f, 0.993f, 0.997f, 0.998f };
        public Matrix4x4 GetLightMatrix(Matrix4x4 pvMatrix, int level)
        {
            return GetLightMatrix1(pvMatrix, level, lightMatrixLevel[level], lightMatrixLevel[level + 1]);
        }

        public Matrix4x4 GetLightMatrix1(Matrix4x4 pvMatrix, int level, float start, float end)
        {
            if (lightMatrixCaches[level].TryGetValue(pvMatrix, out var mat1))
                return mat1;
            Matrix4x4 lightCameraMatrix0 = Matrix4x4.Identity;
            if (directionalLights.Count > 0)
            {
                lightCameraMatrix0 = directionalLights[0].GetLightingMatrix(pvMatrix, start, end);
            }
            lightMatrixCaches[level][pvMatrix] = lightCameraMatrix0;
            return lightCameraMatrix0;
        }

        public void Preprocess()
        {
            int rendererCount = 0;
            foreach (GameObject gameObject in gameObjects)
            {
                LightingComponent lightingComponent = gameObject.GetComponent<LightingComponent>();
                if (lightingComponent != null)
                {
                    lightingComponent.Position = gameObject.Position;
                    lightingComponent.Rotation = gameObject.Rotation;

                    if (lightingComponent.LightingType == LightingType.Directional)
                        directionalLights.Add(lightingComponent.GetDirectionalLightData());
                    if (lightingComponent.LightingType == LightingType.Point)
                        pointLights.Add(lightingComponent.GetPointLightData());
                }
                VolumeComponent volume = gameObject.GetComponent<VolumeComponent>();
                if (volume != null)
                {
                    volume.Position = gameObject.Position;
                    volumes.Add(volume);
                }
                MMDRendererComponent rendererComponent = gameObject.GetComponent<MMDRendererComponent>();
                if (rendererComponent != null)
                {
                    rendererComponent.position = gameObject.Position;
                    rendererComponent.rotation = gameObject.Rotation;
                    renderers.Add(rendererComponent);
                    findRenderer[rendererComponent] = rendererCount;
                    rendererCount++;
                }
            }
        }

        public void FrameBegin()
        {
            for (int i = 0; i < lightMatrixCaches.Count; i++)
            {
                lightMatrixCaches[i].Clear();
            }
            gameObjects.Clear();
            directionalLights.Clear();
            pointLights.Clear();
            volumes.Clear();
            renderers.Clear();
            findRenderer.Clear();
        }
    }
}
