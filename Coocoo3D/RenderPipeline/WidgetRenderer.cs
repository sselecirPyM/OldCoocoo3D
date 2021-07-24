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

namespace Coocoo3D.RenderPipeline
{
    public class WidgetRenderer : RenderPipeline
    {
        const int c_bufferSize = 256;
        const int c_bigBufferSize = 65536;
        public CBuffer bigConstantBuffer = new CBuffer();
        CBufferGroup CBufferGroup = new CBufferGroup();
        public MMDMesh imguiMesh = new MMDMesh();

        public WidgetRenderer()
        {
        }
        public void Reload(RenderPipelineContext context)
        {
            var deviceResources = context.deviceResources;
            deviceResources.InitializeCBuffer(bigConstantBuffer, c_bigBufferSize);
            CBufferGroup.Reload(deviceResources, 256, 65536);
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
                byte[] pixelData = new byte[size];
                spanByte1.CopyTo(pixelData);

                uploader.Texture2DRaw(pixelData, DxgiFormat.DXGI_FORMAT_R8G8B8A8_UNORM, width, height);
            }
            var texture2D = new Texture2D();
            context.RPAssetsManager.texture2ds["imgui_font"] = texture2D;
            io.Fonts.TexID = new IntPtr(1);
            context.RPAssetsManager.ptr2string[io.Fonts.TexID] = "imgui_font";
            context.processingList.AddObject(new ResourceWarp.Texture2DUploadPack(texture2D, uploader));
            Ready = true;
        }

        struct _Data
        {
            public Vector2 size;
            public Vector2 offset;
            public Vector2 uvSize;
            public Vector2 uvOffset;
        }
        readonly Vector2 c_buttonSize = new Vector2(64, 64);

        int indexOfSelectedEntity;
        public override void PrepareRenderData(RenderPipelineContext context, GraphicsContext graphicsContext)
        {
            if (!context.dynamicContextRead.settings.ViewerUI) return;

            var cam = context.dynamicContextRead.cameras[0];

            var selectedLightings = context.dynamicContextRead.selectedLightings;

            var tvp = Matrix4x4.Transpose(cam.vpMatrix);
            MemoryMarshal.Write(new Span<byte>(context.bigBuffer, 0, 64), ref tvp);

            CBufferGroup.SetSlienceCount(selectedLightings.Count + context.dynamicContextRead.volumes.Count + 1);


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


            int matC = 0;
            foreach (var lighting in selectedLightings)
            {
                int ofs = 0;
                ofs += CooUtility.Write(context.bigBuffer, ofs, Matrix4x4.Transpose(Matrix4x4.CreateScale(lighting.Range) * Matrix4x4.CreateTranslation(lighting.Position) * cam.vpMatrix));
                CBufferGroup.UpdateSlience(graphicsContext, context.bigBuffer, 0, 256, matC);
                matC++;
            }
            foreach (var volume in context.dynamicContextRead.volumes)
            {
                int ofs = 0;
                ofs += CooUtility.Write(context.bigBuffer, ofs, Matrix4x4.Transpose(Matrix4x4.CreateScale(volume.Size) * Matrix4x4.CreateTranslation(volume.Position) * cam.vpMatrix));
                CBufferGroup.UpdateSlience(graphicsContext, context.bigBuffer, 0, 256, matC);
                matC++;
            }
            {
                int ofs = 0;
                for (int i = 0; i < mvp.Length; i++)
                {
                    ofs += CooUtility.Write(context.bigBuffer, ofs, mvp[i]);
                }
                CBufferGroup.UpdateSlience(graphicsContext, context.bigBuffer, 0, 256, matC);
                matC++;
            }
            CBufferGroup.UpdateSlienceComplete(graphicsContext);
        }

        public override void RenderCamera(RenderPipelineContext context, GraphicsContext graphicsContext)
        {

            if (!context.dynamicContextRead.settings.ViewerUI) return;
            var rpAssets = context.RPAssetsManager;
            var rsPP = context.RPAssetsManager.GetRootSignature(context.deviceResources, "CCs"); ;
            graphicsContext.SetRootSignature(rsPP);
            graphicsContext.SetSRVTSlot(rpAssets.texture2ds["_UI1Texture"], 0);

            PSODesc desc;
            desc.blendState = EBlendState.alpha;
            desc.cullMode = ECullMode.none;
            desc.depthBias = 0;
            desc.slopeScaledDepthBias = 0;
            desc.dsvFormat = DxgiFormat.DXGI_FORMAT_UNKNOWN;
            desc.inputLayout = EInputLayout.postProcess;
            desc.ptt = ED3D12PrimitiveTopologyType.TRIANGLE;
            desc.rtvFormat = context.swapChainFormat;
            desc.renderTargetCount = 1;
            desc.streamOutput = false;
            desc.wireFrame = false;
            graphicsContext.SetMesh(context.ndcQuadMesh);

            int renderCount = context.dynamicContextRead.selectedLightings.Count + context.dynamicContextRead.volumes.Count;
            int ofs = 0;
            if (renderCount > 0)
            {
                desc.ptt = ED3D12PrimitiveTopologyType.LINE;
                graphicsContext.SetMesh(context.cubeWireMesh);
                SetPipelineStateVariant(context.deviceResources, graphicsContext, rsPP, ref desc, rpAssets.PSOs["PSOWidgetUILight"]);
                for (int i = 0; i < renderCount; i++)
                {
                    CBufferGroup.SetCBVR(graphicsContext, ofs, 0);
                    graphicsContext.DrawIndexed(context.cubeWireMesh.GetIndexCount(), 0, 0);
                    ofs++;
                }
            }

            var data = ImGui.GetDrawData();

            Vector2 clip_off = data.DisplayPos;

            desc.blendState = EBlendState.alpha;
            desc.cullMode = ECullMode.none;
            desc.depthBias = 0;
            desc.slopeScaledDepthBias = 0;
            desc.dsvFormat = DxgiFormat.DXGI_FORMAT_UNKNOWN;
            desc.ptt = ED3D12PrimitiveTopologyType.TRIANGLE;
            desc.rtvFormat = context.swapChainFormat;
            desc.renderTargetCount = 1;
            desc.streamOutput = false;
            desc.wireFrame = false;
            desc.inputLayout = EInputLayout.imgui;
            SetPipelineStateVariant(context.deviceResources, graphicsContext, rsPP, ref desc, rpAssets.PSOs["ImGui"]);
            CBufferGroup.SetCBVR(graphicsContext, ofs, 0);
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
                    imguiMesh.Reload1(vertexDatas, indexDatas, 20, PrimitiveTopology._TRIANGLELIST);
                    graphicsContext.UploadMesh(imguiMesh);
                }
                unsafe
                {
                    int vtxOfs = 0;
                    int idxOfs = 0;
                    for (int i = 0; i < data.CmdListsCount; i++)
                    {
                        var cmdList = data.CmdListsRange[i];

                        imguiMesh.SetIndexFormat(DxgiFormat.DXGI_FORMAT_R16_UINT);
                        graphicsContext.SetMesh(imguiMesh);

                        for (int j = 0; j < cmdList.CmdBuffer.Size; j++)
                        {
                            var cmd = cmdList.CmdBuffer[j];

                            graphicsContext.SetSRVTSlot(rpAssets.texture2ds[rpAssets.ptr2string[cmd.TextureId]], 0);
                            graphicsContext.DrawIndexed((int)cmd.ElemCount, (int)(cmd.IdxOffset) + idxOfs, (int)(cmd.VtxOffset) + vtxOfs);
                        }
                        vtxOfs += cmdList.VtxBuffer.Size;
                        idxOfs += cmdList.IdxBuffer.Size;
                    }
                }
            }
        }
    }
}
