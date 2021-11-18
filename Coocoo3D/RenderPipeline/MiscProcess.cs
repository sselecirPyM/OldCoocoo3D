using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Utility;
using Coocoo3D.RenderPipeline.Wrap;
using Coocoo3DGraphics;

namespace Coocoo3D.RenderPipeline
{
    public static class MiscProcess
    {
        public static void Process(RenderPipelineContext rp, GPUWriter gpuWriter)
        {
            if (rp.SkyBoxChanged)
            {
                var mainCaches = rp.mainCaches;
                GraphicsContext graphicsContext = rp.graphicsContext;
                //var texSkyBox = rp._GetTexCubeByName(rp.skyBoxName);
                //var texIrradiance = rp._GetTexCubeByName(rp.skyBoxName + "Irradiance");
                //var texReflect = rp._GetTexCubeByName(rp.skyBoxName + "Reflect");
                Texture2D texOri = rp.mainCaches.GetTexture1(rp.skyBoxOriTex, rp.graphicsContext);
                rp.mainCaches.GetSkyBox(rp.skyBoxName, rp.graphicsContext, out var texSkyBox, out var texIrradiance, out var texReflect);

                int itCount = 32;
                int[] offsets = new int[itCount+1];
                for (int j = 0; j < itCount; j++)
                {
                    offsets[j] = gpuWriter.BufferBegin();
                    gpuWriter.Write(texIrradiance.width);
                    gpuWriter.Write(texIrradiance.height);
                    gpuWriter.Write(32);
                    gpuWriter.Write(j);
                    gpuWriter.Write(texReflect.width);
                    gpuWriter.Write(texReflect.height);
                }
                offsets[itCount] = gpuWriter.BufferBegin();
                gpuWriter.Write(texSkyBox.width);
                gpuWriter.Write(texSkyBox.height);
                var buffer = gpuWriter.GetBuffer(rp.graphicsDevice, rp.graphicsContext, true);
                var rootSignature = rp.RPAssetsManager.GetRootSignature(rp.graphicsDevice, "CCsu");


                graphicsContext.SetRootSignatureCompute(rootSignature);
                graphicsContext.SetComputeCBVR(buffer, offsets[itCount] / 256, 0, 0);

                graphicsContext.SetPSO(mainCaches.GetComputeShader("Shaders/G_ConvertToCube.hlsl"));
                graphicsContext.SetComputeSRVT(texOri, 2);
                graphicsContext.SetComputeUAVT(texSkyBox, 0, 3);
                graphicsContext.Dispatch((int)(texSkyBox.width + 7) / 8, (int)(texSkyBox.height + 7) / 8, 6);

                graphicsContext.SetComputeCBVR(buffer, 0);
                graphicsContext.SetComputeSRVT(texSkyBox, 2);
                graphicsContext.SetPSO(mainCaches.GetComputeShader("Shaders/G_ClearTexture2DArray.hlsl"));

                int pow2a = 1;
                for (int j = 0; j < texIrradiance.mipLevels; j++)
                {
                    graphicsContext.SetComputeUAVT(texIrradiance, j, 3);
                    graphicsContext.Dispatch((int)(texIrradiance.width + 7) / 8 / pow2a, (int)(texIrradiance.height + 7) / 8 / pow2a, 6);
                    pow2a *= 2;
                }
                pow2a = 1;
                for (int j = 0; j < texReflect.mipLevels; j++)
                {
                    graphicsContext.SetComputeUAVT(texReflect, j, 3);
                    graphicsContext.Dispatch((int)(texReflect.width + 7) / 8 / pow2a, (int)(texReflect.height + 7) / 8 / pow2a, 6);
                    pow2a *= 2;
                }

                graphicsContext.SetPSO(mainCaches.GetComputeShader("Shaders/G_IrradianceMap0.hlsl"));
                pow2a = 1;
                for (int j = 0; j < texIrradiance.mipLevels; j++)
                {
                    for (int k = 0; k < itCount; k++)
                    {
                        graphicsContext.SetComputeUAVT(texIrradiance, j, 3);
                        graphicsContext.SetComputeCBVR(buffer, offsets[k] / 256, 1, 0);
                        graphicsContext.Dispatch((int)(texIrradiance.width + 7) / 8 / pow2a, (int)(texIrradiance.height + 7) / 8 / pow2a, 6);
                    }
                    pow2a *= 2;
                }

                graphicsContext.SetComputeSRVT(texSkyBox, 2);
                graphicsContext.SetPSO(mainCaches.GetComputeShader("Shaders/G_PreFilterEnv.hlsl"));
                pow2a = 1;
                for (int j = 0; j < texReflect.mipLevels; j++)
                {
                    for (int k = 0; k < itCount; k++)
                    {
                        graphicsContext.SetComputeUAVT(texReflect, j, 3);
                        graphicsContext.SetComputeCBVR(buffer, offsets[k] / 256, 1, 0);
                        graphicsContext.SetComputeCBVR(buffer, offsets[j] / 256, 1, 1);
                        graphicsContext.Dispatch((int)(texReflect.width + 7) / 8 / pow2a, (int)(texReflect.height + 7) / 8 / pow2a, 6);
                    }
                    pow2a *= 2;
                }
                rp.SkyBoxChanged = false;
            }
        }
    }
}
