using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
public static class UnionShaderRayTracing
{
    static Dictionary<DebugRenderType, string> debugKeywords = new Dictionary<DebugRenderType, string>()
    {
        { DebugRenderType.Albedo,"DEBUG_ALBEDO"},
        { DebugRenderType.AO,"DEBUG_AO"},
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
        var mainCaches = param.mainCaches;
        var directionalLights = param.directionalLights;
        var pointLights = param.pointLights;
        var rayTracingShader = param.rayTracingShader;
        var renderers = param.renderers;
        var camera = param.visualChannel.cameraData;
        switch (param.passName)
        {
            case "RayTracingPass":
                {
                    List<string> keywords = new List<string>();
                    if (directionalLights.Count != 0)
                    {
                        keywords.Add("ENABLE_DIRECTIONAL_LIGHT");
                        if ((bool)param.GetSettingsValue("EnableVolumetricLighting"))
                            keywords.Add("ENABLE_VOLUME_LIGHTING");
                    }
                    var rtpso = param.mainCaches.GetRTPSO(keywords,
                    rayTracingShader,
                    Path.GetFullPath(rayTracingShader.hlslFile, param.relativePath));

                    if (!graphicsContext.SetPSO(rtpso)) return false;
                    var CBVs = param.pass.CBVs;
                    var tpas = new RTTopLevelAcclerationStruct();
                    tpas.instances = new();
                    foreach (var renderer in renderers)
                    {
                        param.renderer = renderer;
                        foreach (var material in renderer.Materials)
                        {
                            param.material = material;
                            var psoDesc = param.GetPSODesc();
                            var btas = new RTBottomLevelAccelerationStruct();

                            btas.mesh = param.mesh;
                            btas.meshOverride = param.meshOverride;
                            btas.startIndex = material.indexOffset;
                            btas.indexCount = material.indexCount;
                            var inst = new RTInstance() { accelerationStruct = btas };
                            inst.transform = renderer.LocalToWorld;
                            inst.hitGroupName = "rayHit";
                            inst.SRVs = new();
                            inst.SRVs.Add(4, param.TextureFallBack(param.GetTex2D("_Albedo", material)));
                            inst.SRVs.Add(5, param.TextureFallBack(param.GetTex2D("_Emissive", material)));
                            inst.CBVs = new();
                            inst.CBVs.Add(0, param.GetCBVData(CBVs[1]));
                            tpas.instances.Add(inst);
                        }
                    }

                    Texture2D renderTarget = param.renderTargets[0];
                    int width = renderTarget.width;
                    int height = renderTarget.height;

                    RayTracingCall call = new RayTracingCall();
                    call.tpas = tpas;
                    call.UAVs = new();
                    param.SRVUAVs(param.pass.UAVs, call.UAVs);
                    call.SRVs = new();
                    param.SRVUAVs(param.pass.SRVs, call.SRVs, call.srvFlags);

                    call.CBVs = new();
                    call.CBVs.Add(0, param.GetCBVData(CBVs[0]));
                    call.missShaders = new[] { "miss" };

                    if ((bool)param.GetSettingsValue("UpdateGI"))
                    {
                        call.rayGenShader = "rayGenGI";
                        graphicsContext.DispatchRays(16, 16, 16, call);
                        param.SwapBuffer("GIBuffer", "GIBufferWrite");
                    }

                    call.rayGenShader = "rayGen";
                    graphicsContext.DispatchRays(width, height, 1, call);

                    foreach (var inst in tpas.instances)
                        inst.accelerationStruct.Dispose();
                    tpas.Dispose();
                }
                break;
            default:
                return false;
        }
        return true;
    }
}