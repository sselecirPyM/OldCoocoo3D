using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Coocoo3D.RenderPipeline
{
    public class MiscProcess
    {
        const int c_bufferSize = 65536;
        const int c_splitSize = 256;
        public ComputeShader IrradianceMap0 = new ComputeShader();
        public ComputeShader EnvironmentMap0 = new ComputeShader();
        public ComputeShader ClearIrradianceMap = new ComputeShader();
        GraphicsSignature rootSignature = new GraphicsSignature();
        public bool Ready = false;
        public const int c_maxIteration = 32;
        public CBuffer constantBuffer = new CBuffer();
        XYZData _XyzData;
        public async Task ReloadAssets(DeviceResources deviceResources)
        {
            rootSignature.Reload(deviceResources, new GraphicSignatureDesc[]
            {
                GraphicSignatureDesc.CBV,
                GraphicSignatureDesc.CBV,
                GraphicSignatureDesc.SRVTable,
                GraphicSignatureDesc.UAVTable,
            });
            deviceResources.InitializeCBuffer(constantBuffer, c_bufferSize);
            IrradianceMap0.Initialize(await ReadFile("ms-appx:///Coocoo3DGraphics/G_IrradianceMap0.cso"));
            EnvironmentMap0.Initialize(await ReadFile("ms-appx:///Coocoo3DGraphics/G_PreFilterEnv.cso"));
            ClearIrradianceMap.Initialize(await ReadFile("ms-appx:///Coocoo3DGraphics/G_ClearIrradianceMap.cso"));

            Ready = true;
        }

        public void Process(RenderPipelineContext rp)
        {
            if (!Ready) return;
            if (rp.SkyBoxChanged)
            {
                ref byte[] bigBuffer = ref rp.bigBuffer;
                GraphicsContext graphicsContext = rp.graphicsContext1;
                graphicsContext.BeginCommand();
                graphicsContext.SetDescriptorHeapDefault();
                var texture0 = rp.SkyBox;
                var texture1 = rp.IrradianceMap;
                var texture2 = rp.ReflectMap;
                _XyzData.x1 = (int)texture1.m_width;
                _XyzData.y1 = (int)texture1.m_height;
                _XyzData.x2 = (int)texture2.m_width;
                _XyzData.y2 = (int)texture2.m_height;
                _XyzData.Quality = 32;
                int itCount = 32;

                for (int j = 0; j < itCount; j++)
                {
                    _XyzData.Batch = j;
                    MemoryMarshal.Write<XYZData>(new Span<byte>(bigBuffer, j * c_splitSize, c_splitSize), ref _XyzData);
                }
                graphicsContext.UpdateResource(constantBuffer, bigBuffer, c_bufferSize, 0);

                graphicsContext.SetRootSignatureCompute(rootSignature);
                graphicsContext.SetComputeCBVR(constantBuffer, 0);
                graphicsContext.SetComputeSRVT(texture0, 2);
                graphicsContext.SetPSO(ClearIrradianceMap);

                int pow2a = 1;
                for (int j = 0; j < texture1.m_mipLevels; j++)
                {
                    graphicsContext.SetComputeUAVT(texture1, j, 3);
                    graphicsContext.Dispatch((int)(texture1.m_width + 7) / 8 / pow2a, (int)(texture1.m_height + 7) / 8 / pow2a, 6);
                    pow2a *= 2;
                }
                pow2a = 1;
                for (int j = 0; j < texture2.m_mipLevels; j++)
                {
                    graphicsContext.SetComputeUAVT(texture2, j, 3);
                    graphicsContext.Dispatch((int)(texture2.m_width + 7) / 8 / pow2a, (int)(texture2.m_height + 7) / 8 / pow2a, 6);
                    pow2a *= 2;
                }
                graphicsContext.SetPSO(IrradianceMap0);

                pow2a = 1;
                for (int j = 0; j < texture1.m_mipLevels; j++)
                {
                    for (int k = 0; k < itCount; k++)
                    {
                        graphicsContext.SetComputeUAVT(texture1, j, 3);
                        graphicsContext.SetComputeCBVR(constantBuffer, k, 1, 0);
                        graphicsContext.Dispatch((int)(texture1.m_width + 7) / 8 / pow2a, (int)(texture1.m_height + 7) / 8 / pow2a, 6);
                    }
                    pow2a *= 2;
                }

                graphicsContext.SetComputeSRVT(texture0, 2);
                graphicsContext.SetPSO(EnvironmentMap0);
                pow2a = 1;
                for (int j = 0; j < texture2.m_mipLevels; j++)
                {
                    for (int k = 0; k < itCount; k++)
                    {
                        graphicsContext.SetComputeUAVT(texture2, j, 3);
                        graphicsContext.SetComputeCBVR(constantBuffer, k, 1, 0);
                        graphicsContext.SetComputeCBVR(constantBuffer, j, 1, 1);
                        graphicsContext.Dispatch((int)(texture2.m_width + 7) / 8 / pow2a, (int)(texture2.m_height + 7) / 8 / pow2a, 6);
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

        protected async Task<IBuffer> ReadFile(string uri)
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(uri));
            return await FileIO.ReadBufferAsync(file);
        }
    }
}
