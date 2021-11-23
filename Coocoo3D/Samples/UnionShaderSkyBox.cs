using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
public static class UnionShaderSkyBox
{
    public static bool UnionShader(UnionShaderParam param)
    {
        var graphicsContext = param.graphicsContext;
        var psoDesc = param.PSODesc;
        SetPipelineStateVariant(param.rp.graphicsDevice, param.graphicsContext, param.rootSignature, psoDesc, param.PSO);
        graphicsContext.DrawIndexed(6, 0, 0);
        return true;
    }

    static void SetPipelineStateVariant(GraphicsDevice graphicsDevice, GraphicsContext graphicsContext, RootSignature graphicsSignature, in PSODesc desc, PSO pso)
    {
        int variant = pso.GetVariantIndex(graphicsDevice, graphicsSignature, desc);
        graphicsContext.SetPSO(pso, variant);
    }
}