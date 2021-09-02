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

        public override void PrepareRenderData(RenderPipelineContext context, VisualChannel visualChannel)
        {

        }

        public override void RenderCamera(RenderPipelineContext context, VisualChannel visualChannel)
        {
            var rsPostProcess = context.RPAssetsManager.GetRootSignature(context.deviceResources,"CCs");
            var graphicsContext = visualChannel.graphicsContext;
            graphicsContext.SetRootSignature(rsPostProcess);
            graphicsContext.SetRTV(visualChannel.FinalOutput, System.Numerics.Vector4.Zero, true);
            graphicsContext.SetSRVTSlot(visualChannel.OutputRTV, 0);
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
            //graphicsContext.SetRenderTargetScreen(context.dynamicContextRead.settings.backgroundColor, true);
            //graphicsContext.DrawIndexed(context.ndcQuadMesh.GetIndexCount(), 0, 0);
        }
    }
}
