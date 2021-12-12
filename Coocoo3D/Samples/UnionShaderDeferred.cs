using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.IO;
using System.Collections.Generic;
public static class UnionShaderDeferred
{
    static Dictionary<DebugRenderType, string> debugKeywords = new Dictionary<DebugRenderType, string>()
    {
        { DebugRenderType.Albedo,"DEBUG_ALBEDO"},
        { DebugRenderType.Depth,"DEBUG_DEPTH"},
        { DebugRenderType.Diffuse,"DEBUG_DIFFUSE"},
        { DebugRenderType.DiffuseRender,"DEBUG_DIFFUSE_RENDER"},
        { DebugRenderType.Emissive,"DEBUG_EMISSIVE"},
        { DebugRenderType.Normal,"DEBUG_NORMAL"},
        { DebugRenderType.Position,"DEBUG_POSITION"},
        { DebugRenderType.Roughness,"DEBUG_ROUGHNESS"},
        { DebugRenderType.Specular,"DEBUG_SPECULAR"},
        { DebugRenderType.SpecularRender,"DEBUG_SPECULAR_RENDER"},
        { DebugRenderType.UV,"DEBUG_UV"},
    };
    public static bool UnionShader(UnionShaderParam param)
    {
        var graphicsContext = param.graphicsContext;
        var mainCaches = param.rp.mainCaches;
        var psoDesc = param.PSODesc;
        var material = param.material;
        var drp = param.rp.dynamicContextRead;
        var lightings = drp.lightings;
        PSO pso1 = null;
        switch (param.passName)
        {
            case "GBufferPass":
                {
                    List<string> keywords = new List<string>();
                    if (material.Skinning)
                        keywords.Add("SKINNING");
                    pso1 = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("DeferredGBuffer.hlsl", param.relativePath));
                }
                break;
            case "DeferredFinalPass":
                {
                    bool hasLight = lightings.Count != 0;

                    List<string> keywords = new List<string>();
                    if (debugKeywords.TryGetValue(param.settings.DebugRenderType, out string debugKeyword))
                        keywords.Add(debugKeyword);
                    if ((bool)drp.GetSettingsValue("EnableFog"))
                        keywords.Add("ENABLE_FOG");
                    if ((bool)drp.GetSettingsValue("EnableVolumetricLighting"))
                        keywords.Add("ENABLE_VOLUME_LIGHTING");
                    if (hasLight)
                        keywords.Add("ENABLE_LIGHT");
                    pso1 = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("DeferredFinal.hlsl", param.relativePath));
                }
                break;
            default:
                return false;
        }
        if (pso1 != null)
        {
            param.graphicsContext.SetPSO(pso1, psoDesc);
            if (material != null)
            {
                graphicsContext.SetCBVRSlot(param.rp.GetBoneBuffer(param.renderer), 0, 0, 0);
                graphicsContext.DrawIndexed(material.indexCount, material.indexOffset, 0);
            }
            else
                graphicsContext.DrawIndexed(6, 0, 0);
        }
        return true;
    }
}