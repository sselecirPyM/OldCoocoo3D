using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.RenderPipeline.Wrap;
using ImGuiNET;
using Coocoo3D.Utility;
using Vortice.DXGI;
using Vortice.Direct3D12;

namespace Coocoo3D.RenderPipeline
{
    public class WidgetRenderer
    {
        public Mesh imguiMesh = new Mesh();
        GPUWriter GPUWriter = new GPUWriter();

        public void Render(RenderPipelineContext context, GraphicsContext graphicsContext)
        {
            var caches = context.mainCaches;

            Texture2D texLoading = caches.GetTextureLoaded("Assets/Textures/loading.png", graphicsContext);
            Texture2D texError = caches.GetTextureLoaded("Assets/Textures/error.png", graphicsContext);
            Texture2D _Tex(Texture2D _tex)
            {
                return TextureStatusSelect(_tex, texLoading, texError, texError);
            }

            var rs = context.mainCaches.GetRootSignature("CCs");

            graphicsContext.SetRenderTargetScreen(context.dynamicContextRead.settings.BackgroundColor, true);

            graphicsContext.SetRootSignature(rs);

            PSODesc desc;

            var data = ImGui.GetDrawData();
            float L = data.DisplayPos.X;
            float R = data.DisplayPos.X + data.DisplaySize.X;
            float T = data.DisplayPos.Y;
            float B = data.DisplayPos.Y + data.DisplaySize.Y;

            Vector2 clip_off = data.DisplayPos;

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
            var pso = caches.GetPSOWithKeywords(null, System.IO.Path.GetFullPath("Shaders/ImGui.hlsl"));
            graphicsContext.SetPSO(pso, desc);
            Matrix4x4 matrix = new Matrix4x4(
                2.0f / (R - L), 0.0f, 0.0f, (R + L) / (L - R),
                0.0f, 2.0f / (T - B), 0.0f, (T + B) / (B - T),
                0.0f, 0.0f, 0.5f, 0.5f,
                0.0f, 0.0f, 0.0f, 1.0f);

            GPUWriter.Write(matrix);
            GPUWriter.SetBufferImmediately(graphicsContext, false, 0);
            if (data.CmdListsCount > 0)
            {
                unsafe
                {
                    byte[] vertexDatas = new byte[data.TotalVtxCount * sizeof(ImDrawVert)];
                    byte[] indexDatas = new byte[data.TotalIdxCount * sizeof(UInt16)];

                    int vtxByteOfs = 0;
                    int idxByteOfs = 0;
                    for (int i = 0; i < data.CmdListsCount; i++)
                    {
                        var cmdList = data.CmdListsRange[i];
                        var vertBytes = cmdList.VtxBuffer.Size * sizeof(ImDrawVert);
                        var indexBytes = cmdList.IdxBuffer.Size * sizeof(UInt16);
                        new Span<byte>(cmdList.VtxBuffer.Data.ToPointer(), vertBytes).CopyTo(new Span<byte>(vertexDatas, vtxByteOfs, vertBytes));
                        new Span<byte>(cmdList.IdxBuffer.Data.ToPointer(), indexBytes).CopyTo(new Span<byte>(indexDatas, idxByteOfs, indexBytes));
                        vtxByteOfs += vertBytes;
                        idxByteOfs += indexBytes;
                    }
                    imguiMesh.ReloadDontCopy(vertexDatas, indexDatas, 20);
                    graphicsContext.UploadMesh(imguiMesh);
                }
                int vtxOfs = 0;
                int idxOfs = 0;
                for (int i = 0; i < data.CmdListsCount; i++)
                {
                    var cmdList = data.CmdListsRange[i];

                    imguiMesh.SetIndexFormat(Format.R16_UInt);
                    graphicsContext.SetMesh(imguiMesh);

                    for (int j = 0; j < cmdList.CmdBuffer.Size; j++)
                    {
                        var cmd = cmdList.CmdBuffer[j];
                        Texture2D tex = caches.GetTexture(cmd.TextureId);

                        tex = _Tex(tex);

                        graphicsContext.SetSRVTSlotLinear(tex, 0);//srgb2srgb
                        graphicsContext.RSSetScissorRect((int)(cmd.ClipRect.X - clip_off.X), (int)(cmd.ClipRect.Y - clip_off.Y), (int)(cmd.ClipRect.Z - clip_off.X), (int)(cmd.ClipRect.W - clip_off.Y));
                        graphicsContext.DrawIndexed((int)cmd.ElemCount, (int)(cmd.IdxOffset) + idxOfs, (int)(cmd.VtxOffset) + vtxOfs);
                    }
                    vtxOfs += cmdList.VtxBuffer.Size;
                    idxOfs += cmdList.IdxBuffer.Size;
                }

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
