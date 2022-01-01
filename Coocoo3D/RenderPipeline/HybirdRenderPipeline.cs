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

            context.writerReuse.Clear();
            MiscProcess.Process(context, context.writerReuse);
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
            context.writerReuse.Clear();
            UnionShaderParam unionShaderParam = new UnionShaderParam()
            {
                rp = context,
                passSetting = passSetting,
                graphicsContext = graphicsContext,
                visualChannel = visualChannel,
                GPUWriter = context.writerReuse,
                settings = settings,
                relativePath = System.IO.Path.GetDirectoryName(passSetting.path),
                texLoading = texLoading,
                texError = texError,
                renderers = drp.renderers,
                directionalLights = drp.directionalLights,
                pointLights = drp.pointLights,
                mainCaches = mainCaches,
            };
            IPassDispatcher dispatcher;
            if (passSetting.Dispatcher != null)
            {
                dispatcher = mainCaches.GetPassDispatcher(passSetting.Dispatcher);
            }
            else
            {
                dispatcher = defaultPassDispatcher;
            }
            dispatcher?.Dispatch(unionShaderParam);

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
            var obj = unionShaderParam.GetSettingsValue(material, filter);
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

            var mainCaches = unionShaderParam.mainCaches;
            var settings = unionShaderParam.settings;
            var context = unionShaderParam.rp;
            var passSetting = unionShaderParam.passSetting;

            var texError = unionShaderParam.texError;
            var texLoading = unionShaderParam.texLoading;
            var renderers = unionShaderParam.renderers;

            RootSignature rootSignature = mainCaches.GetRootSignature(renderSequence.rootSignatureKey);
            unionShaderParam.pass = pass;
            unionShaderParam.passName = pass.Name;
            graphicsContext.SetRootSignature(rootSignature);

            Texture2D depthStencil = unionShaderParam.GetTex2D(renderSequence.DepthStencil);

            Texture2D[] renderTargets = null;
            if (renderSequence.RenderTargets == null || renderSequence.RenderTargets.Count == 0)
            {
                if (depthStencil != null)
                    graphicsContext.SetDSV(depthStencil, renderSequence.ClearDepth);
            }
            else
            {
                renderTargets = new Texture2D[renderSequence.RenderTargets.Count];
                for (int i = 0; i < renderSequence.RenderTargets.Count; i++)
                {
                    renderTargets[i] = unionShaderParam.GetTex2D(renderSequence.RenderTargets[i]);
                }
                graphicsContext.SetRTVDSV(renderTargets, depthStencil, Vector4.Zero, renderSequence.ClearRenderTarget, renderSequence.ClearDepth);
            }
            unionShaderParam.depthStencil = depthStencil;
            unionShaderParam.renderTargets = renderTargets;


            if (renderSequence.Type == null)
            {
                unionShaderParam.rayTracingShader = null;
                foreach (MMDRendererComponent renderer in renderers)
                {
                    graphicsContext.SetMesh(context.GetMesh(renderer.meshPath), context.meshOverride[renderer]);
                    unionShaderParam.renderer = renderer;

                    foreach (var material in renderer.Materials)
                    {
                        unionShaderParam.material = material;
                        if (!FilterObj(unionShaderParam, renderSequence.Filter))
                        {
                            continue;
                        }
                        bool? executed = mainCaches.GetUnionShader(passSetting.GetAliases(pass.UnionShader))?.Invoke(unionShaderParam);
                    }
                }

                //unionShaderParam.renderer = null;
                //unionShaderParam.material = null;
                //unionShaderParam.rayTracingShader = null;
                //bool? executed = mainCaches.GetUnionShader(passSetting.GetAliases(pass.UnionShader))?.Invoke(unionShaderParam);

            }
            else if (renderSequence.Type == "DrawScreen")
            {
                unionShaderParam.renderer = null;
                unionShaderParam.material = null;
                unionShaderParam.rayTracingShader = null;
                graphicsContext.SetMesh(context.quadMesh);

                bool? executed = mainCaches.GetUnionShader(passSetting.GetAliases(pass.UnionShader))?.Invoke(unionShaderParam);
            }
            else if (renderSequence.Type == "RayTracing")
            {
                if (context.graphicsDevice.IsRayTracingSupport())
                {
                    unionShaderParam.renderer = null;
                    unionShaderParam.material = null;
                    unionShaderParam.rayTracingShader = mainCaches.GetRayTracingShader(passSetting.GetAliases(pass.RayTracingShader));
                    UnionShader unionShader = mainCaches.GetUnionShader(passSetting.GetAliases(pass.UnionShader));
                    bool? executed = unionShader?.Invoke(unionShaderParam);
                }
                else
                {
                    Console.WriteLine(context.graphicsDevice.GetDeviceDescription() + " //this gpu is not support ray tracing.");
                }
            }
        }
    }
}