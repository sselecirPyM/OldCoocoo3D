using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.IO;
using System.Collections.Generic;
public static class UnionShaderPBRForward
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
        var mainCaches = param.mainCaches;
        PSO pso = null;
        var material = param.material;
        var graphicsContext = param.graphicsContext;
        var psoDesc = param.GetPSODesc();

        var directionalLights = param.directionalLights;
        var pointLights = param.pointLights;
        bool skinning = true;
        if (param.customValue.TryGetValue("Skinning", out object oSkinning) && oSkinning is bool bSkinning)
            skinning = bSkinning;

        if (material != null)
        {
            switch (param.passName)
            {
                case "DrawObjectPass":
                    {
                        bool receiveShadow = (bool)param.GetSettingsValue(material, "ReceiveShadow");

                        List<string> keywords = new List<string>();
                        if (debugKeywords.TryGetValue(param.settings.DebugRenderType, out string debugKeyword))
                            keywords.Add(debugKeyword);
                        if ((bool)param.GetSettingsValue("EnableFog"))
                            keywords.Add("ENABLE_FOG");

                        if ((bool?)param.GetSettingsValue("UseGI") == true)
                            keywords.Add("ENABLE_GI");
                        if (param.renderer.skinning && skinning)
                        {
                            graphicsContext.SetCBVRSlot(param.GetBoneBuffer(param.renderer), 0, 0, 0);
                            keywords.Add("SKINNING");
                        }

                        if (directionalLights.Count != 0)
                        {
                            if (!receiveShadow)
                                keywords.Add("DISBLE_SHADOW_RECEIVE");
                            keywords.Add("ENABLE_DIRECTIONAL_LIGHT");
                        }
                        if (pointLights.Count != 0)
                            keywords.Add("ENABLE_POINT_LIGHT");

                        foreach (var cbv in param.pass.CBVs)
                        {
                            param.WriteCBV(cbv);
                        }
                        pso = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("PBRMaterial.hlsl", param.relativePath));
                    }
                    break;
                default:
                    return false;
            }
            param.SetSRVs(param.pass.SRVs, material);
            if (pso != null && graphicsContext.SetPSO(pso, psoDesc))
                graphicsContext.DrawIndexed(material.indexCount, material.indexOffset, 0);
            return true;
        }
        else
        {
            switch (param.passName)
            {
                case "DrawSkyBoxPass":
                    {
                        foreach (var cbv in param.pass.CBVs)
                        {
                            param.WriteCBV(cbv);
                        }
                        pso = mainCaches.GetPSOWithKeywords(null, Path.GetFullPath("SkyBox.hlsl", param.relativePath));
                    }
                    break;
                default:
                    return false;
            }
            param.SetSRVs(param.pass.SRVs, material);
            if (pso != null && graphicsContext.SetPSO(pso, psoDesc))
            {
                graphicsContext.DrawIndexed(6, 0, 0);
            }
            return true;
        }
    }
}