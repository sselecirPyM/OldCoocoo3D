﻿using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.RenderPipeline.Wrap;
using ImGuiNET;
using Vortice.DXGI;
using System.Buffers;
using Coocoo3D.Utility;

namespace Coocoo3D.RenderPipeline
{
    public class WidgetRenderer
    {
        GPUBuffer imguiMesh = new GPUBuffer();
        GPUWriter GPUWriter = new GPUWriter();
        string workDir = System.Environment.CurrentDirectory;
        string imguiShader;
        public WidgetRenderer()
        {
            imguiShader = System.IO.Path.GetFullPath("Shaders/ImGui.hlsl", workDir);
        }

        public void Render(RenderPipelineContext context, GraphicsContext graphicsContext)
        {
            var caches = context.mainCaches;

            Texture2D texLoading = caches.GetTextureLoaded("Assets/Textures/loading.png", graphicsContext);
            Texture2D texError = caches.GetTextureLoaded("Assets/Textures/error.png", graphicsContext);

            var rs = context.mainCaches.GetRootSignature("CCs");

            graphicsContext.SetRenderTargetSwapChain(context.swapChain, context.dynamicContextRead.settings.BackgroundColor, true);

            graphicsContext.SetRootSignature(rs);

            var data = ImGui.GetDrawData();
            if (data.CmdListsCount == 0) return;
            float L = data.DisplayPos.X;
            float R = data.DisplayPos.X + data.DisplaySize.X;
            float T = data.DisplayPos.Y;
            float B = data.DisplayPos.Y + data.DisplaySize.Y;

            Vector2 displayPosition = data.DisplayPos;


            PSODesc desc;
            desc.blendState = BlendState.alpha;
            desc.cullMode = CullMode.None;
            desc.depthBias = 0;
            desc.slopeScaledDepthBias = 0;
            desc.dsvFormat = Format.Unknown;
            desc.primitiveTopologyType = PrimitiveTopologyType.Triangle;
            desc.rtvFormat = context.swapChainFormat;
            desc.renderTargetCount = 1;
            desc.wireFrame = false;
            desc.inputLayout = InputLayout.imgui;
            var pso = caches.GetPSOWithKeywords(null, imguiShader);
            graphicsContext.SetPSO(pso, desc);
            Matrix4x4 matrix = new(
                2.0f / (R - L), 0.0f, 0.0f, (R + L) / (L - R),
                0.0f, 2.0f / (T - B), 0.0f, (T + B) / (B - T),
                0.0f, 0.0f, 0.5f, 0.5f,
                0.0f, 0.0f, 0.0f, 1.0f);

            GPUWriter.Write(matrix);
            GPUWriter.SetBufferImmediately(graphicsContext, false, 0);
            unsafe
            {
                int vertexSize = data.TotalVtxCount * sizeof(ImDrawVert);
                int indexSize = data.TotalIdxCount * sizeof(UInt16);
                var pool = ArrayPool<byte>.Shared;
                byte[] buffer = pool.Rent(vertexSize + indexSize);
                Span<byte> vertexDatas = new Span<byte>(buffer, 0, vertexSize);
                Span<byte> indexDatas = new Span<byte>(buffer, vertexSize, indexSize);

                var vertexWriter = new SpanWriter<byte>(vertexDatas);
                var indexWriter = new SpanWriter<byte>(indexDatas);
                for (int i = 0; i < data.CmdListsCount; i++)
                {
                    var cmdList = data.CmdListsRange[i];
                    var vertBytes = cmdList.VtxBuffer.Size * sizeof(ImDrawVert);
                    var indexBytes = cmdList.IdxBuffer.Size * sizeof(UInt16);
                    vertexWriter.Write(new Span<byte>(cmdList.VtxBuffer.Data.ToPointer(), vertBytes));
                    indexWriter.Write(new Span<byte>(cmdList.IdxBuffer.Data.ToPointer(), indexBytes));
                }
                graphicsContext.SetMesh(imguiMesh, vertexDatas,
                   indexDatas, data.TotalVtxCount, data.TotalIdxCount);
                pool.Return(buffer);
            }
            int vtxOfs = 0;
            int idxOfs = 0;
            for (int i = 0; i < data.CmdListsCount; i++)
            {
                var cmdList = data.CmdListsRange[i];


                for (int j = 0; j < cmdList.CmdBuffer.Size; j++)
                {
                    var cmd = cmdList.CmdBuffer[j];
                    Texture2D tex = caches.GetTexture(cmd.TextureId);

                    tex = TextureStatusSelect(tex, texLoading, texError, texError);

                    graphicsContext.SetSRVTSlotLinear(tex, 0);//srgb2srgb
                    var rect = cmd.ClipRect;
                    graphicsContext.RSSetScissorRect((int)(rect.X - displayPosition.X), (int)(rect.Y - displayPosition.Y), (int)(rect.Z - displayPosition.X), (int)(rect.W - displayPosition.Y));
                    graphicsContext.DrawIndexed((int)cmd.ElemCount, (int)(cmd.IdxOffset) + idxOfs, (int)(cmd.VtxOffset) + vtxOfs);
                }
                vtxOfs += cmdList.VtxBuffer.Size;
                idxOfs += cmdList.IdxBuffer.Size;
            }
        }

        Texture2D TextureStatusSelect(Texture2D texture, Texture2D loading, Texture2D unload, Texture2D error)
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
    }
}
