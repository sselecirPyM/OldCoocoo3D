using Coocoo3D.Core;
using Coocoo3D.Present;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public class PostProcess : RenderPipeline
    {
        public const int c_postProcessDataSize = 256;

        public InnerStruct innerStruct = new InnerStruct
        {
            GammaCorrection = 2.2f,
            Saturation1 = 1.0f,
            Threshold1 = 0.7f,
            Transition1 = 0.1f,
            Saturation2 = 1.0f,
            Threshold2 = 0.2f,
            Transition2 = 0.1f,
            Saturation3 = 1.0f,
            BackgroundFactor = 1.0f,
        };
        CBuffer postProcessDataBuffer = new CBuffer();

        public PostProcess()
        {
        }

        public void Reload(DeviceResources deviceResources)
        {
            deviceResources.InitializeCBuffer(postProcessDataBuffer, c_postProcessDataSize);
            Ready = true;
        }

        public override void PrepareRenderData(RenderPipelineContext context, GraphicsContext graphicsContext)
        {
            Marshal.StructureToPtr(innerStruct, Marshal.UnsafeAddrOfPinnedArrayElement(context.bigBuffer, 0), true);
            graphicsContext.UpdateResource(postProcessDataBuffer, context.bigBuffer, c_postProcessDataSize, 0);
        }

        public override void RenderCamera(RenderPipelineContext context, GraphicsContext graphicsContext)
        {
            var rsPostProcess = context.RPAssetsManager.rootSignaturePostProcess;
            graphicsContext.SetRootSignature(rsPostProcess);
            graphicsContext.SetRenderTargetScreen(context.dynamicContextRead.settings.backgroundColor, true);
            graphicsContext.SetCBVR(postProcessDataBuffer, 0);
            graphicsContext.SetSRVTSlot(context.outputRTV, 0);
            graphicsContext.SetMesh(context.ndcQuadMesh);
            PSODesc desc = new PSODesc
            {
                blendState = EBlendState.none,
                cullMode = ECullMode.back,
                depthBias = 0,
                slopeScaledDepthBias = 0,
                dsvFormat = DxgiFormat.DXGI_FORMAT_UNKNOWN,
                inputLayout = EInputLayout.postProcess,
                ptt = ED3D12PrimitiveTopologyType.TRIANGLE,
                rtvFormat = context.swapChainFormat,
                renderTargetCount = 1,
                streamOutput = false,
                wireFrame = false,
            };

            SetPipelineStateVariant(context.deviceResources, graphicsContext, rsPostProcess, ref desc, context.RPAssetsManager.PSOs["PostProcess"]);
            graphicsContext.DrawIndexed(context.ndcQuadMesh.GetIndexCount(), 0, 0);
        }

        public struct InnerStruct
        {
            public float GammaCorrection;
            public float Saturation1;
            public float Threshold1;
            public float Transition1;
            public float Saturation2;
            public float Threshold2;
            public float Transition2;
            public float Saturation3;
            public float BackgroundFactor;
        }

    }
}
