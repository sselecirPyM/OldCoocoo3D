using Coocoo3D.Components;
using Coocoo3D.Numerics;
using Coocoo3D.Present;
using Coocoo3DGraphics;
using Coocoo3D.RenderPipeline.Wrap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Coocoo3D.RenderPipeline
{
    public class HybirdRenderPipeline
    {
        DefaultPassDispatcher defaultPassDispatcher = new DefaultPassDispatcher();
        public void BeginFrame(RenderPipelineContext context)
        {
            var drp = context.dynamicContextRead;
            var passSetting = drp.currentPassSetting;
            var mainCaches = context.mainCaches;
            var dispatcher = mainCaches.GetPassDispatcher(passSetting.Dispatcher);
            dispatcher?.FrameBegin(context);

            MiscProcess.Process(context, new GPUWriter());
        }
        public void EndFrame(RenderPipelineContext context)
        {
            var drp = context.dynamicContextRead;
            var passSetting = drp.currentPassSetting;
            var mainCaches = context.mainCaches;
            var dispatcher = mainCaches.GetPassDispatcher(passSetting.Dispatcher);
            dispatcher?.FrameEnd(context);

        }
        public void RenderCamera(RenderPipelineContext context, VisualChannel visualChannel)
        {
            var graphicsContext = visualChannel.graphicsContext;
            var drp = context.dynamicContextRead;
            var settings = drp.settings;
            var mainCaches = context.mainCaches;
            var passSetting = drp.currentPassSetting;

            Texture2D texLoading = mainCaches.GetTexture("Assets/Textures/loading.png");
            Texture2D texError = mainCaches.GetTexture("Assets/Textures/error.png");

            UnionShaderParam unionShaderParam = new UnionShaderParam()
            {
                rp = context,
                passSetting = passSetting,
                graphicsContext = graphicsContext,
                visualChannel = visualChannel,
                GPUWriter = new GPUWriter(),
                settings = settings,
                relativePath = System.IO.Path.GetDirectoryName(passSetting.path),
                texLoading = texLoading,
                texError = texError,
                renderers = drp.renderers,
            };

            var dispatcher = mainCaches.GetPassDispatcher(passSetting.Dispatcher) ?? defaultPassDispatcher;
            dispatcher.Dispatch(unionShaderParam);

        }
        static bool FilterObj(UnionShaderParam unionShaderParam, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            var material = unionShaderParam.material;
            if (filter == "Transparent")
                return material.Transparent;
            if (filter == "Opaque")
                return !material.Transparent;
            if (material.textures.ContainsKey(filter))
                return true;
            var obj = unionShaderParam.rp.dynamicContextRead.GetSettingsValue(material, filter);
            if (obj is bool b1 && b1)
                return true;
            return false;
        }

        public static void DispatchPass(UnionShaderParam unionShaderParam)
        {
            var visualChannel = unionShaderParam.visualChannel;
            var renderSequence = unionShaderParam.renderSequence;
            var pass = unionShaderParam.passSetting.Passes[renderSequence.Name];

            var graphicsContext = unionShaderParam.graphicsContext;
            var drp = unionShaderParam.rp.dynamicContextRead;
            var mainCaches = unionShaderParam.rp.mainCaches;
            var settings = unionShaderParam.settings;
            var context = unionShaderParam.rp;
            var passSetting = unionShaderParam.passSetting;

            var texError = unionShaderParam.texError;
            var texLoading = unionShaderParam.texLoading;
            var renderers = unionShaderParam.renderers;


            RootSignature rootSignature = mainCaches.GetRootSignature(renderSequence.rootSignatureKey);
            unionShaderParam.rootSignature = rootSignature;
            unionShaderParam.passName = pass.Name;
            graphicsContext.SetRootSignature(rootSignature);

            Texture2D depthStencil = unionShaderParam.GetTex2D(renderSequence.DepthStencil);

            PSODesc passPsoDesc;
            Texture2D[] renderTargets = null;
            if (renderSequence.RenderTargets == null || renderSequence.RenderTargets.Count == 0)
            {
                graphicsContext.SetDSV(depthStencil, renderSequence.ClearDepth);
                passPsoDesc.rtvFormat = Format.Unknown;
            }
            else
            {
                renderTargets = new Texture2D[renderSequence.RenderTargets.Count];
                for (int i = 0; i < renderSequence.RenderTargets.Count; i++)
                {
                    renderTargets[i] = unionShaderParam.GetTex2D(renderSequence.RenderTargets[i]);
                }
                graphicsContext.SetRTVDSV(renderTargets, depthStencil, Vector4.Zero, renderSequence.ClearRenderTarget, renderSequence.ClearDepth);
                passPsoDesc.rtvFormat = renderTargets[0].GetFormat();
            }
            unionShaderParam.depthStencil = depthStencil;
            unionShaderParam.renderTargets = renderTargets;

            passPsoDesc.blendState = pass.BlendMode;
            passPsoDesc.cullMode = renderSequence.CullMode;
            passPsoDesc.depthBias = renderSequence.DepthBias;
            passPsoDesc.slopeScaledDepthBias = renderSequence.SlopeScaledDepthBias;
            passPsoDesc.dsvFormat = depthStencil == null ? Format.Unknown : depthStencil.GetFormat();
            passPsoDesc.primitiveTopologyType = PrimitiveTopologyType.Triangle;
            passPsoDesc.renderTargetCount = renderSequence.RenderTargets == null ? 0 : renderSequence.RenderTargets.Count;
            passPsoDesc.wireFrame = false;

            if (renderSequence.Type == null)
            {
                passPsoDesc.inputLayout = InputLayout.mmd;
                passPsoDesc.wireFrame = drp.settings.Wireframe;

                for (int i = 0; i < renderers.Count; i++)
                {
                    MMDRendererComponent rendererComponent = renderers[i];
                    graphicsContext.SetMesh(context.GetMesh(rendererComponent.meshPath));
                    graphicsContext.SetMeshVertex(context.meshOverride[rendererComponent]);
                    unionShaderParam.PSODesc = passPsoDesc;
                    unionShaderParam.renderer = rendererComponent;
                    var Materials = rendererComponent.Materials;
                    foreach (var material in Materials)
                    {
                        unionShaderParam.material = material;
                        if (!FilterObj(unionShaderParam, renderSequence.Filter))
                        {
                            continue;
                        }
                        foreach (var cbv in pass.CBVs)
                        {
                            WriteCBV(cbv, unionShaderParam);
                        }
                        _SetSRVs(pass.SRVs, material);
                        if (renderSequence.CullMode == 0)
                            passPsoDesc.cullMode = material.DrawFlags.HasFlag(DrawFlag.DrawDoubleFace) ? CullMode.None : CullMode.Back;

                        bool? executed = mainCaches.GetUnionShader(passSetting.GetAliases(material.unionShader))?.Invoke(unionShaderParam);
                        if (executed != true)
                        {
                            executed = mainCaches.GetUnionShader(passSetting.GetAliases(pass.UnionShader))?.Invoke(unionShaderParam);
                        }
                        if (executed != true && renderSequence.PSODefault.Status == GraphicsObjectStatus.loaded)
                        {
                            graphicsContext.SetPSO(renderSequence.PSODefault, passPsoDesc);
                            graphicsContext.DrawIndexed(material.indexCount, material.indexOffset, 0);
                        }
                    }
                }
            }
            else if (renderSequence.Type == "DrawScreen")
            {
                _SetSRVs(pass.SRVs);
                passPsoDesc.inputLayout = InputLayout.postProcess;
                unionShaderParam.PSODesc = passPsoDesc;
                unionShaderParam.renderer = null;
                unionShaderParam.material = null;
                foreach (var cbv in pass.CBVs)
                {
                    WriteCBV(cbv, unionShaderParam);
                }

                graphicsContext.SetMesh(context.ndcQuadMesh);

                UnionShader unionShader = mainCaches.GetUnionShader(passSetting.GetAliases(pass.UnionShader));

                bool? a = unionShader?.Invoke(unionShaderParam);
                if (a != true && renderSequence.PSODefault.Status == GraphicsObjectStatus.loaded)
                {
                    graphicsContext.SetPSO(renderSequence.PSODefault, passPsoDesc);
                    graphicsContext.DrawIndexed(context.ndcQuadMesh.GetIndexCount(), 0, 0);
                }
            }

            void _SetSRVs(List<SlotRes> SRVs, RuntimeMaterial material = null)
            {
                if (SRVs != null)
                    foreach (var resd in SRVs)
                    {
                        if (resd.ResourceType == "TextureCube")
                        {
                            graphicsContext.SetSRVTSlot(context._GetTexCubeByName(resd.Resource), resd.Index);
                        }
                        else if (resd.ResourceType == "Texture2D")
                        {
                            graphicsContext.SetSRVTSlot(_Tex(unionShaderParam.GetTex2D(resd.Resource, material)), resd.Index);
                        }
                    }
            }

            Texture2D _Tex(Texture2D _tex) => TextureStatusSelect(_tex, texLoading, texError, texError);
        }

        static Texture2D TextureStatusSelect(Texture2D texture, Texture2D loading, Texture2D unload, Texture2D error)
        {
            if (texture == null) return error;
            if (texture.Status == GraphicsObjectStatus.loaded)
                return texture;
            else if (texture.Status == GraphicsObjectStatus.loading)
                return loading;
            else if (texture.Status == GraphicsObjectStatus.unload)
                return unload;
            else
                return error;
        }

        public static void WriteCBV(SlotRes cbv, UnionShaderParam unionShaderParam)
        {
            var writer = unionShaderParam.GPUWriter;
            unionShaderParam.WriteGPU(cbv.Datas, writer);
            writer.SetBufferImmediately(unionShaderParam.graphicsContext, false, cbv.Index);
        }
    }
}