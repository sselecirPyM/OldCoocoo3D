using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Present;
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
    public class ForwardRenderPipeline1 : RenderPipeline
    {
        public const int c_materialDataSize = 256;
        public const int c_offsetMaterialData = 0;
        public const int c_lightingDataSize = 512;
        public const int c_offsetLightingData = c_offsetMaterialData + c_materialDataSize;

        public const int c_presentDataSize = 512;
        public const int c_offsetPresentData = c_offsetLightingData + c_lightingDataSize;
        public void Reload(DeviceResources deviceResources)
        {
            Ready = true;
        }

        Random randomGenerator = new Random();

        public PresentData[] cameraPresentDatas = new PresentData[c_maxCameraPerRender];

        struct _Counters
        {
            public int material;
            public int vertex;
        }

        bool HasMainLight;
        public override void PrepareRenderData(RenderPipelineContext context)
        {
            var deviceResources = context.deviceResources;
            var cameras = context.dynamicContextRead.cameras;
            var graphicsContext = context.graphicsContext;
            ref var settings = ref context.dynamicContextRead.settings;
            ref var inShaderSettings = ref context.dynamicContextRead.inShaderSettings;
            var Entities = context.dynamicContextRead.entities;
            var lightings = context.dynamicContextRead.lightings;

            int countMaterials = 0;
            for (int i = 0; i < Entities.Count; i++)
            {
                countMaterials += Entities[i].rendererComponent.Materials.Count;
            }
            context.DesireMaterialBuffers(countMaterials);
            ref var bigBuffer = ref context.bigBuffer;
            #region Lighting
            int lightCount = 0;
            var camera = context.dynamicContextRead.cameras[0];
            Matrix4x4 lightCameraMatrix0 = Matrix4x4.Identity;
            Matrix4x4 lightCameraMatrix1 = Matrix4x4.Identity;
            IntPtr pBufferData = Marshal.UnsafeAddrOfPinnedArrayElement(bigBuffer, c_offsetPresentData);
            HasMainLight = false;
            var LightCameraDataBuffers = context.LightCameraDataBuffer;
            if (lightings.Count > 0 && lightings[0].LightingType == LightingType.Directional)
            {
                lightCameraMatrix0 = Matrix4x4.Transpose(lightings[0].GetLightingMatrix(2, camera.LookAtPoint, camera.Distance));
                Marshal.StructureToPtr(lightCameraMatrix0, pBufferData, true);

                lightCameraMatrix1 = Matrix4x4.Transpose(lightings[0].GetLightingMatrix(settings.ExtendShadowMapRange, camera.LookAtPoint, camera.Angle, camera.Distance));
                Marshal.StructureToPtr(lightCameraMatrix1, pBufferData + 256, true);
                graphicsContext.UpdateResource(LightCameraDataBuffers, bigBuffer, c_presentDataSize, c_offsetPresentData);
                HasMainLight = true;
            }

            IntPtr p0 = Marshal.UnsafeAddrOfPinnedArrayElement(bigBuffer, c_offsetLightingData);
            Array.Clear(bigBuffer, c_offsetLightingData, c_lightingDataSize);
            pBufferData = p0 + 256;
            Marshal.StructureToPtr(lightCameraMatrix0, p0, true);
            Marshal.StructureToPtr(lightCameraMatrix1, p0 + 64, true);
            for (int i = 0; i < lightings.Count; i++)
            {
                LightingData data1 = lightings[i];
                Marshal.StructureToPtr(data1.GetPositionOrDirection(), pBufferData, true);
                Marshal.StructureToPtr((uint)data1.LightingType, pBufferData + 12, true);
                Marshal.StructureToPtr(data1.Color, pBufferData + 16, true);

                lightCount++;
                pBufferData += 32;
                if (lightCount >= 8)
                    break;
            }
            #endregion

            #region Update material data
            int matIndex = 0;
            pBufferData = Marshal.UnsafeAddrOfPinnedArrayElement(bigBuffer, c_offsetMaterialData);
            for (int i = 0; i < Entities.Count; i++)
            {
                var Materials = Entities[i].rendererComponent.Materials;
                for (int j = 0; j < Materials.Count; j++)
                {
                    Marshal.StructureToPtr(Materials[j].innerStruct, pBufferData, true);
                    context.MaterialBufferGroup.UpdateSlience(graphicsContext, bigBuffer, c_offsetMaterialData, c_materialDataSize + c_lightingDataSize, matIndex);
                    matIndex++;
                }
            }
            if (matIndex > 0)
                context.MaterialBufferGroup.UpdateSlienceComplete(graphicsContext);
            #endregion

            pBufferData = Marshal.UnsafeAddrOfPinnedArrayElement(bigBuffer, c_offsetPresentData);
            for (int i = 0; i < cameras.Count; i++)
            {
                cameraPresentDatas[i].PlayTime = (float)context.dynamicContextRead.Time;
                cameraPresentDatas[i].DeltaTime = (float)context.dynamicContextRead.DeltaTime;

                cameraPresentDatas[i].UpdateCameraData(cameras[i]);
                cameraPresentDatas[i].RandomValue1 = randomGenerator.Next(int.MinValue, int.MaxValue);
                cameraPresentDatas[i].RandomValue2 = randomGenerator.Next(int.MinValue, int.MaxValue);
                cameraPresentDatas[i].inShaderSettings = inShaderSettings;
                Marshal.StructureToPtr(cameraPresentDatas[i], pBufferData, true);
                graphicsContext.UpdateResource(context.CameraDataBuffers[i], bigBuffer, c_presentDataSize, c_offsetPresentData);
            }

        }
        //you can fold local function in your editor
        public override void RenderCamera(RenderPipelineContext context)
        {

            var Entities = context.dynamicContextRead.entities;
            var graphicsContext = context.graphicsContext;
            ref var settings = ref context.dynamicContextRead.settings;
            ref var inShaderSettings = ref context.dynamicContextRead.inShaderSettings;
            Texture2D textureLoading = context.TextureLoading;
            Texture2D textureError = context.TextureError;
            var RPAssetsManager = context.RPAssetsManager;

            PObject currentDrawPObject;
            PObject currentSkinningPObject;
            currentSkinningPObject = RPAssetsManager.PObjectMMDSkinning;
            if (settings.RenderStyle == 1)
                currentDrawPObject = RPAssetsManager.PObjectMMD_Toon1;
            else if (inShaderSettings.Quality == 0)
                currentDrawPObject = RPAssetsManager.PObjectMMD;
            else
                currentDrawPObject = RPAssetsManager.PObjectMMD_DisneyBrdf;

            graphicsContext.SetRootSignature(RPAssetsManager.rootSignatureSkinning);
            graphicsContext.SetSOMesh(context.SkinningMeshBuffer);
            void EntitySkinning(MMDRendererComponent rendererComponent, CBuffer cameraPresentData, CBuffer entityBoneDataBuffer)
            {
                var Materials = rendererComponent.Materials;
                graphicsContext.SetCBVR(entityBoneDataBuffer, 0);
                graphicsContext.SetCBVR(cameraPresentData, 2);
                var POSkinning = rendererComponent.POSkinning;
                if (POSkinning != null && POSkinning.Status == GraphicsObjectStatus.loaded)
                    graphicsContext.SetPObject(POSkinning, 0);
                else
                    graphicsContext.SetPObject(currentSkinningPObject, 0);
                graphicsContext.SetMeshVertex1(rendererComponent.mesh);
                graphicsContext.SetMeshVertex(rendererComponent.meshAppend);
                int indexCountAll = rendererComponent.meshVertexCount;
                graphicsContext.Draw(indexCountAll, 0);
            }
            for (int i = 0; i < Entities.Count; i++)
                EntitySkinning(Entities[i].rendererComponent, context.CameraDataBuffers[0], context.CBs_Bone[i]);
            graphicsContext.SetSOMeshNone();


            graphicsContext.SetRootSignatureCompute(RPAssetsManager.rootSignatureCompute);
            void ParticleCompute(MMDRendererComponent rendererComponent, CBuffer cameraPresentData, CBuffer entityBoneDataBuffer, ref _Counters counter)
            {
                if (rendererComponent.ParticleCompute == null || rendererComponent.meshParticleBuffer == null || rendererComponent.ParticleCompute.Status != GraphicsObjectStatus.loaded)
                {
                    counter.vertex += rendererComponent.meshVertexCount;
                    return;
                }
                graphicsContext.SetComputeCBVR(entityBoneDataBuffer, 0);
                //graphicsContext.SetComputeCBVR(entityDataBuffer, 1);
                graphicsContext.SetComputeCBVR(cameraPresentData, 2);
                graphicsContext.SetComputeUAVR(context.SkinningMeshBuffer, counter.vertex, 4);
                graphicsContext.SetComputeUAVR(rendererComponent.meshParticleBuffer, 0, 5);
                graphicsContext.SetPObject(rendererComponent.ParticleCompute);
                graphicsContext.Dispatch((rendererComponent.meshVertexCount + 63) / 64, 1, 1);
                counter.vertex += rendererComponent.meshVertexCount;
            }
            _Counters counterParticle = new _Counters();
            for (int i = 0; i < Entities.Count; i++)
                ParticleCompute(Entities[i].rendererComponent, context.CameraDataBuffers[0], context.CBs_Bone[i], ref counterParticle);
            if (HasMainLight && inShaderSettings.EnableShadow)
            {
                void _RenderEntityShadow(MMDRendererComponent rendererComponent, CBuffer cameraPresentData, int bufferOffset, CBuffer entityBoneDataBuffer, ref _Counters counter)
                {
                    var Materials = rendererComponent.Materials;
                    graphicsContext.SetCBVR(entityBoneDataBuffer, 0);
                    graphicsContext.SetCBVR(cameraPresentData, bufferOffset, 1, 2);
                    graphicsContext.SetMeshIndex(rendererComponent.mesh);

                    //List<Texture2D> texs = rendererComponent.textures;
                    //int countIndexLocal = 0;
                    //for (int i = 0; i < Materials.Count; i++)
                    //{
                    //    if (Materials[i].DrawFlags.HasFlag(DrawFlag.CastSelfShadow))
                    //    {
                    //        Texture2D tex1 = null;
                    //        if (Materials[i].texIndex != -1)
                    //            tex1 = texs[Materials[i].texIndex];
                    //        graphicsContext.SetCBVR(materialBuffers[counter.material], 3);
                    //        graphicsContext.SetSRVT(TextureStatusSelect(tex1, textureLoading, textureError, textureError), 4);
                    //        graphicsContext.DrawIndexed(Materials[i].indexCount, countIndexLocal, counter.vertex);
                    //    }
                    //    counter.material++;
                    //    countIndexLocal += Materials[i].indexCount;
                    //}
                    graphicsContext.DrawIndexed(rendererComponent.meshIndexCount, 0, counter.vertex);
                    counter.vertex += rendererComponent.meshVertexCount;
                }

                graphicsContext.SetMesh(context.SkinningMeshBuffer);
                graphicsContext.SetRootSignature(RPAssetsManager.rootSignature);
                PSODesc desc;
                desc.blendState = EBlendState.none;
                desc.cullMode = ECullMode.none;
                desc.depthBias = 2500;
                desc.dsvFormat = context.depthFormat;
                desc.inputLayout = EInputLayout.skinned;
                desc.ptt = ED3D12PrimitiveTopologyType.TRIANGLE;
                desc.rtvFormat = DxgiFormat.DXGI_FORMAT_UNKNOWN;
                desc.renderTargetCount = 0;
                desc.streamOutput = false;
                desc.wireFrame = false;
                int variant = RPAssetsManager.PObjectMMDShadowDepth.GetVariantIndex(context.deviceResources, RPAssetsManager.rootSignature, desc);
                graphicsContext.SetPObject1(RPAssetsManager.PObjectMMDShadowDepth, variant);

                graphicsContext.SetDSV(context.ShadowMapCube, 0, true);
                _Counters counterShadow0 = new _Counters();
                var LightCameraDataBuffers = context.LightCameraDataBuffer;
                for (int i = 0; i < Entities.Count; i++)
                    _RenderEntityShadow(Entities[i].rendererComponent, LightCameraDataBuffers, 0, context.CBs_Bone[i], ref counterShadow0);
                graphicsContext.SetDSV(context.ShadowMapCube, 1, true);
                _Counters counterShadow1 = new _Counters();
                for (int i = 0; i < Entities.Count; i++)
                    _RenderEntityShadow(Entities[i].rendererComponent, LightCameraDataBuffers, 1, context.CBs_Bone[i], ref counterShadow1);
            }


            int cameraIndex = 0;

            graphicsContext.SetRootSignature(RPAssetsManager.rootSignature);
            graphicsContext.SetRTVDSV(context.outputRTV, context.ScreenSizeDSVs[0], Vector4.Zero, false, true);
            graphicsContext.SetCBVR(context.CameraDataBuffers[cameraIndex], 2);
            graphicsContext.SetSRVTArray(context.ShadowMapCube, 5);
            graphicsContext.SetSRVT(context.SkyBox, 6);
            graphicsContext.SetSRVT(context.IrradianceMap, 7);
            graphicsContext.SetSRVT(context.BRDFLut, 8);
            #region Render Sky box
            graphicsContext.SetPObject(RPAssetsManager.PObjectSkyBox, ECullMode.back);
            graphicsContext.SetMesh(context.ndcQuadMesh);
            graphicsContext.DrawIndexed(context.ndcQuadMeshIndexCount, 0, 0);
            #endregion

            graphicsContext.SetSRVT(context.EnvironmentMap, 6);
            graphicsContext.SetMesh(context.SkinningMeshBuffer);

            void _RenderEntity(MMDRendererComponent rendererComponent, CBuffer cameraPresentData, CBuffer entityBoneDataBuffer, ref _Counters counter)
            {
                var PODraw = PObjectStatusSelect(rendererComponent.PODraw, RPAssetsManager.PObjectMMDLoading, currentDrawPObject, RPAssetsManager.PObjectMMDError);
                var Materials = rendererComponent.Materials;
                List<Texture2D> texs = rendererComponent.textures;
                graphicsContext.SetMeshIndex(rendererComponent.mesh);
                graphicsContext.SetCBVR(entityBoneDataBuffer, 0);
                graphicsContext.SetCBVR(cameraPresentData, 2);
                int countIndexLocal = 0;
                for (int i = 0; i < Materials.Count; i++)
                {
                    if (Materials[i].innerStruct.DiffuseColor.W <= 0)
                    {
                        counter.material++;
                        countIndexLocal += Materials[i].indexCount;
                        continue;
                    }
                    Texture2D tex1 = null;
                    if (Materials[i].texIndex != -1 && Materials[i].texIndex < Materials.Count)
                        tex1 = texs[Materials[i].texIndex];
                    Texture2D tex2 = null;
                    if (Materials[i].toonIndex > -1 && Materials[i].toonIndex < Materials.Count)
                        tex2 = texs[Materials[i].toonIndex];
                    context.MaterialBufferGroup.SetCBVR(graphicsContext, counter.material, 1);

                    //graphicsContext.SetSRVT(TextureStatusSelect(tex1, textureLoading, textureError, textureError), 3);
                    //graphicsContext.SetSRVT(TextureStatusSelect(tex2, textureLoading, textureError, textureError), 4);
                    CooGExtension.SetSRVTexture2(graphicsContext, tex1, tex2, 3, textureLoading, textureError);


                    PSODesc desc;
                    desc.blendState = EBlendState.alpha;
                    desc.cullMode = Materials[i].DrawFlags.HasFlag(DrawFlag.DrawDoubleFace) ? ECullMode.none : ECullMode.back;
                    desc.depthBias = 0;
                    desc.dsvFormat = context.depthFormat;
                    desc.inputLayout = EInputLayout.skinned;
                    desc.ptt = ED3D12PrimitiveTopologyType.TRIANGLE;
                    desc.rtvFormat = context.outputFormat;
                    desc.renderTargetCount = 1;
                    desc.streamOutput = false;
                    desc.wireFrame = context.dynamicContextRead.settings.Wireframe;
                    int variant = PODraw.GetVariantIndex(context.deviceResources, RPAssetsManager.rootSignature, desc);
                    graphicsContext.SetPObject1(PODraw, variant);

                    graphicsContext.DrawIndexed(Materials[i].indexCount, countIndexLocal, counter.vertex);
                    counter.material++;
                    countIndexLocal += Materials[i].indexCount;
                }
                counter.vertex += rendererComponent.meshVertexCount;
            }
            _Counters counter2 = new _Counters();
            for (int i = 0; i < Entities.Count; i++)
                _RenderEntity(Entities[i].rendererComponent, context.CameraDataBuffers[cameraIndex], context.CBs_Bone[i], ref counter2);
        }
    }
}