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
        public PostProcess()
        {
        }

        public void Reload(DeviceResources deviceResources)
        {
            Ready = true;
        }

        public override void PrepareRenderData(RenderPipelineContext context, GraphicsContext graphicsContext)
        {

        }

        public override void RenderCamera(RenderPipelineContext context, GraphicsContext graphicsContext)
        {
            var rsPostProcess = context.RPAssetsManager.GetRootSignature(context.deviceResources,"CCs");
            graphicsContext.SetRootSignature(rsPostProcess);
            graphicsContext.SetRenderTargetScreen(context.dynamicContextRead.settings.backgroundColor, true);
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
    }
}
