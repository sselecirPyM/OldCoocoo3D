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
        { DebugRenderType.AO,"DEBUG_AO"},
        { DebugRenderType.Depth,"DEBUG_DEPTH"},
        { DebugRenderType.Diffuse,"DEBUG_DIFFUSE"},
        { DebugRenderType.DiffuseProbes,"DEBUG_DIFFUSE_PROBES"},
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
        var mainCaches = param.mainCaches;
        var directionalLights = param.directionalLights;
        var pointLights = param.pointLights;
        PSO pso = null;
        bool skinning = true;
        if (param.customValue.TryGetValue("Skinning", out object oSkinning) && oSkinning is bool bSkinning)
            skinning = bSkinning;
        switch (param.passName)
        {
            case "GBufferPass":
                foreach (var renderer in param.renderers)
                {
                    param.SetMesh(graphicsContext, renderer);
                    param.renderer = renderer;

                    foreach (var material in renderer.Materials)
                    {
                        if (material.Transparent) continue;
                        param.material = material;
                        var psoDesc = param.GetPSODesc();
                        List<string> keywords = new List<string>();
                        if (debugKeywords.TryGetValue(param.settings.DebugRenderType, out string debugKeyword))
                            keywords.Add(debugKeyword);
                        if (param.renderer.skinning && skinning)
                        {
                            keywords.Add("SKINNING");
                            graphicsContext.SetCBVRSlot(param.GetBoneBuffer(param.renderer), 0, 0, 0);
                        }
                        foreach (var cbv in param.pass.CBVs)
                        {
                            param.WriteCBV(cbv);
                        }
                        pso = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("DeferredGBuffer.hlsl", param.relativePath));
                        param.SetSRVs(param.pass.SRVs, material);
                        if (pso != null && graphicsContext.SetPSO(pso, psoDesc))
                            graphicsContext.DrawIndexed(material.indexCount, material.indexOffset, 0);
                    }
                }
                break;
            case "DeferredFinalPass":
                {
                    var psoDesc = param.GetPSODesc();
                    List<string> keywords = new List<string>();
                    if (debugKeywords.TryGetValue(param.settings.DebugRenderType, out string debugKeyword))
                        keywords.Add(debugKeyword);
                    if ((bool)param.GetSettingsValue("UseGI"))
                        keywords.Add("ENABLE_GI");
                    if ((bool)param.GetSettingsValue("EnableFog"))
                        keywords.Add("ENABLE_FOG");
                    if ((bool)param.GetSettingsValue("EnableSSAO"))
                        keywords.Add("ENABLE_SSAO");

                    if (directionalLights.Count != 0)
                    {
                        keywords.Add("ENABLE_DIRECTIONAL_LIGHT");
                        if ((bool)param.GetSettingsValue("EnableVolumetricLighting"))
                            keywords.Add("ENABLE_VOLUME_LIGHTING");
                    }
                    if (pointLights.Count != 0)
                        keywords.Add("ENABLE_POINT_LIGHT");
                    if (param.customValue.TryGetValue("RayTracing", out object oIsRayTracing) && oIsRayTracing is bool bIsRayTracing && bIsRayTracing)
                        keywords.Add("RAY_TRACING");

                    foreach (var cbv in param.pass.CBVs)
                    {
                        param.WriteCBV(cbv);
                    }
                    pso = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("DeferredFinal.hlsl", param.relativePath));
                    param.SetSRVs(param.pass.SRVs, null);
                    if (pso != null && graphicsContext.SetPSO(pso, psoDesc))
                        graphicsContext.DrawIndexed(6, 0, 0);
                }
                break;
            case "DenoisePass":
                {
                    if (!(param.customValue.TryGetValue("RayTracing", out object oIsRayTracing) && oIsRayTracing is bool bIsRayTracing && bIsRayTracing))
                        return true;

                    var psoDesc = param.GetPSODesc();
                    List<string> keywords = new List<string>();
                    if (debugKeywords.TryGetValue(param.settings.DebugRenderType, out string debugKeyword))
                        keywords.Add(debugKeyword);

                    foreach (var cbv in param.pass.CBVs)
                    {
                        param.WriteCBV(cbv);
                    }
                    pso = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("RayTracingDenoise.hlsl", param.relativePath));
                    param.SetSRVs(param.pass.SRVs, null);
                    if (pso != null && graphicsContext.SetPSO(pso, psoDesc))
                        graphicsContext.DrawIndexed(6, 0, 0);
                }
                break;
            default:
                return false;
        }
        return true;
    }
}