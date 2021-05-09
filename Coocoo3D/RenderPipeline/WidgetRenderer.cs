using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.RenderPipeline.Wrap;

namespace Coocoo3D.RenderPipeline
{
    public class WidgetRenderer : RenderPipeline
    {
        const int c_bufferSize = 256;
        const int c_bufferSize1 = 4096;
        const int c_bigBufferSize = 65536;
        public CBuffer constantBuffer = new CBuffer();
        public CBuffer bigConstantBuffer = new CBuffer();
        CBufferGroup CBufferGroup = new CBufferGroup();
        public WidgetRenderer()
        {
        }
        public void Reload(DeviceResources deviceResources)
        {
            deviceResources.InitializeCBuffer(constantBuffer, c_bufferSize1);
            deviceResources.InitializeCBuffer(bigConstantBuffer, c_bigBufferSize);
            CBufferGroup.Reload(deviceResources, 256, 65536);
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
                Marshal.StructureToPtr(Matrix4x4.Transpose(cam.vpMatrix), pData, true);
                Marshal.StructureToPtr(Matrix4x4.Transpose(cam.pvMatrix), pData + 64, true);
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
                graphicsContext.UpdateResource(bigConstantBuffer, context.bigBuffer, c_bigBufferSize, 0);
            }
            var selectedLightings = context.dynamicContextRead.selectedLightings;

            Marshal.StructureToPtr(Matrix4x4.Transpose(cam.vpMatrix), pData, true);
            CBufferGroup.SetSlienceCount(selectedLightings.Count + context.dynamicContextRead.volumes.Count);
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
            CBufferGroup.UpdateSlienceComplete(graphicsContext);
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
            SetPipelineStateVariant(context.deviceResources, graphicsContext, rsPP, ref desc, rpAssets.PSOs["PSOWidgetUI1"]);
            graphicsContext.DrawIndexedInstanced(context.ndcQuadMesh.GetIndexCount(), 0, 0, allocated, 0);

            var selectedEntity = context.dynamicContextRead.selectedEntity;
            if (selectedEntity != null)
            {
                desc.ptt = ED3D12PrimitiveTopologyType.TRIANGLE;
                graphicsContext.SetCBVRSlot(bigConstantBuffer, 0, 0, 0);
                graphicsContext.SetCBVRSlot(context.CBs_Bone[indexOfSelectedEntity], 0, 0, 1);
                SetPipelineStateVariant(context.deviceResources, graphicsContext, rsPP, ref desc, rpAssets.PSOs["PSOWidgetUI2"]);

                graphicsContext.DrawIndexedInstanced(context.ndcQuadMesh.GetIndexCount(), 0, 0, selectedEntity.rendererComponent.bones.Count, 0);
            }

            int renderCount = context.dynamicContextRead.selectedLightings.Count + context.dynamicContextRead.volumes.Count;
            if (renderCount > 0)
            {
                desc.ptt = ED3D12PrimitiveTopologyType.LINE;
                graphicsContext.SetMesh(context.cubeWireMesh);
                SetPipelineStateVariant(context.deviceResources, graphicsContext, rsPP, ref desc, rpAssets.PSOs["PSOWidgetUILight"]);
                for (int i = 0; i < renderCount; i++)
                {
                    CBufferGroup.SetCBVR(graphicsContext, i, 0);
                    graphicsContext.DrawIndexed(context.cubeWireMesh.GetIndexCount(), 0, 0);
                }
            }
        }
    }
}
