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
        var graphicsContext = param.graphicsContext;

        var directionalLights = param.directionalLights;
        var pointLights = param.pointLights;
        bool skinning = true;
        if (param.customValue.TryGetValue("Skinning", out object oSkinning) && oSkinning is bool bSkinning)
            skinning = bSkinning;

        switch (param.passName)
        {
            case "DrawObjectPass":
            case "DrawTransparentPass":
                param.WriteCBV(param.pass.CBVs[1]);
                foreach (var renderer in param.renderers)
                {
                    param.SetMesh(graphicsContext, renderer);
                    param.renderer = renderer;

                    foreach (var material in renderer.Materials)
                    {
                        if (param.passName == "DrawTransparentPass" && !material.Transparent) continue;
                        param.material = material;
                        var psoDesc = param.GetPSODesc();
                        bool receiveShadow = (bool)param.GetSettingsValue(material, "ReceiveShadow");

                        List<string> keywords = new List<string>();
                        if (debugKeywords.TryGetValue(param.settings.DebugRenderType, out string debugKeyword))
                            keywords.Add(debugKeyword);
                        if ((bool)param.GetSettingsValue("EnableFog"))
                            keywords.Add("ENABLE_FOG");
                        if ((bool)param.GetSettingsValue(material, "UseNormalMap"))
                            keywords.Add("USE_NORMAL_MAP");

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

                        //foreach (var cbv in param.pass.CBVs)
                        //{
                        //    param.WriteCBV(cbv);
                        //}
                        param.WriteCBV(param.pass.CBVs[0]);
                        pso = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("PBRMaterial.hlsl", param.relativePath));
                        param.SetSRVs(param.pass.SRVs, material);
                        if (pso != null && graphicsContext.SetPSO(pso, psoDesc))
                            graphicsContext.DrawIndexed(material.indexCount, material.indexOffset, 0);
                    }
                }
                break;
            case "DrawSkyBoxPass":
                {
                    foreach (var cbv in param.pass.CBVs)
                    {
                        param.WriteCBV(cbv);
                    }
                    var psoDesc = param.GetPSODesc();
                    pso = mainCaches.GetPSOWithKeywords(null, Path.GetFullPath("SkyBox.hlsl", param.relativePath));
                    param.SetSRVs(param.pass.SRVs, null);
                    if (pso != null && graphicsContext.SetPSO(pso, psoDesc))
                    {
                        graphicsContext.DrawIndexed(6, 0, 0);
                    }
                }
                break;
            default:
                return false;
        }
        return true;
    }
}