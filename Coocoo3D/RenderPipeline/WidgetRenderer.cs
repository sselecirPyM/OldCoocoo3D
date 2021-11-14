using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.RenderPipeline.Wrap;
using Coocoo3D.Core;
using ImGuiNET;
using Coocoo3D.Utility;
using Vortice.Direct3D;
using Vortice.DXGI;
using Vortice.Direct3D12;

namespace Coocoo3D.RenderPipeline
{
    public class WidgetRenderer
    {
        public MMDMesh imguiMesh = new MMDMesh();
        GPUWriter GPUWriter = new GPUWriter();

        public void Reload(RenderPipelineContext context)
        {
            var caches = context.mainCaches;
            ImGui.SetCurrentContext(ImGui.CreateContext());
            Uploader uploader = new Uploader();
            var io = ImGui.GetIO();
            io.Fonts.AddFontFromFileTTF("c:\\Windows\\Fonts\\SIMHEI.ttf", 14, null, io.Fonts.GetGlyphRangesChineseFull());
            unsafe
            {
                byte* data;
                io.Fonts.GetTexDataAsRGBA32(out data, out int width, out int height, out int bytesPerPixel);
                int size = width * height * bytesPerPixel;
                Span<byte> spanByte1 = new Span<byte>(data, size);

                uploader.Texture2DRaw(spanByte1, Format.R8G8B8A8_UNorm, width, height);
            }
            var texture2D = new Texture2D();
            io.Fonts.TexID = caches.GetPtr("imgui_font");
            caches.SetTexture("imgui_font", texture2D);
            context.processingList.AddObject(texture2D, uploader);
            Ready = true;
        }

        public void Render(RenderPipelineContext context, GraphicsContext graphicsContext)
        {

            Texture2D texLoading = context.TextureLoading;
            Texture2D texError = context.TextureError;
            Texture2D _Tex(Texture2D _tex)
            {
                if (_tex == null)
                    return texError;
                else if (_tex is Texture2D _tex1)
                    return TextureStatusSelect(_tex1, texLoading, texError, texError);
                else
                    return _tex;
            };

            var rpAssets = context.RPAssetsManager;
            var caches = context.mainCaches;
            var rsPP = context.RPAssetsManager.GetRootSignature(context.graphicsDevice, "CCs");

            graphicsContext.SetRenderTargetScreen(context.dynamicContextRead.settings.backgroundColor, true);

            graphicsContext.SetRootSignature(rsPP);
            //graphicsContext.SetSRVTSlot(rpAssets.texture2ds["_UI1Texture"], 0);

            PSODesc desc;
            desc.blendState = BlendState.alpha;
            desc.cullMode = CullMode.None;
            desc.depthBias = 0;
            desc.slopeScaledDepthBias = 0;
            desc.dsvFormat = Format.Unknown;
            desc.inputLayout = InputLayout.postProcess;
            desc.ptt = PrimitiveTopologyType.Triangle;
            desc.rtvFormat = context.swapChainFormat;
            desc.renderTargetCount = 1;
            desc.streamOutput = false;
            desc.wireFrame = false;
            graphicsContext.SetMesh(context.ndcQuadMesh);

            //int renderCount = context.dynamicContextRead.selectedLightings.Count + context.dynamicContextRead.volumes.Count;
            int ofs = 0;

            var data = ImGui.GetDrawData();
            float L = data.DisplayPos.X;
            float R = data.DisplayPos.X + data.DisplaySize.X;
            float T = data.DisplayPos.Y;
            float B = data.DisplayPos.Y + data.DisplaySize.Y;
            float[] mvp =
            {
                    2.0f/(R-L),   0.0f,           0.0f,       0.0f,
                    0.0f,         2.0f/(T-B),     0.0f,       0.0f,
                    0.0f,         0.0f,           0.5f,       0.0f,
                    (R+L)/(L-R),  (T+B)/(B-T),    0.5f,       1.0f,
            };


            {
                GPUWriter.BufferBegin();
                for (int i = 0; i < mvp.Length; i++)
                {
                    GPUWriter.Write(mvp[i]);
                }
            }

            Vector2 clip_off = data.DisplayPos;

            desc.blendState = BlendState.alpha;
            desc.cullMode = CullMode.None;
            desc.depthBias = 0;
            desc.slopeScaledDepthBias = 0;
            desc.dsvFormat = Format.Unknown;
            desc.ptt = PrimitiveTopologyType.Triangle;
            desc.rtvFormat = context.swapChainFormat;
            desc.renderTargetCount = 1;
            desc.streamOutput = false;
            desc.wireFrame = false;
            desc.inputLayout = InputLayout.imgui;
            SetPipelineStateVariant(context.graphicsDevice, graphicsContext, rsPP, ref desc, rpAssets.PSOs["ImGui"]);
            var cbuffer = GPUWriter.GetBuffer(context.graphicsDevice, graphicsContext, false);
            graphicsContext.SetCBVRSlot(cbuffer, 0, 0, 0);
            ofs++;
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
                    imguiMesh.Reload1(vertexDatas, indexDatas, 20);
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

                        graphicsContext.SetSRVTSlot(tex, 0);
                        graphicsContext.RSSetScissorRect((int)(cmd.ClipRect.X - clip_off.X), (int)(cmd.ClipRect.Y - clip_off.Y), (int)(cmd.ClipRect.Z - clip_off.X), (int)(cmd.ClipRect.W - clip_off.Y));
                        graphicsContext.DrawIndexed((int)cmd.ElemCount, (int)(cmd.IdxOffset) + idxOfs, (int)(cmd.VtxOffset) + vtxOfs);
                    }
                    vtxOfs += cmdList.VtxBuffer.Size;
                    idxOfs += cmdList.IdxBuffer.Size;
                }

            }
        }

        public volatile bool Ready;

        protected Texture2D TextureStatusSelect(Texture2D texture, Texture2D loading, Texture2D unload, Texture2D error)
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

        protected void SetPipelineStateVariant(GraphicsDevice graphicsDevice, GraphicsContext graphicsContext, RootSignature graphicsSignature, ref PSODesc desc, PSO pso)
        {
            int variant = pso.GetVariantIndex(graphicsDevice, graphicsSignature, desc);
            graphicsContext.SetPSO(pso, variant);
        }
    }
}
