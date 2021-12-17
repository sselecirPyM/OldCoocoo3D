using System;
using Coocoo3D.RenderPipeline;
using System.IO;
using System.Collections.Generic;
public class Dispatcher : IPassDispatcher
{
    public void FrameBegin(RenderPipelineContext context)
    {

    }

    public void FrameEnd(RenderPipelineContext context)
    {

    }

    public void Dispatch(UnionShaderParam unionShaderParam)
    {
        var passSetting = unionShaderParam.passSetting;
        var drp = unionShaderParam.rp.dynamicContextRead;
        foreach (var renderSequence in passSetting.RenderSequence)
        {
            unionShaderParam.renderSequence = renderSequence;
            var Pass = passSetting.Passes[renderSequence.Name];

            if (Pass.Properties != null && Pass.Properties.ContainsKey("ShadowMap"))
            {
                if (!(drp.directionalLights.Count > 0))
                {
                    continue;
                }
            }
            HybirdRenderPipeline.DispatchPass(unionShaderParam);
        }
    }
}