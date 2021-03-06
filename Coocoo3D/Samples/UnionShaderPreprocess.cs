using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.IO;
using System.Collections.Generic;
public static class UnionShaderPreprocess
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
        var brdfTex = param.GetTex2D("_BRDFLUT");
        if (param.GetPersistentValue("preprocess_brdflut", 0) == brdfTex.width)
        {
            return true;
        }
        else
        {
            param.SetPersistentValue("preprocess_brdflut", brdfTex.width);
        }
        var mainCaches = param.mainCaches;
        PSO pso = null;
        var material = param.material;
        var graphicsContext = param.graphicsContext;
        var psoDesc = param.GetPSODesc();

        var directionalLights = param.directionalLights;
        var pointLights = param.pointLights;

        param.SetSRVs(param.pass.SRVs, material);
        param.SetUAVs(param.pass.UAVs, material);

        var writer = param.GPUWriter;
        writer.Write(brdfTex.width);
        writer.Write(brdfTex.height);
        writer.SetBufferImmediately(graphicsContext, false, 0);

        var computeShader = mainCaches.GetComputeShader(Path.GetFullPath("BRDFLUT.hlsl", param.relativePath));
        if (computeShader != null && graphicsContext.SetPSO(computeShader))
            graphicsContext.Dispatch(brdfTex.width / 8, brdfTex.height / 8, 1);
        return true;
    }
}