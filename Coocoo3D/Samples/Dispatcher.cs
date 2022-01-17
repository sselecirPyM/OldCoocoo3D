using System;
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

        param.customValue["Skinning"] = !param.rp.CPUSkinning;
        param.rp.CPUSkinning = false;

        foreach (var renderSequence in passSetting.RenderSequence)
        {
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