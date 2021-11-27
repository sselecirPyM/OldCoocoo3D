using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.IO;
using System.Collections.Generic;
public static class UnionShaderPBRForward
{
    public static bool UnionShader(UnionShaderParam param)
    {
        var mainCaches = param.rp.mainCaches;
        PSO pso1 = null;
        var material = param.material;
        var graphicsContext = param.graphicsContext;
        var psoDesc = param.PSODesc;
        var lightings = param.rp.dynamicContextRead.lightings;
        if (material != null)
        {
            switch (param.passName)
            {
                case "DrawObjectPass":
                    {
                        bool hasLight = lightings.Count != 0;
                        bool receiveShadow = material.ReceiveShadow;

                        List<string> keywords = new List<string>();
                        if (hasLight)
                        {
                            if(!receiveShadow)
                                keywords.Add("DISBLE_SHADOW_RECEIVE");
                            keywords.Add("ENABLE_LIGHT");
                        }
                        pso1 = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("PBRMaterial.hlsl", param.relativePath));
                    }
                    break;
                default:
                    return false;
            }
            graphicsContext.SetCBVRSlot(param.rp.GetBoneBuffer(param.renderer), 0, 0, 0);
            param.graphicsContext.SetPSO(pso1, psoDesc);
            graphicsContext.DrawIndexed(material.indexCount, material.indexOffset, 0);
            return true;
        }
        else
        {
            switch (param.passName)
            {
                case "DrawSkyBoxPass":
                    {
                        pso1 = mainCaches.GetPSOWithKeywords(null, Path.GetFullPath("SkyBox.hlsl", param.relativePath));
                    }
                    break;
                default:
                    return false;
            }
            param.graphicsContext.SetPSO(pso1, psoDesc);
            graphicsContext.DrawIndexed(6, 0, 0);
            return true;
        }
    }
}