using System;
using Coocoo3D.RenderPipeline;
using Coocoo3DGraphics;
using System.IO;
using System.Collections.Generic;
public static class UnionShaderBloom
{
    public static bool UnionShader(UnionShaderParam param)
    {
        var drp = param.rp.dynamicContextRead;
        if ((bool?)drp.GetSettingsValue("EnableBloom") != true) return true;
        var graphicsContext = param.graphicsContext;
        var mainCaches = param.rp.mainCaches;
        var psoDesc = param.PSODesc;
        //var pass = param.passSetting.RenderSequence.Find(u => u.Name == param.passName);
        //foreach (var cbvs in pass.Pass.CBVs)
        //{
        //    _WriteCBV(cbvs, param, cbvs.Index);
        //}

        var writer = param.GPUWriter;
        Texture2D renderTarget = param.renderTargets[0];
        writer.Write(renderTarget.GetWidth());
        writer.Write(renderTarget.GetHeight());
        writer.Write((float)drp.GetSettingsValue("BloomThreshold"));
        writer.Write((float)drp.GetSettingsValue("BloomIntensity"));
        writer.SetBufferImmediately(graphicsContext, false, 0);

        PSO pso1 = null;
        switch (param.passName)
        {
            case "BloomBlur1":
                {
                    List<string> keywords = new List<string>();
                    keywords.Add("BLOOM_1");
                    pso1 = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("Bloom.hlsl", param.relativePath));
                }
                break;
            case "BloomBlur2":
                {
                    List<string> keywords = new List<string>();
                    keywords.Add("BLOOM_2");
                    pso1 = mainCaches.GetPSOWithKeywords(keywords, Path.GetFullPath("Bloom.hlsl", param.relativePath));
                }
                break;
            default:
                return false;
        }
        if (param.settings.DebugRenderType == DebugRenderType.Bloom)
        {
            psoDesc.blendState = BlendState.None;
        }
        param.graphicsContext.SetPSO(pso1, psoDesc);
        graphicsContext.DrawIndexed(6, 0, 0);
        return true;
    }
    static void _WriteCBV(CBVSlotRes cbv, UnionShaderParam unionShaderParam, int slot)
    {
        if (cbv.Datas == null || cbv.Datas.Count == 0) return;
        var material = unionShaderParam.material;
        var context = unionShaderParam.rp;
        var writer = unionShaderParam.GPUWriter;
        var camera = unionShaderParam.visualChannel.cameraData;
        var settings = unionShaderParam.settings;
        foreach (var s in cbv.Datas)
        {
            switch (s)
            {
                case "DrawFlags":
                    writer.Write((int)material.DrawFlags);
                    break;
                case "DeltaTime":
                    writer.Write((float)context.dynamicContextRead.DeltaTime);
                    break;
                case "Time":
                    writer.Write((float)context.dynamicContextRead.Time);
                    break;
                case "World":
                    writer.Write(unionShaderParam.renderer.LocalToWorld);
                    break;
                case "CameraPosition":
                    writer.Write(camera.Position);
                    break;
                case "Camera":
                    writer.Write(camera.vpMatrix);
                    break;
                case "CameraInvert":
                    writer.Write(camera.pvMatrix);
                    break;
                case "WidthHeight":
                    {
                        var depthStencil = unionShaderParam.depthStencil;
                        var renderTargets = unionShaderParam.renderTargets;
                        if (renderTargets != null && renderTargets.Length > 0)
                        {
                            Texture2D renderTarget = renderTargets[0];
                            writer.Write(renderTarget.GetWidth());
                            writer.Write(renderTarget.GetHeight());
                        }
                        else if (depthStencil != null)
                        {
                            writer.Write(depthStencil.GetWidth());
                            writer.Write(depthStencil.GetHeight());
                        }
                        else
                        {
                            writer.Write(0);
                            writer.Write(0);
                        }
                    }
                    break;
                //case "DirectionalLight":
                //    if (lightings.Count > 0)
                //    {
                //        var lstruct = lightings[0].GetLStruct();

                //        writer.Write(lightCameraMatrix0);
                //        writer.Write(lstruct.PosOrDir);
                //        writer.Write((int)lstruct.Type);
                //        writer.Write(lstruct.Color);
                //    }
                //    else
                //    {
                //        writer.Write(Matrix4x4.Identity);
                //        writer.Write(new Vector4());
                //        writer.Write(new Vector4());
                //    }
                //    break;
                //case "PointLights4":
                //    {
                //        int count = Math.Min(pointLights.Count, 4);
                //        for (int pli = 0; pli < count; pli++)
                //        {
                //            var lstruct = pointLights[pli].GetLStruct();
                //            writer.Write(lstruct.PosOrDir);
                //            writer.Write((int)lstruct.Type);
                //            writer.Write(lstruct.Color);
                //        }
                //        for (int i = 0; i < 4 - count; i++)
                //        {
                //            writer.Write(new Vector4());
                //            writer.Write(new Vector4());
                //        }
                //    }
                //    break;
                case "IndirectMultiplier":
                    writer.Write(settings.SkyBoxLightMultiplier);
                    break;
            }
        }
        writer.SetBufferImmediately(unionShaderParam.graphicsContext, false, slot);
    }
}