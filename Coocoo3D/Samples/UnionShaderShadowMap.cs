using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.IO;
using System.Collections.Generic;
public static class UnionShaderShadowMap
{
    public static bool UnionShader(UnionShaderParam param)
    {
        var graphicsContext = param.graphicsContext;
        var mainCaches = param.mainCaches;
        bool skinning = true;
        if (param.customValue.TryGetValue("Skinning", out object oSkinning) && oSkinning is bool bSkinning)
            skinning = bSkinning;

        int width = param.depthStencil.width;
        int height = param.depthStencil.height;
        if (param.passName == "ShadowMapPass0")
            graphicsContext.RSSetScissorRectAndViewport(0, 0, width / 2, height);
        if (param.passName == "ShadowMapPass1")
            graphicsContext.RSSetScissorRectAndViewport(width / 2, 0, width, height);
        foreach (var renderer in param.renderers)
        {
            param.SetMesh(graphicsContext, renderer);
            param.renderer = renderer;

            foreach (var material in renderer.Materials)
            {
                param.material = material;
                var psoDesc = param.GetPSODesc();

                if ((bool)param.GetSettingsValue(material, "CastShadow"))
                {
                    if (!param.visualChannel.CustomValue.ContainsKey(param.passName))
                    {
                        param.visualChannel.CustomValue[param.passName] = 0;
                    }
                    List<string> keywords = new List<string>();
                    if (param.renderer.skinning && skinning)
                        keywords.Add("SKINNING");
                    foreach (var cbv in param.pass.CBVs)
                    {
                        param.WriteCBV(cbv);
                    }
                    var pso = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("ShadowMap.hlsl", param.relativePath), true, false);
                    param.SetSRVs(param.pass.SRVs, material);
                    if (graphicsContext.SetPSO(pso, psoDesc))
                    {
                        graphicsContext.SetCBVRSlot(param.GetBoneBuffer(param.renderer), 0, 0, 0);
                        graphicsContext.DrawIndexed(material.indexCount, material.indexOffset, 0);
                    }
                }
            }
        }
        return true;
    }
}