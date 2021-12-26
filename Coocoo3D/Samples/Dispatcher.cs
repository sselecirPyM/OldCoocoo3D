﻿using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
public class Dispatcher : IPassDispatcher
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

        bool rayTracing = false;

        foreach (var renderSequence in passSetting.RenderSequence)
        {
            if (renderSequence.Type == "RayTracing" && param.graphicsDevice.IsRayTracingSupport())
            {
                rayTracing = true;
            }
        }
        if (!(param.GetSettingsValue("EnableRayTracing") is bool bRayTracing && bRayTracing))
            rayTracing = false;
        param.customValue["RayTracing"] = rayTracing;
        param.customValue["Skinning"] = !param.rp.CPUSkinning;
        if (rayTracing)
            param.rp.CPUSkinning = true;
        else
            param.rp.CPUSkinning = false;

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