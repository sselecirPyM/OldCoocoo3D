﻿using Coocoo3D.Components;
using Coocoo3D.Present;
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
    public class RayTracingRenderPipeline1 : RenderPipeline
    {
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

        public SBuffer CameraDataBuffer = new SBuffer();
        public CBuffer LightCameraDataBuffer = new CBuffer();
        SBufferGroup materialBuffers1 = new SBufferGroup();

        public RayTracingRenderPipeline1()
        {
            materialBuffers1.Reload(c_materialDataSize, 65536);
        }

        public void Reload(DeviceResources deviceResources)
        {
            deviceResources.InitializeSBuffer(CameraDataBuffer, c_presentDataSize);
            deviceResources.InitializeCBuffer(LightCameraDataBuffer, c_lightCameraDataSize);
        }

        #region graphics assets
        static readonly string[] c_rayGenShaderNames = { "MyRaygenShader", };
        static readonly string[] c_missShaderNames = { "MissShaderSurface", "MissShaderTest", };
        static readonly string[] c_hitGroupNames = new string[] { "HitGroupSurface", "HitGroupTest", };
        static readonly HitGroupDesc[] hitGroupDescs = new HitGroupDesc[]
        {
            new HitGroupDesc { HitGroupName = "HitGroupSurface", AnyHitName = "AnyHitShaderSurface", ClosestHitName = "ClosestHitShaderSurface" },
            new HitGroupDesc { HitGroupName = "HitGroupTest", AnyHitName = "AnyHitShaderTest", ClosestHitName = "ClosestHitShaderTest" },
        };
        static readonly string[] c_exportNames = new string[] { "MyRaygenShader", "ClosestHitShaderSurface", "ClosestHitShaderTest", "MissShaderSurface", "MissShaderTest", "AnyHitShaderSurface", "AnyHitShaderTest", };

        public async Task ReloadAssets(RenderPipelineContext context)
        {
            DeviceResources deviceResources = context.deviceResources;
            RayTracingScene.ReloadLibrary(await ReadFile("ms-appx:///Coocoo3DGraphics/Raytracing.cso"));
            RayTracingScene.ReloadPipelineStates(deviceResources, context.RPAssetsManager.rtGlobal, context.RPAssetsManager.rtLocal, c_exportNames, hitGroupDescs, c_rayTracingSceneSettings);
            RayTracingScene.ReloadAllocScratchAndInstance(deviceResources, 1024 * 1024 * 64, 1024);
            Ready = true;
        }
        #endregion


        bool HasMainLight;
        int renderMatCount = 0;
        public override void PrepareRenderData(RenderPipelineContext context, GraphicsContext graphicsContext)
        {
            var rendererComponents = context.dynamicContextRead.renderers;
            var deviceResources = context.deviceResources;
            int countMaterials = 0;
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                countMaterials += rendererComponents[i].Materials.Count;
            }
            DesireMaterialBuffers(deviceResources, countMaterials);
            var cameras = context.dynamicContextRead.cameras;
            var camera = context.dynamicContextRead.cameras[0];
            ref var settings = ref context.dynamicContextRead.settings;
            var lightings = context.dynamicContextRead.lightings;

            IntPtr pBufferData = Marshal.UnsafeAddrOfPinnedArrayElement(context.bigBuffer, 0);

            int ofs = 0;
            ofs += CooUtility.Write(context.bigBuffer, ofs, Matrix4x4.Transpose(camera.vpMatrix));
            ofs += CooUtility.Write(context.bigBuffer, ofs, Matrix4x4.Transpose(camera.pvMatrix));
            ofs += CooUtility.Write(context.bigBuffer, ofs, camera.Pos);
            ofs += CooUtility.Write(context.bigBuffer, ofs, settings.SkyBoxLightMultiplier);
            ofs += CooUtility.Write(context.bigBuffer, ofs, settings.EnableAO ? 1 : 0);
            ofs += CooUtility.Write(context.bigBuffer, ofs, settings.EnableShadow ? 1 : 0);
            ofs += CooUtility.Write(context.bigBuffer, ofs, settings.Quality);
            ofs += CooUtility.Write(context.bigBuffer, ofs, camera.AspectRatio);
            ofs += CooUtility.Write(context.bigBuffer, ofs, randomGenerator.Next(int.MinValue, int.MaxValue));
            ofs += CooUtility.Write(context.bigBuffer, ofs, randomGenerator.Next(int.MinValue, int.MaxValue));

            graphicsContext.UpdateResource(CameraDataBuffer, context.bigBuffer, c_presentDataSize, 0);


            #region Update material data

            void WriteLightData(IList<LightingData> lightings1, IntPtr pBufferData1)
            {
                int lightCount1 = 0;
                for (int j = 0; j < lightings1.Count; j++)
                {
                    Marshal.StructureToPtr(lightings1[j].GetPositionOrDirection(), pBufferData1, true);
                    Marshal.StructureToPtr((uint)lightings1[j].LightingType, pBufferData1 + 12, true);
                    Marshal.StructureToPtr(lightings1[j].Color, pBufferData1 + 16, true);
                    lightCount1++;
                    pBufferData1 += 32;
                    if (lightCount1 >= 8)
                        break;
                }
            }
            _Counters counterMaterial = new _Counters();
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                var Materials = rendererComponents[i].Materials;
                for (int j = 0; j < Materials.Count; j++)
                {
                    Array.Clear(context.bigBuffer, 0, c_materialDataSize);
                    Marshal.StructureToPtr(Materials[j].innerStruct, pBufferData, true);
                    Marshal.StructureToPtr(counterMaterial.vertex, pBufferData + 240, true);
                    WriteLightData(lightings, pBufferData + RuntimeMaterial.c_materialDataSize);
                    materialBuffers1.UpdateSlience(graphicsContext, context.bigBuffer, 0, c_materialDataSize, counterMaterial.material);
                    counterMaterial.material++;
                }
                counterMaterial.vertex += rendererComponents[i].meshVertexCount;
            }
            #endregion
            renderMatCount = counterMaterial.material;
            if (renderMatCount > 0)
                materialBuffers1.UpdateSlienceComplete(graphicsContext);
        }

        public override void RenderCamera(RenderPipelineContext context, GraphicsContext graphicsContext)
        {
            var RPAssetsManager = context.RPAssetsManager;

            var rendererComponents = context.dynamicContextRead.renderers;
            graphicsContext.SetRootSignature(RPAssetsManager.rootSignatureSkinning);
            graphicsContext.SetSOMesh(context.SkinningMeshBuffer);
            //var shadowDepth = RPAssetsManager.PSOs["PSOMMDShadowDepth"];
            var PSOSkinning = RPAssetsManager.PSOs["PSOMMDSkinning"];

            void EntitySkinning(MMDRendererComponent rendererComponent, CBuffer entityBoneDataBuffer)
            {
                var Materials = rendererComponent.Materials;
                graphicsContext.SetCBVR(entityBoneDataBuffer, 0);
                rendererComponent.shaders.TryGetValue("Skinning", out var shaderSkinning);
                SetPipelineStateVariant(context.deviceResources, graphicsContext, RPAssetsManager.rootSignatureSkinning, ref context.SkinningDesc, PSOSkinning);
                graphicsContext.SetMeshVertex1(rendererComponent.mesh);
                graphicsContext.SetMeshVertex(rendererComponent.meshAppend);
                int indexCountAll = rendererComponent.meshVertexCount;
                graphicsContext.Draw(indexCountAll, 0);
            }
            for (int i = 0; i < rendererComponents.Count; i++)
                EntitySkinning(rendererComponents[i], context.CBs_Bone[i]);
            graphicsContext.SetSOMeshNone();

            graphicsContext.SetRootSignatureCompute(RPAssetsManager.rootSignatureCompute);

            //if (HasMainLight && context.dynamicContextRead.inShaderSettings.EnableShadow)
            //{
            //    graphicsContext.SetRootSignature(RPAssetsManager.rootSignature);
            //    graphicsContext.SetDSV(context.ShadowMapCube, 0, true);
            //    graphicsContext.SetMesh(context.SkinningMeshBuffer);

            //    void RenderEntityShadow(MMDRendererComponent rendererComponent, CBuffer cameraPresentData, ref _Counters counter)
            //    {
            //        Texture2D texLoading = context.TextureLoading;
            //        Texture2D texError = context.TextureError;
            //        var Materials = rendererComponent.Materials;
            //        //graphicsContext.SetCBVR(entityBoneDataBuffer, 0);
            //        //graphicsContext.SetCBVR(entityDataBuffer, 1);
            //        graphicsContext.SetCBVR(cameraPresentData, 2);

            //        graphicsContext.SetMeshIndex(rendererComponent.mesh);
            //        SetPipelineStateVariant(context.deviceResources, graphicsContext, RPAssetsManager.rootSignature, ref context.shadowDesc, shadowDepth);
            //        //List<Texture2D> texs = rendererComponent.textures;
            //        //int countIndexLocal = 0;
            //        //for (int i = 0; i < Materials.Count; i++)
            //        //{
            //        //    if (Materials[i].DrawFlags.HasFlag(DrawFlag.CastSelfShadow))
            //        //    {
            //        //        Texture2D tex1 = null;
            //        //        if (Materials[i].texIndex != -1)
            //        //            tex1 = texs[Materials[i].texIndex];
            //        //        graphicsContext.SetCBVR(materialBuffers[counter.material], 3);
            //        //        graphicsContext.SetSRVT(TextureStatusSelect(tex1, textureLoading, textureError, textureError), 4);
            //        //        graphicsContext.DrawIndexed(Materials[i].indexCount, countIndexLocal, counter.vertex);
            //        //    }
            //        //    counter.material++;
            //        //    countIndexLocal += Materials[i].indexCount;
            //        //}
            //        graphicsContext.DrawIndexed(rendererComponent.meshIndexCount, 0, counter.vertex);

            //        counter.vertex += rendererComponent.meshVertexCount;
            //    }
            //    _Counters counterShadow = new _Counters();
            //    for (int i = 0; i < rendererComponents.Count; i++)
            //        RenderEntityShadow(rendererComponents[i], LightCameraDataBuffer, ref counterShadow);
            //}


            if (rendererComponents.Count > 0)
            {
                graphicsContext.Prepare(RayTracingScene, renderMatCount);
                void BuildEntityBAS1(MMDRendererComponent rendererComponent, ref _Counters counter)
                {
                    Texture2D texLoading = context.TextureLoading;
                    Texture2D texError = context.TextureError;

                    var Materials = rendererComponent.Materials;

                    int numIndex = 0;
                    foreach (RuntimeMaterial material in Materials)
                    {
                        material.textures.TryGetValue("_Albedo", out ITexture2D tex);
                        Texture2D tex1 = (Texture2D)tex;
                        tex1 = TextureStatusSelect(tex1, texLoading, texError, texError);

                        graphicsContext.BuildBASAndParam(RayTracingScene, context.SkinningMeshBuffer, rendererComponent.mesh, 0x1, counter.vertex, numIndex, material.indexCount, tex1,
                            materialBuffers1.constantBuffers[counter.material / materialBuffers1.sliencesPerBuffer], (counter.material % materialBuffers1.sliencesPerBuffer) * 2);
                        counter.material++;
                        numIndex += material.indexCount;
                    }
                    counter.vertex += rendererComponent.meshVertexCount;
                }
                _Counters counter1 = new _Counters();
                for (int i = 0; i < rendererComponents.Count; i++)
                {
                    BuildEntityBAS1(rendererComponents[i], ref counter1);
                }
                graphicsContext.BuildTopAccelerationStructures(RayTracingScene);
                graphicsContext.BuildShaderTable(RayTracingScene, c_rayGenShaderNames, c_missShaderNames, c_hitGroupNames, counter1.material);
                graphicsContext.SetRootSignatureRayTracing(RayTracingScene);
                graphicsContext.SetComputeUAVT(context.outputRTV, 0);
                graphicsContext.SetComputeCBVR(CameraDataBuffer, 2);
                graphicsContext.SetComputeSRVT(context.SkyBox, 3);
                graphicsContext.SetComputeSRVT(context.IrradianceMap, 4);
                graphicsContext.SetComputeSRVT(RPAssetsManager.texture2ds["_BRDFLUT"], 5);

                graphicsContext.DoRayTracing(RayTracingScene, context.screenWidth, context.screenHeight, 0);
            }
            else
            {
                var rootSignature = RPAssetsManager.GetRootSignature(context.deviceResources, "Cssss");
                #region Render Sky box
                graphicsContext.SetRootSignature(rootSignature);
                graphicsContext.SetRTV(context.outputRTV, Vector4.Zero, true);
                graphicsContext.SetCBVRSlot(CameraDataBuffer, 0, 0, 0);
                graphicsContext.SetSRVTSlot(context.SkyBox, 3);
                graphicsContext.SetMesh(context.ndcQuadMesh);
                PSODesc descSkyBox;
                descSkyBox.blendState = EBlendState.none;
                descSkyBox.cullMode = ECullMode.back;
                descSkyBox.depthBias = 0;
                descSkyBox.slopeScaledDepthBias = 1.0f;
                descSkyBox.dsvFormat = DxgiFormat.DXGI_FORMAT_UNKNOWN;
                descSkyBox.inputLayout = EInputLayout.postProcess;
                descSkyBox.ptt = ED3D12PrimitiveTopologyType.TRIANGLE;
                descSkyBox.rtvFormat = context.outputFormat;
                descSkyBox.renderTargetCount = 1;
                descSkyBox.streamOutput = false;
                descSkyBox.wireFrame = false;
                SetPipelineStateVariant(context.deviceResources, graphicsContext, rootSignature, ref descSkyBox, RPAssetsManager.PSOs["SkyBox"]);

                graphicsContext.DrawIndexed(context.ndcQuadMesh.GetIndexCount(), 0, 0);
                #endregion
            }
        }

        private void DesireMaterialBuffers(DeviceResources deviceResources, int count)
        {
            materialBuffers1.SetSlienceCount(deviceResources, count);
        }
    }
}
