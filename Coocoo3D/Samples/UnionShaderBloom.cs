using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.IO;
using System.Collections.Generic;
public static class UnionShaderBloom
{
    public static bool UnionShader(UnionShaderParam param)
    {
        if ((bool?)param.GetSettingsValue("EnableBloom") != true) return true;
        var graphicsContext = param.graphicsContext;
        var mainCaches = param.mainCaches;
        var psoDesc = param.PSODesc;


        var writer = param.GPUWriter;
        Texture2D renderTarget = param.renderTargets[0];
        writer.Write(renderTarget.GetWidth());
        writer.Write(renderTarget.GetHeight());
        writer.Write((float)param.GetSettingsValue("BloomThreshold"));
        writer.Write((float)param.GetSettingsValue("BloomIntensity"));
        writer.SetBufferImmediately(graphicsContext, false, 0);

        PSO pso = null;
        switch (param.passName)
        {
            case "BloomBlur1":
                {
                    List<string> keywords = new List<string>();
                    keywords.Add("BLOOM_1");
                    pso = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("Bloom.hlsl", param.relativePath));
                }
                break;
            case "BloomBlur2":
                {
                    List<string> keywords = new List<string>();
                    keywords.Add("BLOOM_2");
                    pso = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("Bloom.hlsl", param.relativePath));
                }
                break;
            default:
                return false;
        }
        if (param.settings.DebugRenderType == DebugRenderType.Bloom)
        {
            psoDesc.blendState = BlendState.None;
        }
        param.SetSRVs(param.pass.SRVs);
        if (pso != null && graphicsContext.SetPSO(pso, psoDesc))
            graphicsContext.DrawIndexed(6, 0, 0);
        return true;
    }
}