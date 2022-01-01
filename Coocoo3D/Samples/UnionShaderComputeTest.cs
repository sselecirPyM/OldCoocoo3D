using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.IO;
using System.Collections.Generic;
public static class UnionShaderComputeTest
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

        param.SetSRVs(param.pass.SRVs, material);
        param.SetUAVs(param.pass.UAVs, material);
        var computeShader = mainCaches.GetComputeShader(Path.GetFullPath("TestComputeShader.hlsl", param.relativePath));
        if (computeShader != null && graphicsContext.SetPSO(computeShader))
            graphicsContext.Dispatch(1, 1, 1);
        return true;
    }
}