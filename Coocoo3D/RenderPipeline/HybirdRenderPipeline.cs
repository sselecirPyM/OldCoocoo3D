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
        public void BeginFrame(RenderPipelineContext context)
        {
            var mainCaches = context.mainCaches;
            while(mainCaches.TextureReadyToUpload.TryDequeue(out var uploadPack))
                context.graphicsContext.UploadTexture(uploadPack.Item1, uploadPack.Item2);
            while(mainCaches.MeshReadyToUpload.TryDequeue(out var mesh))
                context.graphicsContext.UploadMesh(mesh);

            var passSetting = context.dynamicContextRead.currentPassSetting;
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

            //Texture2D texLoading = mainCaches.GetTexture("Assets/Textures/loading.png");
            //Texture2D texError = mainCaches.GetTexture("Assets/Textures/error.png");
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
                //texLoading = texLoading,
                //texError = texError,
                directionalLights = drp.directionalLights,
                pointLights = drp.pointLights,
                mainCaches = mainCaches,
            };
            IPassDispatcher dispatcher = null;
            if (passSetting.Dispatcher != null)
                dispatcher = mainCaches.GetPassDispatcher(passSetting.Dispatcher);
            dispatcher?.Dispatch(unionShaderParam);

        }

        public static void DispatchPass(UnionShaderParam param)
        {
            var renderSequence = param.renderSequence;
            var pass = param.passSetting.Passes[renderSequence.Name];

            var graphicsContext = param.graphicsContext;

            var mainCaches = param.mainCaches;
            var context = param.rp;
            var passSetting = param.passSetting;

            RootSignature rootSignature = mainCaches.GetRootSignature(renderSequence.rootSignatureKey);
            param.pass = pass;
            param.passName = pass.Name;
            graphicsContext.SetRootSignature(rootSignature);

            Texture2D depthStencil = param.GetTex2D(renderSequence.DepthStencil);

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
                    renderTargets[i] = param.GetTex2D(renderSequence.RenderTargets[i]);
                }
                graphicsContext.SetRTVDSV(renderTargets, depthStencil, Vector4.Zero, renderSequence.ClearRenderTarget, renderSequence.ClearDepth);
            }
            param.depthStencil = depthStencil;
            param.renderTargets = renderTargets;


            if (renderSequence.Type == null)
            {
                param.renderer = null;
                param.material = null;
                param.rayTracingShader = null;
                bool? executed = mainCaches.GetUnionShader(passSetting.GetAliases(pass.UnionShader))?.Invoke(param);
            }
            else if (renderSequence.Type == "DrawScreen")
            {
                param.renderer = null;
                param.material = null;
                param.rayTracingShader = null;
                graphicsContext.SetMesh(context.quadMesh);

                bool? executed = mainCaches.GetUnionShader(passSetting.GetAliases(pass.UnionShader))?.Invoke(param);
            }
            else if (renderSequence.Type == "RayTracing")
            {
                if (context.graphicsDevice.IsRayTracingSupport())
                {
                    param.renderer = null;
                    param.material = null;
                    param.rayTracingShader = mainCaches.GetRayTracingShader(passSetting.GetAliases(pass.RayTracingShader));
                    bool? executed = mainCaches.GetUnionShader(passSetting.GetAliases(pass.UnionShader))?.Invoke(param);
                }
                else
                {
                    Console.WriteLine(context.graphicsDevice.GetDeviceDescription() + " //this gpu does not support ray tracing.");
                }
            }
        }
    }
}