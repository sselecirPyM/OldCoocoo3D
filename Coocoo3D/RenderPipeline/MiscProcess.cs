using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.RenderPipeline.Wrap;
using Coocoo3DGraphics;

namespace Coocoo3D.RenderPipeline
{
    public static class MiscProcess
    {
        public static void Process(RenderPipelineContext rp, GPUWriter gpuWriter)
        {
            int currentQuality = 0;
            if (rp.customData.TryGetValue("CurrentSkyBoxQuality", out object o1) && o1 is int a0)
                currentQuality = a0;

            if (rp.SkyBoxChanged || currentQuality < rp.dynamicContextRead.settings.SkyBoxMaxQuality)
            {
                var mainCaches = rp.mainCaches;
                GraphicsContext graphicsContext = rp.graphicsContext;

                Texture2D texOri = rp.mainCaches.GetTextureLoaded(rp.skyBoxOriTex, rp.graphicsContext);
                rp.mainCaches.GetSkyBox(rp.skyBoxName, rp.graphicsContext, out var texSkyBox, out var texReflect);
                int roughnessLevel = 5;

                var rootSignature = rp.mainCaches.GetRootSignature("Csu");

                graphicsContext.SetRootSignature(rootSignature);

                if (rp.SkyBoxChanged)
                {
                    rp.SkyBoxChanged = false;
                    currentQuality = 0;

                    graphicsContext.SetPSO(mainCaches.GetComputeShader("Shaders/G_ConvertToCube.hlsl"));
                    gpuWriter.Write(texSkyBox.width);
                    gpuWriter.Write(texSkyBox.height);
                    gpuWriter.SetBufferComputeImmediately(graphicsContext, true, 0);
                    graphicsContext.SetSRVTSlot(texOri, 0);
                    graphicsContext.SetUAVTSlot(texSkyBox, 0, 0);
                    graphicsContext.Dispatch((int)(texSkyBox.width + 7) / 8, (int)(texSkyBox.height + 7) / 8, 6);

                    int pow2a;
                    graphicsContext.SetPSO(mainCaches.GetComputeShader("Shaders/G_GenerateCubeMipMap.hlsl"));
                    for (int j = 1; j < texSkyBox.mipLevels; j++)
                    {
                        pow2a = 1 << j;
                        graphicsContext.SetSRVTLim(texSkyBox, j, 0);
                        graphicsContext.SetUAVTSlot(texSkyBox, j, 0);
                        gpuWriter.Write(texSkyBox.width / pow2a);
                        gpuWriter.Write(texSkyBox.height / pow2a);
                        gpuWriter.Write(j - 1);
                        gpuWriter.SetBufferComputeImmediately(graphicsContext, true, 0);
                        graphicsContext.Dispatch((int)(texSkyBox.width + 7) / 8 / pow2a, (int)(texSkyBox.height + 7) / 8 / pow2a, 6);
                    }

                    graphicsContext.SetSRVTSlot(texSkyBox, 0);
                    graphicsContext.SetPSO(mainCaches.GetComputeShader("Shaders/G_ClearTexture2DArray.hlsl"));

                    for (int j = 0; j < texReflect.mipLevels; j++)
                    {
                        pow2a = 1 << j;
                        graphicsContext.SetUAVTSlot(texReflect, j, 0);
                        graphicsContext.Dispatch((int)(texReflect.width + 7) / 8 / pow2a, (int)(texReflect.height + 7) / 8 / pow2a, 6);
                    }
                }
                {
                    int pow2a;
                    {
                        int j = currentQuality % (roughnessLevel + 1);
                        int quality = currentQuality / (roughnessLevel + 1);
                        if (j != roughnessLevel)
                            graphicsContext.SetPSO(mainCaches.GetComputeShader("Shaders/G_PreFilterEnv.hlsl"));
                        else
                            graphicsContext.SetPSO(mainCaches.GetComputeShader("Shaders/G_IrradianceMap0.hlsl"));
                        pow2a = 1 << j;
                        gpuWriter.Write(texReflect.width / pow2a);
                        gpuWriter.Write(texReflect.height / pow2a);
                        gpuWriter.Write(quality);//quality
                        gpuWriter.Write(quality);
                        gpuWriter.Write(j * j / (4.0f * 4.0f));
                        gpuWriter.SetBufferComputeImmediately(graphicsContext, true, 0);

                        graphicsContext.SetSRVTSlot(texSkyBox, 0);
                        graphicsContext.SetUAVTSlot(texReflect, j, 0);
                        graphicsContext.Dispatch((int)(texReflect.width + 7) / 8 / pow2a, (int)(texReflect.height + 7) / 8 / pow2a, 6);
                    }
                }
                rp.customData["CurrentSkyBoxQuality"] = currentQuality + 1;
            }
        }
    }
}
