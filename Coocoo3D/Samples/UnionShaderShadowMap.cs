using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.IO;
public static class UnionShaderShadowMap
{
    public static bool UnionShader(UnionShaderParam param)
    {
        var graphicsContext = param.graphicsContext;
        var mainCaches = param.rp.mainCaches;
        var psoDesc = param.PSODesc;
        var material = param.material;
        if (material.CastShadow)
        {
            var pso1 = mainCaches.GetPSOWithKeywords(null, Path.GetFullPath("ShadowMap.hlsl", param.relativePath), true, false);
            param.graphicsContext.SetPSO(pso1, psoDesc);
            graphicsContext.SetCBVRSlot(param.rp.GetBoneBuffer(param.renderer), 0, 0, 0);
            graphicsContext.DrawIndexed(material.indexCount, material.indexOffset, 0);
        }
        return true;
    }
}