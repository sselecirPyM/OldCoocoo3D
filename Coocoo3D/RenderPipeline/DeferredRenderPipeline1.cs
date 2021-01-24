﻿using Coocoo3D.Components;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline.Wrap;
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
    public class DeferredRenderPipeline1 : RenderPipeline
    {
        const int c_materialDataSize = 256;
        const int c_presentDataSize = 512;
        const int c_lightingDataSize = 512;
        #region forward
        public const int c_offsetMaterialData = 0;
        public const int c_offsetLightingData = c_offsetMaterialData + c_materialDataSize;
        public const int c_offsetPresentData = c_offsetLightingData + c_lightingDataSize;
        #endregion
        public PresentData[] cameraPresentDatas = new PresentData[c_maxCameraPerRender];

        Random randomGenerator = new Random();

        public void Reload(DeviceResources deviceResources)
        {
            lightingBuffers.Reload(256, 65536);
            Ready = true;
        }

        public CBufferGroup lightingBuffers = new CBufferGroup();

        private void DesireLightingBuffers(DeviceResources deviceResources, int count)
        {
            lightingBuffers.SetSlienceCount(deviceResources, count);
        }

        struct _Counters
        {
            public int material;
            public int vertex;
        }

        struct _Struct1
        {
            public Matrix4x4 x1;
            public Matrix4x4 x2;
            public Vector3 positionOrDirection;
            public uint lightType;
            public Vector4 color;
            public float Range;
        }

        bool HasMainLight;
        Matrix4x4 lightCameraMatrix = Matrix4x4.Identity;
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
            DesireLightingBuffers(context.deviceResources, lightings.Count * 2);
            var camera = context.dynamicContextRead.cameras[0];
            ref var bigBuffer = ref context.bigBuffer;
            IntPtr pBufferData = Marshal.UnsafeAddrOfPinnedArrayElement(bigBuffer, 0);
            #region forward lighting
            int lightCount = 0;
            Matrix4x4 lightCameraMatrix0 = Matrix4x4.Identity;
            Matrix4x4 lightCameraMatrix1 = Matrix4x4.Identity;
            HasMainLight = false;
            Array.Clear(bigBuffer, c_offsetLightingData, c_lightingDataSize);
            var LightCameraDataBuffers = context.LightCameraDataBuffer;
            if (lightings.Count > 0 && lightings[0].LightingType == LightingType.Directional)
            {
                lightCameraMatrix0 = Matrix4x4.Transpose(lightings[0].GetLightingMatrix(2, camera.LookAtPoint, camera.Distance));
                Marshal.StructureToPtr(lightCameraMatrix0, pBufferData + c_offsetPresentData, true);

                lightCameraMatrix1 = Matrix4x4.Transpose(lightings[0].GetLightingMatrix(settings.ExtendShadowMapRange, camera.LookAtPoint, camera.Angle, camera.Distance));
                Marshal.StructureToPtr(lightCameraMatrix1, pBufferData + c_offsetPresentData + 256, true);
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

            pBufferData = Marshal.UnsafeAddrOfPinnedArrayElement(bigBuffer, 0);
            #region Update material data
            int matIndex = 0;
            for (int i = 0; i < Entities.Count; i++)
            {
                var Materials = Entities[i].rendererComponent.Materials;
                for (int j = 0; j < Materials.Count; j++)
                {
                    Marshal.StructureToPtr(Materials[j].innerStruct, pBufferData, true);
                    context.MaterialBufferGroup.UpdateSlience(graphicsContext, bigBuffer, 0, c_materialDataSize + c_lightingDataSize, matIndex);
                    matIndex++;
                }
            }
            if (matIndex > 0)
                context.MaterialBufferGroup.UpdateSlienceComplete(graphicsContext);
            #endregion

            for (int i = 0; i < cameras.Count; i++)
            {
                cameraPresentDatas[i].PlayTime = (float)context.dynamicContextRead.Time;
                cameraPresentDatas[i].DeltaTime = (float)context.dynamicContextRead.DeltaTime;

                cameraPresentDatas[i].UpdateCameraData(cameras[i]);
                cameraPresentDatas[i].RandomValue1 = randomGenerator.Next(int.MinValue, int.MaxValue);
                cameraPresentDatas[i].RandomValue2 = randomGenerator.Next(int.MinValue, int.MaxValue);
                cameraPresentDatas[i].inShaderSettings = inShaderSettings;
                Marshal.StructureToPtr(cameraPresentDatas[i], pBufferData, true);
                graphicsContext.UpdateResource(context.CameraDataBuffers[i], bigBuffer, c_presentDataSize, 0);
            }
            for (int i = 0; i < lightings.Count; i++)
            {
                _Struct1 _struct1 = new _Struct1()
                {
                    positionOrDirection = lightings[i].GetPositionOrDirection(),
                    lightType = (uint)lightings[i].LightingType,
                    color = lightings[i].Color,
                    Range = lightings[i].Range
                };
                if (lightings[i].LightingType == LightingType.Directional)
                {
                    _struct1.x1 = Matrix4x4.Transpose(lightings[i].GetLightingMatrix(2, camera.LookAtPoint, camera.Distance));
                    _struct1.x2 = Matrix4x4.Transpose(lightings[i].GetLightingMatrix(64, camera.LookAtPoint, camera.Angle, camera.Distance));
                    Marshal.StructureToPtr(_struct1.x2, pBufferData + 256, true);
                }
                Marshal.StructureToPtr(_struct1, pBufferData, true);
                lightingBuffers.UpdateSlience(graphicsContext, bigBuffer, 0, 256, i * 2);
                lightingBuffers.UpdateSlience(graphicsContext, bigBuffer, 256, 256, i * 2 + 1);
            }
            if (lightings.Count > 0)
                lightingBuffers.UpdateSlienceComplete(graphicsContext);
        }

        public override void RenderCamera(RenderPipelineContext context)
        {
            var Entities = context.dynamicContextRead.entities;
            var graphicsContext = context.graphicsContext;
            ref var settings = ref context.dynamicContextRead.settings;
            ref var inShaderSettings = ref context.dynamicContextRead.inShaderSettings;
            Texture2D textureLoading = context.TextureLoading;
            Texture2D textureError = context.TextureError;
            var RPAssetsManager = context.RPAssetsManager;

            graphicsContext.SetRootSignature(context.RPAssetsManager.rootSignatureSkinning);
            graphicsContext.SetSOMesh(context.SkinningMeshBuffer);
            PObject mmdSkinning = context.RPAssetsManager.PObjectMMDSkinning;
            void EntitySkinning(MMDRendererComponent rendererComponent, CBuffer cameraPresentData, CBuffer entityBoneDataBuffer)
            {
                var Materials = rendererComponent.Materials;
                graphicsContext.SetCBVR(entityBoneDataBuffer, 0);
                graphicsContext.SetCBVR(cameraPresentData, 2);
                var POSkinning = PObjectStatusSelect(rendererComponent.POSkinning, mmdSkinning, mmdSkinning, mmdSkinning);

                graphicsContext.SetPObject(POSkinning, 0);
                graphicsContext.SetMeshVertex1(rendererComponent.mesh);
                graphicsContext.SetMeshVertex(rendererComponent.meshAppend);
                int indexCountAll = rendererComponent.meshVertexCount;
                graphicsContext.Draw(indexCountAll, 0);
            }
            for (int i = 0; i < Entities.Count; i++)
                EntitySkinning(Entities[i].rendererComponent, context.CameraDataBuffers[0], context.CBs_Bone[i]);
            graphicsContext.SetSOMeshNone();


            int cameraIndex = 0;

            graphicsContext.SetRootSignature(context.RPAssetsManager.rootSignature);
            graphicsContext.SetRTVDSV(context.ScreenSizeRenderTextures, context.ScreenSizeDSVs[0], Vector4.Zero, true, true);
            graphicsContext.SetMesh(context.SkinningMeshBuffer);

            void _RenderEntity(MMDRendererComponent rendererComponent, CBuffer cameraPresentData, CBuffer entityBoneDataBuffer, ref _Counters counter)
            {
                var PODraw = context.RPAssetsManager.PObjectDeferredRenderGBuffer; ;
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
                    //ECullMode cullMode = ECullMode.back;
                    //if (Materials[i].DrawFlags.HasFlag(DrawFlag.DrawDoubleFace))
                    //    cullMode = ECullMode.none;
                    //graphicsContext.SetPObject(PODraw, cullMode, context.dynamicContextRead.settings.Wireframe);


                    PSODesc desc;
                    desc.blendState = EBlendState.none;
                    desc.cullMode = Materials[i].DrawFlags.HasFlag(DrawFlag.DrawDoubleFace) ? ECullMode.none : ECullMode.back;
                    desc.depthBias = 0;
                    desc.dsvFormat = context.depthFormat;
                    desc.inputLayout = EInputLayout.skinned;
                    desc.ptt = ED3D12PrimitiveTopologyType.TRIANGLE;
                    desc.rtvFormat = context.gBufferFormat;
                    desc.renderTargetCount = 3;
                    desc.streamOutput = false;
                    desc.wireFrame = context.dynamicContextRead.settings.Wireframe;
                    int variant = PODraw.GetVariantIndex(context.deviceResources, context.RPAssetsManager.rootSignature, desc);
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

            graphicsContext.SetRTV(context.outputRTV, Vector4.Zero, true);
            graphicsContext.SetCBVR(context.CameraDataBuffers[cameraIndex], 2);
            graphicsContext.SetSRVT(context.ScreenSizeRenderTextures[0], 3);
            graphicsContext.SetSRVT(context.ScreenSizeRenderTextures[1], 4);
            graphicsContext.SetSRVT(context.ScreenSizeDSVs[0], 5);
            graphicsContext.SetSRVT(context.EnvironmentMap, 6);
            graphicsContext.SetSRVT(context.IrradianceMap, 7);
            graphicsContext.SetSRVT(context.BRDFLut, 8);

            PSODesc desc1;
            desc1.blendState = EBlendState.add;
            desc1.cullMode = ECullMode.back;
            desc1.depthBias = 0;
            desc1.dsvFormat = DxgiFormat.DXGI_FORMAT_UNKNOWN;
            desc1.inputLayout = EInputLayout.skinned;
            desc1.ptt = ED3D12PrimitiveTopologyType.TRIANGLE;
            desc1.rtvFormat = context.outputFormat;
            desc1.renderTargetCount = 1;
            desc1.streamOutput = false;
            desc1.wireFrame = false;
            int variant1 = context.RPAssetsManager.PObjectDeferredRenderIBL.GetVariantIndex(context.deviceResources, context.RPAssetsManager.rootSignature, desc1);
            graphicsContext.SetPObject1(context.RPAssetsManager.PObjectDeferredRenderIBL, variant1);

            graphicsContext.SetMesh(context.ndcQuadMesh);
            graphicsContext.DrawIndexed(context.ndcQuadMeshIndexCount, 0, 0);

            var lightings = context.dynamicContextRead.lightings;
            for (int i = lightings.Count - 1; i >= 0; i--)
            {
                if (lightings[i].LightingType == LightingType.Directional)
                {
                    //graphicsContext.SetPObject(context.RPAssetsManager.PObjectMMDShadowDepth, ECullMode.none);

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

                    graphicsContext.SetMesh(context.SkinningMeshBuffer);

                    void _RenderEntityShadow(MMDRendererComponent rendererComponent, CBufferGroup cameraPresentData, int presentDataIndex, CBuffer entityBoneDataBuffer, ref _Counters counter)
                    {
                        var Materials = rendererComponent.Materials;
                        graphicsContext.SetCBVR(entityBoneDataBuffer, 0);
                        cameraPresentData.SetCBVR(graphicsContext, presentDataIndex, 2);
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

                    graphicsContext.SetDSV(context.ShadowMapCube, 0, true);
                    _Counters counterShadow0 = new _Counters();
                    for (int j = 0; j < Entities.Count; j++)
                        _RenderEntityShadow(Entities[j].rendererComponent, lightingBuffers, i * 2, context.CBs_Bone[j], ref counterShadow0);

                    graphicsContext.SetDSV(context.ShadowMapCube, 1, true);
                    _Counters counterShadow1 = new _Counters();
                    for (int j = 0; j < Entities.Count; j++)
                        _RenderEntityShadow(Entities[j].rendererComponent, lightingBuffers, i * 2 + 1, context.CBs_Bone[j], ref counterShadow1);


                    graphicsContext.SetRTV(context.outputRTV, Vector4.Zero, false);
                    lightingBuffers.SetCBVR(graphicsContext, i * 2, 1);
                    graphicsContext.SetCBVR(context.CameraDataBuffers[cameraIndex], 2);
                    graphicsContext.SetSRVT(context.ScreenSizeRenderTextures[0], 3);
                    graphicsContext.SetSRVT(context.ScreenSizeRenderTextures[1], 4);
                    graphicsContext.SetSRVTArray(context.ShadowMapCube, 5);
                    graphicsContext.SetSRVT(context.ScreenSizeDSVs[0], 6);

                    int variant2 = RPAssetsManager.PObjectDeferredRenderDirectLight.GetVariantIndex(context.deviceResources, RPAssetsManager.rootSignature, desc1);
                    graphicsContext.SetPObject1(RPAssetsManager.PObjectDeferredRenderDirectLight, variant2);

                    graphicsContext.SetMesh(context.ndcQuadMesh);
                    graphicsContext.DrawIndexed(context.ndcQuadMeshIndexCount, 0, 0);
                }
                else if (lightings[i].LightingType == LightingType.Point)
                {
                    graphicsContext.SetRTV(context.outputRTV, Vector4.Zero, false);
                    lightingBuffers.SetCBVR(graphicsContext, i * 2, 1);
                    graphicsContext.SetCBVR(context.CameraDataBuffers[cameraIndex], 2);
                    graphicsContext.SetSRVT(context.ScreenSizeRenderTextures[0], 3);
                    graphicsContext.SetSRVT(context.ScreenSizeRenderTextures[1], 4);
                    graphicsContext.SetSRVT(context.ShadowMapCube, 5);
                    graphicsContext.SetSRVT(context.ScreenSizeDSVs[0], 6);
                    graphicsContext.SetMesh(context.cubeMesh);

                    int variant = context.RPAssetsManager.PObjectDeferredRenderPointLight.GetVariantIndex(context.deviceResources, context.RPAssetsManager.rootSignature, desc1);
                    graphicsContext.SetPObject1(context.RPAssetsManager.PObjectDeferredRenderPointLight, variant);

                    graphicsContext.DrawIndexed(context.cubeMeshIndexCount, 0, 0);
                }
            }

            #region forward
            graphicsContext.SetCBVR(context.CameraDataBuffers[cameraIndex], 2);
            graphicsContext.SetMesh(context.SkinningMeshBuffer);

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
            graphicsContext.SetSRVTArray(context.ShadowMapCube, 5);
            graphicsContext.SetSRVT(context.EnvironmentMap, 6);
            graphicsContext.SetSRVT(context.IrradianceMap, 7);
            graphicsContext.SetSRVT(context.BRDFLut, 8);

            graphicsContext.SetRTVDSV(context.outputRTV, context.ScreenSizeDSVs[0], Vector4.Zero, false, false);
            void _RenderEntity2(MMDRendererComponent rendererComponent, CBuffer cameraPresentData, CBuffer entityBoneDataBuffer, ref _Counters counter)
            {
                var PODraw = PObjectStatusSelect(rendererComponent.PODraw, context.RPAssetsManager.PObjectMMDLoading, context.RPAssetsManager.PObjectMMDTransparent, context.RPAssetsManager.PObjectMMDError);
                var Materials = rendererComponent.Materials;
                List<Texture2D> texs = rendererComponent.textures;
                graphicsContext.SetMeshIndex(rendererComponent.mesh);
                graphicsContext.SetCBVR(entityBoneDataBuffer, 0);
                graphicsContext.SetCBVR(cameraPresentData, 2);
                //CooGExtension.SetCBVBuffer3(graphicsContext, entityBoneDataBuffer, entityDataBuffer, cameraPresentData, 0);
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
                    int variant = PODraw.GetVariantIndex(context.deviceResources, context.RPAssetsManager.rootSignature, desc);
                    graphicsContext.SetPObject1(PODraw, variant);

                    graphicsContext.DrawIndexed(Materials[i].indexCount, countIndexLocal, counter.vertex);
                    counter.material++;
                    countIndexLocal += Materials[i].indexCount;
                }
                counter.vertex += rendererComponent.meshVertexCount;
            }
            _Counters counter3 = new _Counters();
            for (int i = 0; i < Entities.Count; i++)
                _RenderEntity2(Entities[i].rendererComponent, context.CameraDataBuffers[cameraIndex], context.CBs_Bone[i], ref counter3);
            #endregion
        }
    }
}
