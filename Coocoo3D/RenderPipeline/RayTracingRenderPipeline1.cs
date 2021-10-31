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
using Coocoo3D.RenderPipeline.Wrap;
using Coocoo3D.Utility;

namespace Coocoo3D.RenderPipeline
{
    public class RayTracingRenderPipeline1 : RenderPipeline
    {
        const int c_materialDataSize = 512;
        const int c_presentDataSize = 512;

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
        public Dictionary<string, RayTracingScene> rayTracingScenes = new Dictionary<string, RayTracingScene>();
        //RayTracingScene RayTracingScene = new RayTracingScene();
        [ThreadStatic]
        static Random random;

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

        Windows.Storage.Streams.IBuffer rayTracingPso;

        public async Task ReloadAssets(RenderPipelineContext context)
        {
            rayTracingPso = await ReadFile("ms-appx:///Coocoo3DGraphics/Raytracing.cso");
            Ready = true;
        }
        #endregion

        public override void PrepareRenderData(RenderPipelineContext context, VisualChannel visualChannel)
        {
            if (random == null)
                random = new Random();

            var graphicsContext = visualChannel.graphicsContext;
            var rendererComponents = context.dynamicContextRead.renderers;
            var graphicsDevice = context.graphicsDevice;
            var sBufferGroup = visualChannel.XSBufferGroup;
            int countMaterials = 0;
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                countMaterials += rendererComponents[i].Materials.Count;
            }
            visualChannel.XSBufferGroup.SetSlienceCount(countMaterials + 1);

            var camera = visualChannel.cameraData;
            ref var settings = ref context.dynamicContextRead.settings;
            var lightings = context.dynamicContextRead.lightings;

            int ofs = 0;
            ofs += CooUtility.Write(context.bigBuffer, ofs, Matrix4x4.Transpose(camera.vpMatrix));
            ofs += CooUtility.Write(context.bigBuffer, ofs, Matrix4x4.Transpose(camera.pvMatrix));
            ofs += CooUtility.Write(context.bigBuffer, ofs, camera.Pos);
            ofs += CooUtility.Write(context.bigBuffer, ofs, settings.SkyBoxLightMultiplier);
            ofs += CooUtility.Write(context.bigBuffer, ofs, settings.EnableAO ? 1 : 0);
            ofs += CooUtility.Write(context.bigBuffer, ofs, settings.EnableShadow ? 1 : 0);
            ofs += CooUtility.Write(context.bigBuffer, ofs, settings.Quality);
            ofs += CooUtility.Write(context.bigBuffer, ofs, camera.AspectRatio);
            ofs += CooUtility.Write(context.bigBuffer, ofs, random.Next(int.MinValue, int.MaxValue));
            ofs += CooUtility.Write(context.bigBuffer, ofs, random.Next(int.MinValue, int.MaxValue));

            int countBufferIndex = 0;
            sBufferGroup.UpdateSlience(graphicsContext, context.bigBuffer, 0, c_presentDataSize, 0);
            countBufferIndex++;

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

            IntPtr pBufferData = Marshal.UnsafeAddrOfPinnedArrayElement(context.bigBuffer, 0);
            _Counters counterMaterial = new _Counters();
            for (int i = 0; i < rendererComponents.Count; i++)
            {
                var Materials = rendererComponents[i].Materials;
                for (int j = 0; j < Materials.Count; j++)
                {
                    int ofs1 = 0;
                    Array.Clear(context.bigBuffer, 0, c_materialDataSize);
                    //Marshal.StructureToPtr(Materials[j].innerStruct, pBufferData, true);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.DiffuseColor);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.SpecularColor);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.AmbientColor);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.EdgeSize);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.EdgeColor);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.Texture);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.SubTexture);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.ToonTexture);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.IsTransparent);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.Metallic);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.Roughness);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.Emission);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.Subsurface);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.Specular);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.SpecularTint);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.Anisotropic);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.Sheen);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.SheenTint);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.Clearcoat);
                    ofs1 += CooUtility.Write(context.bigBuffer, ofs1, Materials[j].innerStruct.ClearcoatGloss);


                    CooUtility.Write(context.bigBuffer, 240, counterMaterial.vertex);
                    WriteLightData(lightings, pBufferData + RuntimeMaterial.c_materialDataSize);
                    sBufferGroup.UpdateSlience(graphicsContext, context.bigBuffer, 0, c_materialDataSize, countBufferIndex);
                    countBufferIndex++;
                    counterMaterial.material++;
                }
                counterMaterial.vertex += rendererComponents[i].meshVertexCount;
            }
            #endregion

            visualChannel.customDataInt["renderMatCount"] = counterMaterial.material;
            //if (visualChannel.customDataInt["renderMatCount"] > 0)
            sBufferGroup.UpdateSlienceComplete(graphicsContext);
        }

        public override void RenderCamera(RenderPipelineContext context, VisualChannel visualChannel)
        {
            var graphicsContext = visualChannel.graphicsContext;
            var RPAssetsManager = context.RPAssetsManager;

            var rendererComponents = context.dynamicContextRead.renderers;
            var sBufferGroup = visualChannel.XSBufferGroup;

            var graphicsDevice = context.graphicsDevice;
            bool loaded = rayTracingScenes.TryGetValue(visualChannel.Name, out var rayTracingScene);
            if (!loaded)
            {
                rayTracingScene = new RayTracingScene();
                rayTracingScenes[visualChannel.Name] = rayTracingScene;
                rayTracingScene.ReloadLibrary(rayTracingPso);
                rayTracingScene.ReloadPipelineStates(graphicsDevice, context.RPAssetsManager.rtGlobal, context.RPAssetsManager.rtLocal, c_exportNames, hitGroupDescs, c_rayTracingSceneSettings);
                rayTracingScene.ReloadAllocScratchAndInstance(graphicsDevice, 1024 * 1024 * 64, 1024);
            }

            int countBufferIndex = 1;// use 1 for camera
            if (rendererComponents.Count > 0)
            {
                graphicsContext.Prepare(rayTracingScene, visualChannel.customDataInt["renderMatCount"]);
                void BuildEntityBAS1(MMDRendererComponent rendererComponent, ref _Counters counter)
                {
                    Texture2D texLoading = context.TextureLoading;
                    Texture2D texError = context.TextureError;

                    var Materials = rendererComponent.Materials;

                    int numIndex = 0;
                    foreach (RuntimeMaterial material in Materials)
                    {
                        Texture2D tex = null;
                        if (material.textures.TryGetValue("_Albedo", out string texPath) && context.mainCaches.TextureCaches.TryGetValue(texPath, out var texpack))
                        {
                            tex = texpack.texture2D;
                        }
                        tex = TextureStatusSelect(tex, texLoading, texError, texError);

                        graphicsContext.BuildBASAndParam(rayTracingScene, context.SkinningMeshBuffer, context.GetMesh(rendererComponent.meshPath), 0x1, counter.vertex, numIndex, material.indexCount, tex,
                            sBufferGroup.constantBuffers[countBufferIndex / sBufferGroup.sliencesPerBuffer], (countBufferIndex % sBufferGroup.sliencesPerBuffer) * 2);
                        counter.material++;
                        countBufferIndex++;
                        numIndex += material.indexCount;
                    }
                    counter.vertex += rendererComponent.meshVertexCount;
                }
                _Counters counter1 = new _Counters();
                for (int i = 0; i < rendererComponents.Count; i++)
                {
                    BuildEntityBAS1(rendererComponents[i], ref counter1);
                }
                graphicsContext.BuildTopAccelerationStructures(rayTracingScene);
                graphicsContext.BuildShaderTable(rayTracingScene, c_rayGenShaderNames, c_missShaderNames, c_hitGroupNames, counter1.material);
                graphicsContext.SetRootSignatureRayTracing(rayTracingScene);
                graphicsContext.SetComputeUAVT(visualChannel.OutputRTV, 0);
                sBufferGroup.SetComputeCBVR(graphicsContext, 0, 2);
                graphicsContext.SetComputeSRVT(context.SkyBox, 3);
                graphicsContext.SetComputeSRVT(context.IrradianceMap, 4);
                graphicsContext.SetComputeSRVT(context.mainCaches.GetTexture("_BRDFLUT"), 5);

                graphicsContext.DoRayTracing(rayTracingScene, visualChannel.outputSize.X, visualChannel.outputSize.Y, 0);
            }
            else
            {
                var rootSignature = RPAssetsManager.GetRootSignature(context.graphicsDevice, "Cssss");
                #region Render Sky box
                graphicsContext.SetRootSignature(rootSignature);
                graphicsContext.SetRTV(visualChannel.OutputRTV, Vector4.Zero, true);
                sBufferGroup.SetCBVRSlot(graphicsContext, 0, 0);//camera
                graphicsContext.SetSRVTSlot(context.SkyBox, 3);
                graphicsContext.SetMesh(context.ndcQuadMesh);
                PSODesc descSkyBox;
                descSkyBox.blendState = BlendState.none;
                descSkyBox.cullMode = CullMode.back;
                descSkyBox.depthBias = 0;
                descSkyBox.slopeScaledDepthBias = 1.0f;
                descSkyBox.dsvFormat = Format.Unknown;
                descSkyBox.inputLayout = InputLayout.postProcess;
                descSkyBox.ptt = PrimitiveTopologyType.Triangle;
                descSkyBox.rtvFormat = context.outputFormat;
                descSkyBox.renderTargetCount = 1;
                descSkyBox.streamOutput = false;
                descSkyBox.wireFrame = false;
                SetPipelineStateVariant(context.graphicsDevice, graphicsContext, rootSignature, descSkyBox, RPAssetsManager.PSOs["SkyBox"]);

                graphicsContext.DrawIndexed(context.ndcQuadMesh.GetIndexCount(), 0, 0);
                #endregion
            }
        }
    }
}
