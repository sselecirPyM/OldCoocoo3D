using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Utility;
using Coocoo3DGraphics;

namespace Coocoo3D.RenderPipeline
{
    public class MiscProcess
    {
        const int c_bufferSize = 65536;
        const int c_splitSize = 256;
        bool Ready = false;

        public CBuffer constantBuffer = new CBuffer();
        public void Process(RenderPipelineContext rp)
        {
            if (!Ready)
            {
                Ready = true;
                rp.graphicsDevice.InitializeCBuffer(constantBuffer, c_bufferSize);
            }
            if (rp.SkyBoxChanged)
            {
                var mainCaches = rp.mainCaches;
                byte[] bigBuffer = MemUtil.MegaBuffer;
                GraphicsContext graphicsContext = rp.graphicsContext;
                graphicsContext.Begin();
                var texture0 = rp.SkyBox;
                var texture1 = rp.IrradianceMap;
                var texture2 = rp.ReflectMap;
                XYZData _XyzData;
                _XyzData.x1 = (int)texture1.width;
                _XyzData.y1 = (int)texture1.height;
                _XyzData.x2 = (int)texture2.width;
                _XyzData.y2 = (int)texture2.height;
                _XyzData.Quality = 32;
                int itCount = 32;

                for (int j = 0; j < itCount; j++)
                {
                    _XyzData.Batch = j;
                    MemoryMarshal.Write<XYZData>(new Span<byte>(bigBuffer, j * c_splitSize, c_splitSize), ref _XyzData);
                }
                graphicsContext.UpdateResource(constantBuffer, bigBuffer, c_bufferSize, 0);
                var rootSignature = rp.RPAssetsManager.GetRootSignature(rp.graphicsDevice, "CCsu");
                graphicsContext.SetRootSignatureCompute(rootSignature);
                graphicsContext.SetComputeCBVR(constantBuffer, 0);
                graphicsContext.SetComputeSRVT(texture0, 2);
                graphicsContext.SetPSO(mainCaches.GetComputeShader("Shaders/G_ClearIrradianceMap.hlsl"));

                int pow2a = 1;
                for (int j = 0; j < texture1.mipLevels; j++)
                {
                    graphicsContext.SetComputeUAVT(texture1, j, 3);
                    graphicsContext.Dispatch((int)(texture1.width + 7) / 8 / pow2a, (int)(texture1.height + 7) / 8 / pow2a, 6);
                    pow2a *= 2;
                }
                pow2a = 1;
                for (int j = 0; j < texture2.mipLevels; j++)
                {
                    graphicsContext.SetComputeUAVT(texture2, j, 3);
                    graphicsContext.Dispatch((int)(texture2.width + 7) / 8 / pow2a, (int)(texture2.height + 7) / 8 / pow2a, 6);
                    pow2a *= 2;
                }
                graphicsContext.SetPSO(mainCaches.GetComputeShader("Shaders/G_IrradianceMap0.hlsl"));

                pow2a = 1;
                for (int j = 0; j < texture1.mipLevels; j++)
                {
                    for (int k = 0; k < itCount; k++)
                    {
                        graphicsContext.SetComputeUAVT(texture1, j, 3);
                        graphicsContext.SetComputeCBVR(constantBuffer, k, 1, 0);
                        graphicsContext.Dispatch((int)(texture1.width + 7) / 8 / pow2a, (int)(texture1.height + 7) / 8 / pow2a, 6);
                    }
                    pow2a *= 2;
                }

                graphicsContext.SetComputeSRVT(texture0, 2);
                graphicsContext.SetPSO(mainCaches.GetComputeShader("Shaders/G_PreFilterEnv.hlsl"));
                pow2a = 1;
                for (int j = 0; j < texture2.mipLevels; j++)
                {
                    for (int k = 0; k < itCount; k++)
                    {
                        graphicsContext.SetComputeUAVT(texture2, j, 3);
                        graphicsContext.SetComputeCBVR(constantBuffer, k, 1, 0);
                        graphicsContext.SetComputeCBVR(constantBuffer, j, 1, 1);
                        graphicsContext.Dispatch((int)(texture2.width + 7) / 8 / pow2a, (int)(texture2.height + 7) / 8 / pow2a, 6);
                    }
                    pow2a *= 2;
                }
                graphicsContext.EndCommand();
                graphicsContext.Execute();
                rp.SkyBoxChanged = false;
            }
        }

        public struct XYZData
        {
            public int x1;
            public int y1;
            public int Quality;
            public int Batch;
            public int x2;
            public int y2;
        }
    }
}
