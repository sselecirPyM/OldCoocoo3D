using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
public class DRDispatcher : IPassDispatcher
{
    public void FrameBegin(RenderPipelineContext context)
    {
    }

    public void FrameEnd(RenderPipelineContext context)
    {

    }

    public void Dispatch(UnionShaderParam param)
    {
        var passSetting = param.passSetting;
        var graphicsContext = param.graphicsContext;
        var renderers = param.renderers;

        param.customValue["Skinning"] = !param.rp.CPUSkinning;

        bool rayTracing = false;
        foreach (var renderSequence in passSetting.RenderSequence)
            if (renderSequence.Type == "RayTracing" && param.graphicsDevice.IsRayTracingSupport())
                rayTracing = true;

        if ((bool?)param.GetSettingsValue("EnableRayTracing") != true)
            rayTracing = false;
        param.customValue["RayTracing"] = rayTracing;
        param.rp.CPUSkinning = rayTracing;
        var random = param.random;
        if ((bool?)param.GetSettingsValue("EnableTAA") == true)
        {
            var outputTex = param.GetTex2D("_Output0");
            int index1 = param.rp.frameRenderCount;
            Vector2 jitterVector = new Vector2((float)(random.NextDouble() * 2 - 1) / outputTex.width, (float)(random.NextDouble() * 2 - 1) / outputTex.height);
            param.visualChannel.cameraData = param.visualChannel.camera.GetCameraData(jitterVector);
        }

        foreach (var renderSequence in passSetting.RenderSequence)
        {
            if (renderSequence.Type == "RayTracing" && !rayTracing)
            {
                continue;
            }
            param.renderSequence = renderSequence;
            var Pass = passSetting.Passes[renderSequence.Name];

            if (Pass.Properties != null && Pass.Properties.ContainsKey("ShadowMap"))
            {
                if (!(param.directionalLights.Count > 0))
                {
                    continue;
                }
            }
            HybirdRenderPipeline.DispatchPass(param);
        }
    }
}