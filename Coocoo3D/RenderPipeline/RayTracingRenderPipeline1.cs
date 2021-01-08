using Coocoo3D.Components;
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
    public class RayTracingRenderPipeline1 : RenderPipeline
    {
        public const int c_tempDataSize = 512;
        public const int c_entityDataDataSize = 128;
        const int c_materialDataSize = 512;
        const int c_presentDataSize = 512;
        const int c_lightCameraDataSize = 256;

        struct _Counters
        {
            public int material;
            public int vertex;
        }

        static readonly RayTracingSceneSettings c_rayTracingSceneSettings = new RayTracingSceneSettings()
        {
            payloadSize = 32,
            attributeSize = 8,
            maxRecursionDepth = 5,
            rayTypeCount = 2,
        };

        RayTracingScene RayTracingScene = new RayTracingScene();
        Random randomGenerator = new Random();

        public PresentData[] cameraPresentDatas = new PresentData[c_maxCameraPerRender];
        public ConstantBufferStatic[] CameraDataBuffers = new ConstantBufferStatic[c_maxCameraPerRender];
        public ConstantBuffer LightCameraDataBuffer = new ConstantBuffer();
        public List<ConstantBufferStatic> materialBuffers = new List<ConstantBufferStatic>();

        public RayTracingRenderPipeline1()
        {
            for (int i = 0; i < c_maxCameraPerRender; i++)
            {
                CameraDataBuffers[i] = new ConstantBufferStatic();
            }
        }

        public void Reload(DeviceResources deviceResources)
        {
            for (int i = 0; i < c_maxCameraPerRender; i++)
            {
                CameraDataBuffers[i].Reload(deviceResources, c_presentDataSize);
            }
            LightCameraDataBuffer.Reload(deviceResources, c_lightCameraDataSize);
        }

        #region graphics assets
        static readonly string[] c_rayGenShaderNames = { "MyRaygenShader", "MyRaygenShader1" };
        static readonly string[] c_missShaderNames = { "MissShaderSurface", "MissShaderTest", };
        static readonly string[] c_hitGroupNames = new string[] { "HitGroupSurface", "HitGroupTest", };
        static readonly HitGroupDesc[] hitGroupDescs = new HitGroupDesc[]
        {
            new HitGroupDesc { HitGroupName = "HitGroupSurface", AnyHitName = "AnyHitShaderSurface", ClosestHitName = "ClosestHitShaderSurface" },
            new HitGroupDesc { HitGroupName = "HitGroupTest", AnyHitName = "AnyHitShaderTest", ClosestHitName = "ClosestHitShaderTest" },
        };
        static readonly string[] c_exportNames = new string[] { "MyRaygenShader", "MyRaygenShader1", "ClosestHitShaderSurface", "ClosestHitShaderTest", "MissShaderSurface", "MissShaderTest", "AnyHitShaderSurface", "AnyHitShaderTest", };

        public async Task ReloadAssets(DeviceResources deviceResources)
        {
            RayTracingScene.ReloadLibrary(await ReadFile("ms-appx:///Coocoo3DGraphics/Raytracing.cso"));
            RayTracingScene.ReloadPipelineStates(deviceResources, c_exportNames, hitGroupDescs, c_rayTracingSceneSettings);
            RayTracingScene.ReloadAllocScratchAndInstance(deviceResources, 1024 * 1024 * 64, 1024);
            Ready = true;
        }
        #endregion


        bool HasMainLight;
        int renderMatCount = 0;
        public override void PrepareRenderData(RenderPipelineContext context)
        {
            var Entities = context.dynamicContextRead.entities;
            var deviceResources = context.deviceResources;
            int countMaterials = 0;
            for (int i = 0; i < Entities.Count; i++)
            {
                countMaterials += Entities[i].rendererComponent.Materials.Count;
            }
            DesireMaterialBuffers(deviceResources, countMaterials);
            var graphicsContext = context.graphicsContext;
            var cameras = context.dynamicContextRead.cameras;
            var camera = context.dynamicContextRead.cameras[0];
            ref var settings = ref context.dynamicContextRead.settings;
            ref var inShaderSettings = ref context.dynamicContextRead.inShaderSettings;
            var lightings = context.dynamicContextRead.lightings;

            IntPtr pBufferData = Marshal.UnsafeAddrOfPinnedArrayElement(context.bigBuffer, 0);
            Matrix4x4 lightCameraMatrix = Matrix4x4.Identity;
            HasMainLight = false;
            if (lightings.Count > 0 && lightings[0].LightingType == LightingType.Directional)
            {
                lightCameraMatrix = Matrix4x4.Transpose(lightings[0].GetLightingMatrix(256, camera.LookAtPoint - camera.Pos, camera.Distance));
                Marshal.StructureToPtr(lightCameraMatrix, pBufferData, true);
                graphicsContext.UpdateResource(LightCameraDataBuffer, context.bigBuffer, c_lightCameraDataSize, 0);
                HasMainLight = true;
            }

            for (int i = 0; i < cameras.Count; i++)
            {
                cameraPresentDatas[i].PlayTime = (float)context.dynamicContextRead.Time;
                cameraPresentDatas[i].DeltaTime = (float)context.dynamicContextRead.DeltaTime;

                cameraPresentDatas[i].UpdateCameraData(cameras[i]);
                cameraPresentDatas[i].RandomValue1 = randomGenerator.Next(int.MinValue, int.MaxValue);
                cameraPresentDatas[i].RandomValue2 = randomGenerator.Next(int.MinValue, int.MaxValue);
                cameraPresentDatas[i].inShaderSettings = inShaderSettings;
                Marshal.StructureToPtr(cameraPresentDatas[i], pBufferData, true);
                Marshal.StructureToPtr(lightCameraMatrix, pBufferData + 256, true);
                graphicsContext.UpdateResource(CameraDataBuffers[i], context.bigBuffer, c_presentDataSize, 0);
            }


            #region Update material data

            void WriteLightData(IList<LightingData> lightings1, IntPtr pBufferData1)
            {
                int lightCount1 = 0;
                for (int j = 0; j < lightings1.Count; j++)
                {
                    Marshal.StructureToPtr(lightings1[j].GetPositionOrDirection(camera.Pos), pBufferData1, true);
                    Marshal.StructureToPtr((uint)lightings1[j].LightingType, pBufferData1 + 12, true);
                    Marshal.StructureToPtr(lightings1[j].Color, pBufferData1 + 16, true);
                    lightCount1++;
                    pBufferData1 += 32;
                    if (lightCount1 >= 8)
                        break;
                }
            }
            _Counters counterMaterial = new _Counters();
            for (int i = 0; i < Entities.Count; i++)
            {
                var Materials = Entities[i].rendererComponent.Materials;
                for (int j = 0; j < Materials.Count; j++)
                {
                    Array.Clear(context.bigBuffer, 0, c_materialDataSize);
                    Marshal.StructureToPtr(Materials[j].innerStruct, pBufferData, true);
                    Marshal.StructureToPtr(counterMaterial.vertex, pBufferData + 240, true);
                    WriteLightData(lightings, pBufferData + RuntimeMaterial.c_materialDataSize);
                    graphicsContext.UpdateResource(materialBuffers[counterMaterial.material], context.bigBuffer, c_materialDataSize, 0);
                    counterMaterial.material++;
                }
                counterMaterial.vertex += Entities[i].rendererComponent.meshVertexCount;
            }
            #endregion
            renderMatCount = counterMaterial.material;
        }

        public override void RenderCamera(RenderPipelineContext context)
        {
            var graphicsContext = context.graphicsContext;

            RayTracingScene.NextASIndex(renderMatCount);
            RayTracingScene.NextSTIndex();


            var entities = context.dynamicContextRead.entities;
            graphicsContext.SetRootSignature(context.RPAssetsManager.rootSignatureSkinning);
            graphicsContext.SetSOMesh(context.SkinningMeshBuffer);


            void EntitySkinning(MMDRendererComponent rendererComponent, ConstantBufferStatic cameraPresentData, ConstantBuffer entityBoneDataBuffer)
            {
                var Materials = rendererComponent.Materials;
                graphicsContext.SetCBVR(entityBoneDataBuffer, 0);
                //graphicsContext.SetCBVR(entityDataBuffer, 1);
                graphicsContext.SetCBVR(cameraPresentData, 2);
                var POSkinning = rendererComponent.POSkinning;
                if (POSkinning != null && POSkinning.Status == GraphicsObjectStatus.loaded)
                    graphicsContext.SetPObjectStreamOut(POSkinning);
                else
                    graphicsContext.SetPObjectStreamOut(context.RPAssetsManager.PObjectMMDSkinning);
                graphicsContext.SetMeshVertex1(rendererComponent.mesh);
                graphicsContext.SetMeshVertex(rendererComponent.meshAppend);
                int indexCountAll = rendererComponent.meshVertexCount;
                graphicsContext.Draw(indexCountAll, 0);
            }
            for (int i = 0; i < entities.Count; i++)
                EntitySkinning(entities[i].rendererComponent, CameraDataBuffers[0], context.CBs_Bone[i]);
            graphicsContext.SetSOMeshNone();

            graphicsContext.SetRootSignatureCompute(context.RPAssetsManager.rootSignatureCompute);

            void ParticleCompute(MMDRendererComponent rendererComponent, ConstantBufferStatic cameraPresentData, ConstantBuffer entityBoneDataBuffer, ConstantBuffer entityDataBuffer, ref _Counters counter)
            {
                if (rendererComponent.ParticleCompute == null || rendererComponent.meshParticleBuffer == null || rendererComponent.ParticleCompute.Status != GraphicsObjectStatus.loaded)
                {
                    counter.vertex += rendererComponent.meshIndexCount;
                    return;
                }
                graphicsContext.SetComputeCBVR(entityBoneDataBuffer, 0);
                //graphicsContext.SetComputeCBVR(entityDataBuffer, 1);
                graphicsContext.SetComputeCBVR(cameraPresentData, 2);
                graphicsContext.SetComputeUAVR(context.SkinningMeshBuffer, counter.vertex, 4);
                graphicsContext.SetComputeUAVR(rendererComponent.meshParticleBuffer, 0, 5);
                graphicsContext.SetPObject(rendererComponent.ParticleCompute);
                graphicsContext.Dispatch((rendererComponent.meshVertexCount + 63) / 64, 1, 1);
                counter.vertex += rendererComponent.meshIndexCount;
            }
            _Counters counterParticle = new _Counters();
            for (int i = 0; i < entities.Count; i++)
                ParticleCompute(entities[i].rendererComponent, CameraDataBuffers[0], context.CBs_Bone[i], null, ref counterParticle);

            if (HasMainLight && context.dynamicContextRead.inShaderSettings.EnableShadow)
            {
                graphicsContext.SetRootSignature(context.RPAssetsManager.rootSignature);
                graphicsContext.SetDSV(context.ShadowMapCube, 0, true);
                graphicsContext.SetMesh(context.SkinningMeshBuffer);

                void RenderEntityShadow(MMDRendererComponent rendererComponent, ConstantBuffer cameraPresentData, ref _Counters counter)
                {
                    Texture2D textureLoading = context.TextureLoading;
                    Texture2D textureError = context.TextureError;
                    var Materials = rendererComponent.Materials;
                    //graphicsContext.SetCBVR(entityBoneDataBuffer, 0);
                    //graphicsContext.SetCBVR(entityDataBuffer, 1);
                    graphicsContext.SetCBVR(cameraPresentData, 2);

                    graphicsContext.SetMeshIndex(rendererComponent.mesh);
                    graphicsContext.SetPObject(context.RPAssetsManager.PObjectMMDShadowDepth, CullMode.none);

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
                _Counters counterShadow = new _Counters();
                for (int i = 0; i < entities.Count; i++)
                    RenderEntityShadow(entities[i].rendererComponent, LightCameraDataBuffer, ref counterShadow);
            }


            if (entities.Count > 0)
            {
                void BuildEntityBAS1(MMDRendererComponent rendererComponent, ref _Counters counter)
                {
                    Texture2D textureLoading = context.TextureLoading;
                    Texture2D textureError = context.TextureError;

                    var Materials = rendererComponent.Materials;
                    List<Texture2D> texs = rendererComponent.textures;

                    int countIndexLocal = 0;
                    for (int i = 0; i < Materials.Count; i++)
                    {
                        Texture2D tex1 = null;
                        if (Materials[i].texIndex != -1)
                            tex1 = texs[Materials[i].texIndex];
                        tex1 = TextureStatusSelect(tex1, textureLoading, textureError, textureError);

                        graphicsContext.BuildBASAndParam(RayTracingScene, context.SkinningMeshBuffer, rendererComponent.mesh, 0x1, counter.vertex, countIndexLocal, Materials[i].indexCount, tex1, materialBuffers[counter.material]);
                        counter.material++;
                        countIndexLocal += Materials[i].indexCount;
                    }
                    counter.vertex += rendererComponent.meshVertexCount;
                }
                _Counters counter1 = new _Counters();
                for (int i = 0; i < entities.Count; i++)
                {
                    BuildEntityBAS1(entities[i].rendererComponent, ref counter1);
                }
                graphicsContext.BuildTopAccelerationStructures(RayTracingScene);
                RayTracingScene.BuildShaderTable(context.deviceResources, c_rayGenShaderNames, c_missShaderNames, c_hitGroupNames, counter1.material);
                int cameraIndex = 0;
                graphicsContext.SetRootSignatureRayTracing(RayTracingScene);
                graphicsContext.SetComputeUAVT(context.outputRTV, 0);
                graphicsContext.SetComputeCBVR(CameraDataBuffers[cameraIndex], 2);
                graphicsContext.SetComputeSRVT(context.SkyBox, 3);
                graphicsContext.SetComputeSRVT(context.IrradianceMap, 4);
                graphicsContext.SetComputeSRVT(context.BRDFLut, 5);
                graphicsContext.SetSRVTFace(context.ShadowMapCube, 0, 6);
                graphicsContext.SetComputeSRVR(context.SkinningMeshBuffer, 0, 7);
                graphicsContext.SetComputeUAVR(context.LightCacheBuffer, context.dynamicContextRead.frameRenderIndex % 2, 8);
                graphicsContext.SetComputeSRVR(context.LightCacheBuffer, (context.dynamicContextRead.frameRenderIndex + 1) % 2, 9);

                graphicsContext.DoRayTracing(RayTracingScene, context.dynamicContextRead.VertexCount, 1, 1);

                graphicsContext.SetComputeUAVR(context.LightCacheBuffer, (context.dynamicContextRead.frameRenderIndex + 1) % 2, 8);
                graphicsContext.SetComputeSRVR(context.LightCacheBuffer, context.dynamicContextRead.frameRenderIndex % 2, 9);
                graphicsContext.DoRayTracing(RayTracingScene, context.screenWidth, context.screenHeight, 0);
            }
            else
            {
                #region Render Sky box
                int cameraIndex = 0;
                graphicsContext.SetRootSignature(context.RPAssetsManager.rootSignature);
                graphicsContext.SetRTVDSV(context.outputRTV, context.ScreenSizeDSVs[0], Vector4.Zero, true, true);
                graphicsContext.SetCBVR(CameraDataBuffers[cameraIndex], 2);
                graphicsContext.SetSRVT(context.SkyBox, 6);
                graphicsContext.SetSRVT(context.IrradianceMap, 7);
                graphicsContext.SetSRVT(context.BRDFLut, 8);
                graphicsContext.SetPObject(context.RPAssetsManager.PObjectSkyBox, CullMode.back);
                graphicsContext.SetMesh(context.ndcQuadMesh);
                graphicsContext.DrawIndexed(context.ndcQuadMesh.m_indexCount, 0, 0);
                #endregion
            }
        }

        private void DesireMaterialBuffers(DeviceResources deviceResources, int count)
        {
            while (materialBuffers.Count < count)
            {
                ConstantBufferStatic constantBuffer = new ConstantBufferStatic();
                constantBuffer.Reload(deviceResources, c_materialDataSize);
                materialBuffers.Add(constantBuffer);
            }
        }
    }
}
