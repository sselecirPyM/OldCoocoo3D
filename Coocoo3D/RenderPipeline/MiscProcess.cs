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
    public struct P_Env_Data
    {
        public TextureCube source;
        public RenderTextureCube IrradianceMap;
        public RenderTextureCube EnvMap;
        public int Level;
    }
    public class MiscProcessContext
    {
        public List<P_Env_Data> miscProcessPairs = new List<P_Env_Data>();
        public void Add(P_Env_Data pair)
        {
            lock (miscProcessPairs)
            {
                miscProcessPairs.Add(pair);
            }
        }

        public void MoveToAnother(MiscProcessContext context)
        {
            miscProcessPairs.MoveTo_CC(context.miscProcessPairs);
        }

        public void Clear()
        {
            miscProcessPairs.Clear();
        }
    }

    public class MiscProcess
    {
        const int c_bufferSize = 65536;
        const int c_splitSize = 256;
        public ComputePO IrradianceMap0 = new ComputePO();
        public ComputePO EnvironmentMap0 = new ComputePO();
        public ComputePO ClearIrradianceMap = new ComputePO();
        GraphicsSignature rootSignature = new GraphicsSignature();
        public bool Ready = false;
        public const int c_maxIteration = 32;
        public CBuffer constantBuffers = new CBuffer();
        XYZData _XyzData;
        public async Task ReloadAssets(DeviceResources deviceResources)
        {
            rootSignature.ReloadCompute(deviceResources, new GraphicSignatureDesc[]
            {
                GraphicSignatureDesc.CBV,
                GraphicSignatureDesc.CBV,
                GraphicSignatureDesc.SRVTable,
                GraphicSignatureDesc.UAVTable,
            });
            deviceResources.InitializeCBuffer(constantBuffers, c_bufferSize);
            IrradianceMap0.Initialize(deviceResources, rootSignature, await ReadFile("ms-appx:///Coocoo3DGraphics/G_IrradianceMap0.cso"));
            EnvironmentMap0.Initialize(deviceResources, rootSignature, await ReadFile("ms-appx:///Coocoo3DGraphics/G_PreFilterEnv.cso"));
            ClearIrradianceMap.Initialize(deviceResources, rootSignature, await ReadFile("ms-appx:///Coocoo3DGraphics/G_ClearIrradianceMap.cso"));

            Ready = true;
        }

        public void Process(RenderPipelineContext rp, MiscProcessContext context)
        {
            if (context.miscProcessPairs.Count == 0) return;
            if (!Ready) return;
            ref byte[] bigBuffer = ref rp.bigBuffer;
            GraphicsContext graphicsContext = rp.graphicsContext1;
            graphicsContext.BeginCommand();
            graphicsContext.SetDescriptorHeapDefault();
            for (int i = 0; i < context.miscProcessPairs.Count; i++)
            {
                var texture0 = context.miscProcessPairs[i].source;
                var texture1 = context.miscProcessPairs[i].IrradianceMap;
                var texture2 = context.miscProcessPairs[i].EnvMap;
                IntPtr ptr1 = Marshal.UnsafeAddrOfPinnedArrayElement(bigBuffer, 0);
                _XyzData.x1 = (int)texture1.m_width;
                _XyzData.y1 = (int)texture1.m_height;
                _XyzData.x2 = (int)texture2.m_width;
                _XyzData.y2 = (int)texture2.m_height;
                _XyzData.Quality = context.miscProcessPairs[i].Level;
                int itCount = context.miscProcessPairs[i].Level;

                for (int j = 0; j < itCount; j++)
                {
                    _XyzData.Batch = j;

                    Marshal.StructureToPtr(_XyzData, ptr1 + j * c_splitSize, true);
                }
                graphicsContext.UpdateResource(constantBuffers, bigBuffer, c_bufferSize, 0);

                graphicsContext.SetRootSignatureCompute(rootSignature);
                graphicsContext.SetComputeCBVR(constantBuffers, 0);
                graphicsContext.SetComputeSRVT(texture0, 2);
                graphicsContext.SetPObject(ClearIrradianceMap);

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
                graphicsContext.SetPObject(IrradianceMap0);

                pow2a = 1;
                for (int j = 0; j < texture1.m_mipLevels; j++)
                {
                    for (int k = 0; k < itCount; k++)
                    {
                        graphicsContext.SetComputeUAVT(texture1, j, 3);
                        graphicsContext.SetComputeCBVR(constantBuffers, k, 1, 0);
                        graphicsContext.Dispatch((int)(texture1.m_width + 7) / 8 / pow2a, (int)(texture1.m_height + 7) / 8 / pow2a, 6);
                    }
                    pow2a *= 2;
                }

                graphicsContext.SetComputeSRVT(texture0, 2);
                graphicsContext.SetPObject(EnvironmentMap0);
                pow2a = 1;
                for (int j = 0; j < texture2.m_mipLevels; j++)
                {
                    for (int k = 0; k < itCount; k++)
                    {
                        graphicsContext.SetComputeUAVT(texture2, j, 3);
                        graphicsContext.SetComputeCBVR(constantBuffers, k, 1, 0);
                        graphicsContext.SetComputeCBVR(constantBuffers, j, 1, 1);
                        graphicsContext.Dispatch((int)(texture2.m_width + 7) / 8 / pow2a, (int)(texture2.m_height + 7) / 8 / pow2a, 6);
                    }
                    pow2a *= 2;
                }
            }
            graphicsContext.EndCommand();
            graphicsContext.Execute();
            context.Clear();
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
