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
        var psoDesc = param.PSODesc;
        var material = param.material;
        bool skinning = true;
        if (param.customValue.TryGetValue("Skinning", out object oSkinning) && oSkinning is bool bSkinning && !bSkinning)
            skinning = false;

        if ((bool)param.GetSettingsValue(material, "CastShadow"))
        {
            if (!param.visualChannel.CustomValue.ContainsKey(param.passName))
            {
                param.visualChannel.CustomValue[param.passName] = 0;
                int width = param.depthStencil.GetWidth();
                int height = param.depthStencil.GetHeight();
                if (param.passName == "ShadowMapPass0")
                    graphicsContext.RSSetScissorRectAndViewport(0, 0, width / 2, height);
                if (param.passName == "ShadowMapPass1")
                    graphicsContext.RSSetScissorRectAndViewport(width / 2, 0, width, height);
            }
            List<string> keywords = new List<string>();
            if (material.Skinning && param.renderer.skinning && skinning)
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
        return true;
    }
}