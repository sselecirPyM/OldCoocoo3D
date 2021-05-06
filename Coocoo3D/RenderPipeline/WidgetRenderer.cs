using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Coocoo3D.RenderPipeline
{
    public class WidgetRenderer : RenderPipeline
    {
        const int c_bufferSize = 256;
        const int c_bufferSize1 = 4096;
        const int c_bgBufferSize = 16640;
        const int c_bgBufferSize1 = 65536;
        public CBuffer constantBuffer = new CBuffer();
        public CBuffer[] bgConstantBuffers = new CBuffer[4];
        public WidgetRenderer()
        {
            for (int i = 0; i < bgConstantBuffers.Length; i++)
            {
                bgConstantBuffers[i] = new CBuffer();
            }
        }
        public void Reload(DeviceResources deviceResources)
        {
            deviceResources.InitializeCBuffer(constantBuffer, c_bufferSize1);
            for (int i = 0; i < bgConstantBuffers.Length; i++)
            {
                deviceResources.InitializeCBuffer(bgConstantBuffers[i], c_bgBufferSize);
            }
        }

        struct _Data
        {
            public Vector2 size;
            public Vector2 offset;
            public Vector2 uvSize;
            public Vector2 uvOffset;
        }
        readonly Vector2 c_buttonSize = new Vector2(64, 64);

        int allocated;
        int indexOfSelectedEntity;
        public override void PrepareRenderData(RenderPipelineContext context, GraphicsContext graphicsContext)
        {
            if (!context.dynamicContextRead.settings.ViewerUI) return;
            IntPtr pData = Marshal.UnsafeAddrOfPinnedArrayElement(context.bigBuffer, 0);
            Vector2 screenSize = new Vector2(context.screenWidth, context.screenHeight) / context.logicScale;
            Marshal.StructureToPtr(screenSize, pData, true);

            allocated = 0;
            int allocatedSize = 32;
            void write(_Data data1)
            {
                Marshal.StructureToPtr(data1, pData + allocatedSize, true);
                allocatedSize += 32;
                allocated++;
            }

            #region Buttons
            write(new _Data()
            {
                size = c_buttonSize,
                offset = new Vector2(screenSize.X, 0) + new Vector2(-192, 64),
                uvSize = new Vector2(0.25f, 0.25f),
                uvOffset = new Vector2(0, 0),
            });
            write(new _Data()
            {
                size = c_buttonSize,
                offset = new Vector2(screenSize.X, 0) + new Vector2(-128, 64),
                uvSize = new Vector2(0.25f, 0.25f),
                uvOffset = new Vector2(0.25f, 0),
            });
            write(new _Data()
            {
                size = c_buttonSize,
                offset = new Vector2(screenSize.X, 0) + new Vector2(-64, 64),
                uvSize = new Vector2(0.25f, 0.25f),
                uvOffset = new Vector2(0.5f, 0),
            });
            write(new _Data()
            {
                size = c_buttonSize,
                offset = new Vector2(screenSize.X, 0) + new Vector2(-192, 0),
                uvSize = new Vector2(0.25f, 0.25f),
                uvOffset = new Vector2(0, 0.25f),
            });
            write(new _Data()
            {
                size = c_buttonSize,
                offset = new Vector2(screenSize.X, 0) + new Vector2(-128, 0),
                uvSize = new Vector2(0.25f, 0.25f),
                uvOffset = new Vector2(0.25f, 0.25f),
            });
            write(new _Data()
            {
                size = c_buttonSize,
                offset = new Vector2(screenSize.X, 0) + new Vector2(-64, 0),
                uvSize = new Vector2(0.25f, 0.25f),
                uvOffset = new Vector2(0.5f, 0.25f),
            });
            #endregion

            graphicsContext.UpdateResource(constantBuffer, context.bigBuffer, c_bufferSize, 0);

            var cam = context.dynamicContextRead.cameras[0];

            var selectedEntity = context.dynamicContextRead.selectedEntity;
            if (selectedEntity != null)
            {
                indexOfSelectedEntity = context.dynamicContextRead.entities.IndexOf(selectedEntity);
                Matrix4x4.Invert(cam.pMatrix, out Matrix4x4 mat1);
                Marshal.StructureToPtr(Matrix4x4.Transpose(cam.vpMatrix), pData, true);
                Marshal.StructureToPtr(Matrix4x4.Transpose(mat1), pData + 64, true);
                Marshal.StructureToPtr(new _Data()
                {
                    size = new Vector2(16, 16),
                    offset = new Vector2(0, 0),
                    uvSize = new Vector2(0.25f, 0.25f),
                    uvOffset = new Vector2(0, 0),
                }, pData + 128, true);
                Marshal.StructureToPtr(screenSize, pData + 160, true);
                var bones = selectedEntity.rendererComponent.bones;
                for (int i = 0; i < bones.Count; i++)
                {
                    Marshal.StructureToPtr(bones[i].staticPosition, pData + i * 16 + 256, true);
                }
                graphicsContext.UpdateResource(bgConstantBuffers[0], context.bigBuffer, c_bgBufferSize, 0);
            }
            var selectedLightings = context.dynamicContextRead.selectedLightings;
            for (int i = 0; i < selectedLightings.Count; i++)
            {
                Marshal.StructureToPtr(Matrix4x4.Transpose(cam.vpMatrix), pData, true);
                Marshal.StructureToPtr(selectedLightings[i].Position, pData + i * 16 + 128, true);
                Marshal.StructureToPtr(selectedLightings[i].Range, pData + i * 16 + 140, true);
                if (i >= 1024) break;
            }
            if (selectedLightings.Count > 0)
                graphicsContext.UpdateResource(bgConstantBuffers[1], context.bigBuffer, c_bgBufferSize, 0);
        }

        public override void RenderCamera(RenderPipelineContext context, GraphicsContext graphicsContext)
        {
            if (!context.dynamicContextRead.settings.ViewerUI) return;
            var rpAssets = context.RPAssetsManager;
            var rsPP = rpAssets.rootSignaturePostProcess;
            graphicsContext.SetCBVR(constantBuffer, 0);
            graphicsContext.SetSRVTSlot(rpAssets.texture2ds["_UI1Texture"], 0);
            graphicsContext.SetMesh(context.ndcQuadMesh);

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
            SetPipelineStateVariant(context.deviceResources, graphicsContext, rsPP, ref desc, rpAssets.PSOs["PObjectWidgetUI1"]);
            graphicsContext.DrawIndexedInstanced(context.ndcQuadMesh.GetIndexCount(), 0, 0, allocated, 0);

            var selectedEntity = context.dynamicContextRead.selectedEntity;
            if (selectedEntity != null)
            {
                desc.ptt = ED3D12PrimitiveTopologyType.TRIANGLE;
                graphicsContext.SetCBVR(bgConstantBuffers[0], 0);
                graphicsContext.SetCBVR(context.CBs_Bone[indexOfSelectedEntity], 3);
                SetPipelineStateVariant(context.deviceResources, graphicsContext, rsPP, ref desc, rpAssets.PSOs["PObjectWidgetUI2"]);

                graphicsContext.DrawIndexedInstanced(context.ndcQuadMesh.GetIndexCount(), 0, 0, selectedEntity.rendererComponent.bones.Count, 0);
            }
            var selectedLight = context.dynamicContextRead.selectedLightings;
            if (selectedLight.Count > 0)
            {
                desc.ptt = ED3D12PrimitiveTopologyType.LINE;
                graphicsContext.SetMesh(context.cubeWireMesh);
                graphicsContext.SetCBVR(bgConstantBuffers[1], 0);
                SetPipelineStateVariant(context.deviceResources, graphicsContext, rsPP, ref desc, rpAssets.PSOs["PObjectWidgetUILight"]);

                graphicsContext.DrawIndexedInstanced(context.cubeWireMesh.GetIndexCount(), 0, 0, selectedLight.Count, 0);
            }
        }
    }
}
