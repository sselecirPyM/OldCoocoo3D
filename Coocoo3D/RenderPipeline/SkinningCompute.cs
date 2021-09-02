using Coocoo3D.Components;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public class SkinningCompute
    {

        public static void Process(RenderPipelineContext context)
        {
            var rpAssets = context.RPAssetsManager;
            var deviceResources = context.deviceResources;
            var rendererComponents = context.dynamicContextRead.renderers;

            PSO PSOSkinning = rpAssets.PSOs["PSOMMDSkinning"];
            var graphicsContext = context.graphicsContext;

            graphicsContext.SetRootSignature(rpAssets.rootSignatureSkinning);
            graphicsContext.SetSOMesh(context.SkinningMeshBuffer);
            void EntitySkinning(MMDRendererComponent rendererComponent, CBuffer entityBoneDataBuffer)
            {
                var Materials = rendererComponent.Materials;
                graphicsContext.SetCBVRSlot(entityBoneDataBuffer, 0, 0, 0);
                rendererComponent.shaders.TryGetValue("Skinning", out var shaderSkinning);
                var psoSkinning = PSOSkinning;
                SetPipelineStateVariant(deviceResources, graphicsContext, rpAssets.rootSignatureSkinning, ref context.SkinningDesc, psoSkinning);
                graphicsContext.SetMeshVertex(context.GetMesh(rendererComponent.meshPath));
                graphicsContext.SetMeshVertex(rendererComponent.meshAppend);
                graphicsContext.Draw(rendererComponent.meshVertexCount, 0);
            }
            for (int i = 0; i < rendererComponents.Count; i++)
                EntitySkinning(rendererComponents[i], context.CBs_Bone[i]);
            graphicsContext.SetSOMeshNone();
        }

        private static void SetPipelineStateVariant(DeviceResources deviceResources, GraphicsContext graphicsContext, GraphicsSignature graphicsSignature, ref PSODesc desc, PSO pso)
        {
            int variant = pso.GetVariantIndex(deviceResources, graphicsSignature, desc);
            graphicsContext.SetPSO(pso, variant);
        }
    }
}
