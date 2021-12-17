using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Coocoo3D.RenderPipeline
{
    public class PostProcess
    {
        public void RenderCamera(RenderPipelineContext context, VisualChannel visualChannel)
        {
            var rsPostProcess = context.mainCaches.GetRootSignature("CCs");
            var graphicsContext = visualChannel.graphicsContext;
            graphicsContext.SetRootSignature(rsPostProcess);
            graphicsContext.SetRTV(visualChannel.FinalOutput, System.Numerics.Vector4.Zero, true);
            graphicsContext.SetSRVTSlot(visualChannel.OutputRTV, 0);
            graphicsContext.SetMesh(context.ndcQuadMesh);
            PSODesc desc = new PSODesc
            {
                blendState = BlendState.none,
                cullMode = CullMode.Back,
                depthBias = 0,
                slopeScaledDepthBias = 0,
                dsvFormat = Format.Unknown,
                inputLayout = InputLayout.postProcess,
                primitiveTopologyType = PrimitiveTopologyType.Triangle,
                rtvFormat = context.swapChainFormat,
                renderTargetCount = 1,
                wireFrame = false,
            };
            graphicsContext.SetPSO(context.RPAssetsManager.PSOs["PostProcess"], desc);
            graphicsContext.DrawIndexed(context.ndcQuadMesh.GetIndexCount(), 0, 0);
        }
    }
}
