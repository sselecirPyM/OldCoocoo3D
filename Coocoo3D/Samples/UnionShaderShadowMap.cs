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
        var mainCaches = param.rp.mainCaches;
        var psoDesc = param.PSODesc;
        var material = param.material;
        var drp = param.rp.dynamicContextRead;
        if ((bool)drp.GetSettingsValue(material, "CastShadow"))
        {
            List<string> keywords = new List<string>();
            if (material.Skinning)
                keywords.Add("SKINNING");
            var pso1 = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("ShadowMap.hlsl", param.relativePath), true, false);
            param.graphicsContext.SetPSO(pso1, psoDesc);
            graphicsContext.SetCBVRSlot(param.rp.GetBoneBuffer(param.renderer), 0, 0, 0);
            graphicsContext.DrawIndexed(material.indexCount, material.indexOffset, 0);
        }
        return true;
    }
}