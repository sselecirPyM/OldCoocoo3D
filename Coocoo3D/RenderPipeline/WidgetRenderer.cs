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
        const int c_bgBufferSize = 16640;
        public ConstantBuffer[] constantBuffers = new ConstantBuffer[8];
        public ConstantBuffer[] bgConstantBuffers = new ConstantBuffer[4];
        public WidgetRenderer()
        {
            for (int i = 0; i < constantBuffers.Length; i++)
            {
                constantBuffers[i] = new ConstantBuffer();
            }
            for (int i = 0; i < bgConstantBuffers.Length; i++)
            {
                bgConstantBuffers[i] = new ConstantBuffer();
            }
        }
        ~WidgetRenderer()
        {
        }
        public void Reload(DeviceResources deviceResources)
        {
            for (int i = 0; i < constantBuffers.Length; i++)
            {
                constantBuffers[i].Reload(deviceResources, c_bufferSize);
            }
            for (int i = 0; i < bgConstantBuffers.Length; i++)
            {
                bgConstantBuffers[i].Reload(deviceResources, c_bgBufferSize);
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
        public override void PrepareRenderData(RenderPipelineContext context)
        {
            if (!context.dynamicContextRead.settings.ViewerUI) return;
            var graphicsContext = context.graphicsContext;
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

            graphicsContext.UpdateResource(constantBuffers[0], context.bigBuffer, c_bufferSize, 0);

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
                var bones = selectedEntity.boneComponent.bones;
                for (int i = 0; i < bones.Count; i++)
                {
                    Marshal.StructureToPtr(bones[i].staticPosition, pData + i * 16 + 256, true);
                }
                graphicsContext.UpdateResource(bgConstantBuffers[0], context.bigBuffer, c_bgBufferSize, 0);
            }
            var selectedLight = context.dynamicContextRead.selectedLightings;
            for (int i = 0; i < selectedLight.Count; i++)
            {
                Marshal.StructureToPtr(Matrix4x4.Transpose(cam.vpMatrix), pData, true);
                Marshal.StructureToPtr(selectedLight[i].Position - cam.Pos, pData + i * 16 + 128, true);
                Marshal.StructureToPtr(selectedLight[i].Range, pData + i * 16 + 140, true);
                if (i >= 1024) break;
            }
            if (selectedLight.Count > 0)
                graphicsContext.UpdateResource(bgConstantBuffers[1], context.bigBuffer, c_bgBufferSize, 0);
        }

        public override void RenderCamera(RenderPipelineContext context)
        {
            if (!context.dynamicContextRead.settings.ViewerUI) return;
            var graphicsContext = context.graphicsContext;
            graphicsContext.SetPObject(context.RPAssetsManager.PObjectWidgetUI1, CullMode.none);
            graphicsContext.SetCBVR(constantBuffers[0], 0);
            graphicsContext.SetSRVT(context.UI1Texture, 1);
            graphicsContext.SetMesh(context.ndcQuadMesh);
            graphicsContext.DrawIndexedInstanced(context.ndcQuadMeshIndexCount, 0, 0, allocated, 0);

            var selectedEntity = context.dynamicContextRead.selectedEntity;
            if (selectedEntity != null)
            {
                graphicsContext.SetSRVT(context.ScreenSizeDSVs[0], 2);
                graphicsContext.SetPObject(context.RPAssetsManager.PObjectWidgetUI2, CullMode.none);
                graphicsContext.SetCBVR(bgConstantBuffers[0], 0);
                graphicsContext.SetCBVR(context.CBs_Bone[indexOfSelectedEntity], 3);
                graphicsContext.DrawIndexedInstanced(context.ndcQuadMeshIndexCount, 0, 0, selectedEntity.boneComponent.bones.Count, 0);
            }
            var selectedLight = context.dynamicContextRead.selectedLightings;
            if (selectedLight.Count > 0)
            {
                graphicsContext.SetMesh(context.cubeWireMesh);
                graphicsContext.SetPObject(context.RPAssetsManager.PObjectWidgetUILight, CullMode.none, true);
                graphicsContext.SetCBVR(bgConstantBuffers[1], 0);
                graphicsContext.DrawIndexedInstanced(context.cubeWireMeshIndexCount, 0, 0, selectedLight.Count, 0);
            }
        }
    }
}
