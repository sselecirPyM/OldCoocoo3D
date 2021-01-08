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

        byte[] rcDataUploadBuffer = new byte[c_postProcessDataSize];
        public GCHandle gch_rcDataUploadBuffer;

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
            BackgroundFactory = 1.0f,
        };
        public InnerStruct prevData;
        ConstantBufferStatic postProcessDataBuffer = new ConstantBufferStatic();

        public PostProcess()
        {
            gch_rcDataUploadBuffer = GCHandle.Alloc(rcDataUploadBuffer);
        }
        ~PostProcess()
        {
            gch_rcDataUploadBuffer.Free();
        }

        public void Reload(DeviceResources deviceResources)
        {
            postProcessDataBuffer.Reload(deviceResources, c_postProcessDataSize);
            Ready = true;
        }

        public override void PrepareRenderData(RenderPipelineContext context)
        {
            if (!innerStruct.Equals(prevData))
            {
                Marshal.StructureToPtr(innerStruct, Marshal.UnsafeAddrOfPinnedArrayElement(rcDataUploadBuffer, 0), true);
                context.graphicsContext.UpdateResource(postProcessDataBuffer, rcDataUploadBuffer, c_postProcessDataSize, 0);
                prevData = innerStruct;
            }
        }

        public override void RenderCamera(RenderPipelineContext context)
        {
            var graphicsContext = context.graphicsContext;
            graphicsContext.SetRootSignature(context.RPAssetsManager.rootSignaturePostProcess);
            graphicsContext.SetRenderTargetScreen(context.dynamicContextRead.settings.backgroundColor, context.ScreenSizeDSVs[1], true, true);
            graphicsContext.SetCBVR(postProcessDataBuffer, 0);
            graphicsContext.SetSRVT(context.outputRTV, 1);
            graphicsContext.SetSRVT(context.postProcessBackground, 2);
            graphicsContext.SetMesh(context.ndcQuadMesh);
            graphicsContext.SetPObject(context.RPAssetsManager.PObjectPostProcess, CullMode.back);
            graphicsContext.DrawIndexed(context.ndcQuadMeshIndexCount, 0, 0);
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
            public float BackgroundFactory;
        }

    }
}
