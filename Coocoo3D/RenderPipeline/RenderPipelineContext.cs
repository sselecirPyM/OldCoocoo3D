using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline.Wrap;
using Coocoo3D.ResourceWarp;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Windows.Storage;
using Windows.Storage.Streams;
using PSO = Coocoo3DGraphics.PObject;

namespace Coocoo3D.RenderPipeline
{
    public struct RecordSettings
    {
        public float FPS;
        public float StartTime;
        public float StopTime;
        public int Width;
        public int Height;
    }
    public class GameDriverContext
    {
        public volatile bool NeedRender;
        public volatile bool EnableDisplay;
        public bool Playing;
        public double PlayTime;
        public double DeltaTime;
        public TimeSpan FrameInterval;
        public float PlaySpeed;
        public volatile bool RequireResetPhysics;
        public bool NeedReloadModel;
        public bool RequireResize;
        public bool RequireResizeOuter;
        public Windows.Foundation.Size NewSize;
        public float AspectRatio;
        public bool RequireInterruptRender;
        public WICFactory WICFactory = new WICFactory();
        public RecordSettings recordSettings;

        public DateTime LatestRenderTime;

        public void ReqireReloadModel()
        {
            NeedReloadModel = true;
            RequireInterruptRender = true;
            NeedRender = true;
        }

        public void RequireRender(bool updateEntities)
        {
            NeedRender = true;
        }

        public void RequireRender()
        {
            NeedRender = true;
        }
    }

    public class RenderPipelineContext
    {
        const int c_entityDataBufferSize = 65536;
        public byte[] bigBuffer = new byte[65536];
        GCHandle _bigBufferHandle;

        public const int c_presentDataSize = 256;
        public const int c_lightingBufferSize = 1024;
        public CBuffer CameraDataBuffers = new CBuffer();
        public CBuffer LightCameraDataBuffer = new CBuffer();
        public CBufferGroup MaterialBufferGroup = new CBufferGroup();
        public CBufferGroup XBufferGroup = new CBufferGroup();
        public void DesireMaterialBuffers(int count)
        {
            MaterialBufferGroup.SetSlienceCount(count);
        }

        public RenderTexture2D outputRTV = new RenderTexture2D();
        public RenderTexture2D[] ScreenSizeRenderTextures = new RenderTexture2D[4];
        public RenderTexture2D[] ScreenSizeDSVs = new RenderTexture2D[2];

        public RenderTextureCube ShadowMapCube = new RenderTextureCube();

        public Dictionary<string, RenderTexture2D> RTs = new Dictionary<string, RenderTexture2D>();

        public RayTracingASGroup RTASGroup = new RayTracingASGroup();
        public Dictionary<string, RayTracingShaderTable> RTSTs = new Dictionary<string, RayTracingShaderTable>();
        public Dictionary<string, RayTracingInstanceGroup> RTIGroups = new Dictionary<string, RayTracingInstanceGroup>();
        public Dictionary<string, RayTracingTopAS> RTTASs = new Dictionary<string, RayTracingTopAS>();

        public Texture2D TextureLoading = new Texture2D();
        public Texture2D TextureError = new Texture2D();
        public TextureCube SkyBox = new TextureCube();
        public RenderTextureCube IrradianceMap = new RenderTextureCube();
        public RenderTextureCube ReflectMap = new RenderTextureCube();

        public MMDMesh ndcQuadMesh = new MMDMesh();
        public MMDMesh cubeMesh = new MMDMesh();
        public MMDMesh cubeWireMesh = new MMDMesh();
        public MeshBuffer SkinningMeshBuffer = new MeshBuffer();
        public TwinBuffer LightCacheBuffer = new TwinBuffer();
        public int SkinningMeshBufferSize;
        public int frameRenderCount;

        public RPAssetsManager RPAssetsManager = new RPAssetsManager();
        public DeviceResources deviceResources = new DeviceResources();
        public GraphicsContext graphicsContext = new GraphicsContext();
        public GraphicsContext graphicsContext1 = new GraphicsContext();

        public Texture2D UI1Texture = new Texture2D();
        public Texture2D BRDFLut = new Texture2D();
        public Texture2D postProcessBackground = new Texture2D();

        public ReadBackTexture2D ReadBackTexture2D = new ReadBackTexture2D();

        public RenderPipelineDynamicContext dynamicContextRead = new RenderPipelineDynamicContext();
        public RenderPipelineDynamicContext dynamicContextWrite = new RenderPipelineDynamicContext();

        public List<CBuffer> CBs_Bone = new List<CBuffer>();

        public ProcessingList processingList = new ProcessingList();

        public PSODesc SkinningDesc = new PSODesc
        {
            blendState = EBlendState.none,
            cullMode = ECullMode.back,
            depthBias = 0,
            slopeScaledDepthBias = 0,
            dsvFormat = DxgiFormat.DXGI_FORMAT_UNKNOWN,
            inputLayout = EInputLayout.mmd,
            ptt = ED3D12PrimitiveTopologyType.POINT,
            rtvFormat = DxgiFormat.DXGI_FORMAT_UNKNOWN,
            renderTargetCount = 0,
            streamOutput = true,
            wireFrame = false,
        };

        public PSODesc shadowDesc = new PSODesc()
        {
            blendState = EBlendState.none,
            cullMode = ECullMode.none,
            depthBias = 3000,
            slopeScaledDepthBias = 1.0f,
            dsvFormat = DxgiFormat.DXGI_FORMAT_D32_FLOAT,
            inputLayout = EInputLayout.skinned,
            ptt = ED3D12PrimitiveTopologyType.TRIANGLE,
            rtvFormat = DxgiFormat.DXGI_FORMAT_UNKNOWN,
            renderTargetCount = 0,
            streamOutput = false,
            wireFrame = false,
        };

        public DxgiFormat gBufferFormat = DxgiFormat.DXGI_FORMAT_R16G16B16A16_UNORM;
        public DxgiFormat outputFormat = DxgiFormat.DXGI_FORMAT_R16G16B16A16_FLOAT;
        public DxgiFormat swapChainFormat = DxgiFormat.DXGI_FORMAT_B8G8R8A8_UNORM;
        public DxgiFormat depthFormat = DxgiFormat.DXGI_FORMAT_D32_FLOAT;

        XmlSerializer xmlSerializer2 = new XmlSerializer(typeof(PassSetting));
        public PassSetting defaultPassSetting;
        public PassSetting deferredPassSetting;
        public PassSetting RTPassSetting;
        public PassSetting currentPassSetting;

        public int screenWidth;
        public int screenHeight;
        public float dpi = 96.0f;
        public float logicScale = 1;
        public GameDriverContext gameDriverContext = new GameDriverContext()
        {
            FrameInterval = TimeSpan.FromSeconds(1 / 240.0),
            recordSettings = new RecordSettings()
            {
                FPS = 60,
                Width = 1920,
                Height = 1080,
                StartTime = 0,
                StopTime = 9999,
            },
        };

        public RenderPipelineContext()
        {
            _bigBufferHandle = GCHandle.Alloc(bigBuffer);
            for (int i = 0; i < ScreenSizeRenderTextures.Length; i++)
            {
                ScreenSizeRenderTextures[i] = new RenderTexture2D();
            }
            for (int i = 0; i < ScreenSizeDSVs.Length; i++)
            {
                ScreenSizeDSVs[i] = new RenderTexture2D();
            }
            MaterialBufferGroup.Reload(deviceResources, 768, 768 * 84);
            XBufferGroup.Reload(deviceResources, 768, 768 * 84);
        }
        ~RenderPipelineContext()
        {
            _bigBufferHandle.Free();
        }
        public void Reload()
        {
            shadowDesc.dsvFormat = depthFormat;
            graphicsContext.Reload(deviceResources);
            graphicsContext1.Reload(deviceResources);
        }

        public void BeginDynamicContext(bool enableDisplay, Settings settings, InShaderSettings inShaderSettings)
        {
            dynamicContextWrite.ClearCollections();
            dynamicContextWrite.frameRenderIndex = frameRenderCount;
            dynamicContextWrite.EnableDisplay = enableDisplay;
            frameRenderCount++;
            dynamicContextWrite.settings = settings;
            dynamicContextWrite.inShaderSettings = inShaderSettings;
        }

        struct _Data1
        {
            public int vertexStart;
            public int indexStart;
            public int vertexCount;
            public int indexCount;
        }

        public void UpdateGPUResource()
        {
            #region Update bone data
            int count = dynamicContextRead.rendererComponents.Count;
            while (CBs_Bone.Count < count)
            {
                CBuffer constantBuffer = new CBuffer();
                deviceResources.InitializeCBuffer(constantBuffer, c_entityDataBufferSize);
                CBs_Bone.Add(constantBuffer);
            }
            _Data1 data1 = new _Data1();
            Vector3 camPos = dynamicContextRead.cameras[0].Pos;
            for (int i = 0; i < count; i++)
            {
                var rendererComponent = dynamicContextRead.rendererComponents[i];
                data1.vertexCount = rendererComponent.meshVertexCount;
                data1.indexCount = rendererComponent.meshIndexCount;
                IntPtr ptr1 = Marshal.UnsafeAddrOfPinnedArrayElement(bigBuffer, 0);
                Matrix4x4 world = Matrix4x4.CreateFromQuaternion(rendererComponent.rotation) * Matrix4x4.CreateTranslation(rendererComponent.position);
                Marshal.StructureToPtr(Matrix4x4.Transpose(world), ptr1, true);
                Marshal.StructureToPtr(rendererComponent.amountAB, ptr1 + 64, true);
                Marshal.StructureToPtr(rendererComponent.meshVertexCount, ptr1 + 68, true);
                Marshal.StructureToPtr(rendererComponent.meshIndexCount, ptr1 + 72, true);
                Marshal.StructureToPtr(data1, ptr1 + 80, true);

                graphicsContext.UpdateResource(CBs_Bone[i], bigBuffer, 256, 0);
                graphicsContext.UpdateResourceRegion(CBs_Bone[i], 256, rendererComponent.boneMatricesData, 65280, 0);
                data1.vertexStart += rendererComponent.meshVertexCount;
                data1.indexStart += rendererComponent.meshIndexCount;


                if (rendererComponent.meshNeedUpdateA)
                {
                    graphicsContext.UpdateVerticesPos(rendererComponent.meshAppend, rendererComponent.meshPosData1, 0);
                    rendererComponent.meshNeedUpdateA = false;
                }
                if (rendererComponent.meshNeedUpdateB)
                {
                    graphicsContext.UpdateVerticesPos(rendererComponent.meshAppend, rendererComponent.meshPosData2, 1);
                    rendererComponent.meshNeedUpdateB = false;
                }
            }
            #endregion
        }

        public void PreConfig()
        {
            if (!Initilized) return;
            Prepare1(currentPassSetting);
        }
        public void Prepare1(PassSetting passSetting)
        {
            if (passSetting == null) return;
            foreach (var rt in passSetting.RenderTargets)
            {
                if (!RTs.TryGetValue(rt.Name, out var tex2d))
                {
                    tex2d = new RenderTexture2D();
                    RTs[rt.Name] = tex2d;
                }
                int x;
                int y;
                int z;

                if (rt.Size.Source == "OutputSize")
                {
                    x = screenWidth;
                    y = screenHeight;
                    z = 1;
                }
                else if (rt.Size.Source == "ShadowMapSize")
                {
                    x = ShadowMapResolution;
                    y = ShadowMapResolution;
                    z = 1;
                }
                else
                {
                    x = rt.Size.x;
                    y = rt.Size.y;
                    z = rt.Size.z;
                }
                if (tex2d.GetWidth() != x || tex2d.GetHeight() != y)
                {
                    if (rt.Format == DxgiFormat.DXGI_FORMAT_D16_UNORM || rt.Format == DxgiFormat.DXGI_FORMAT_D24_UNORM_S8_UINT || rt.Format == DxgiFormat.DXGI_FORMAT_D32_FLOAT)
                        tex2d.ReloadAsDepthStencil(x, y, rt.Format);
                    else
                        tex2d.ReloadAsRTVUAV(x, y, rt.Format);
                    graphicsContext.UpdateRenderTexture(tex2d);
                }
            }
        }

        public void ReloadTextureSizeResources()
        {
            int x = Math.Max((int)Math.Round(deviceResources.GetOutputSize().Width), 1);
            int y = Math.Max((int)Math.Round(deviceResources.GetOutputSize().Height), 1);
            screenWidth = x;
            screenHeight = y;
            if (outputRTV.GetWidth() != x || outputRTV.GetHeight() != y)
            {
                outputRTV.ReloadAsRTVUAV(x, y, outputFormat);
                graphicsContext.UpdateRenderTexture(outputRTV);
            }
            for (int i = 0; i < ScreenSizeRenderTextures.Length; i++)
            {
                ScreenSizeRenderTextures[i].ReloadAsRTVUAV(x, y, gBufferFormat);
                graphicsContext.UpdateRenderTexture(ScreenSizeRenderTextures[i]);
            }
            for (int i = 0; i < ScreenSizeDSVs.Length; i++)
            {
                ScreenSizeDSVs[i].ReloadAsDepthStencil(x, y, depthFormat);
                graphicsContext.UpdateRenderTexture(ScreenSizeDSVs[i]);
            }
            ReadBackTexture2D.Reload(x, y, 4);
            graphicsContext.UpdateReadBackTexture(ReadBackTexture2D);
            dpi = deviceResources.GetDpi();
            logicScale = dpi / 96.0f;
        }

        const int c_shadowMapResolutionLow = 2048;
        const int c_shadowMapResolutionHigh = 4096;
        int ShadowMapResolution = 2048;
        public bool HighResolutionShadowNow;
        public void ChangeShadowMapsQuality(bool highQuality)
        {
            if (HighResolutionShadowNow == highQuality) return;
            HighResolutionShadowNow = highQuality;
            if (highQuality)
                ShadowMapResolution = c_shadowMapResolutionHigh;
            else
                ShadowMapResolution = c_shadowMapResolutionLow;

            ShadowMapCube.ReloadAsDSV(ShadowMapResolution, ShadowMapResolution, depthFormat);
            graphicsContext.UpdateRenderTexture(ShadowMapCube);
        }

        public bool Initilized = false;
        public Task LoadTask;
        public async Task ReloadDefalutResources(MiscProcessContext miscProcessContext)
        {
            deviceResources.InitializeCBuffer(CameraDataBuffers, c_presentDataSize);
            deviceResources.InitializeCBuffer(LightCameraDataBuffer, c_lightingBufferSize);

            HighResolutionShadowNow = true;
            ChangeShadowMapsQuality(false);

            Uploader upTexLoading = new Uploader();
            Uploader upTexError = new Uploader();
            upTexLoading.Texture2DPure(1, 1, new Vector4(0, 1, 1, 1));
            upTexError.Texture2DPure(1, 1, new Vector4(1, 0, 1, 1));
            processingList.AddObject(new Texture2DUploadPack(TextureLoading, upTexLoading));
            processingList.AddObject(new Texture2DUploadPack(TextureError, upTexError));
            Uploader upTexPostprocessBackground = new Uploader();
            upTexPostprocessBackground.Texture2DPure(64, 64, new Vector4(1, 1, 1, 0));
            processingList.AddObject(new Texture2DUploadPack(postProcessBackground, upTexPostprocessBackground));

            Uploader upTexEnvCube = new Uploader();
            upTexEnvCube.TextureCubePure(32, 32, new Vector4[] { new Vector4(0.4f, 0.32f, 0.32f, 1), new Vector4(0.32f, 0.4f, 0.32f, 1), new Vector4(0.4f, 0.4f, 0.4f, 1), new Vector4(0.32f, 0.4f, 0.4f, 1), new Vector4(0.4f, 0.4f, 0.32f, 1), new Vector4(0.32f, 0.32f, 0.4f, 1) });
            processingList.AddObject(new TextureCubeUploadPack(SkyBox, upTexEnvCube));

            IrradianceMap.ReloadAsRTVUAV(32, 32, 1, DxgiFormat.DXGI_FORMAT_R32G32B32A32_FLOAT);
            ReflectMap.ReloadAsRTVUAV(1024, 1024, 7, DxgiFormat.DXGI_FORMAT_R16G16B16A16_FLOAT);
            miscProcessContext.Add(new P_Env_Data() { source = SkyBox, IrradianceMap = IrradianceMap, EnvMap = ReflectMap, Level = 16 });
            graphicsContext.UpdateRenderTexture(IrradianceMap);
            graphicsContext.UpdateRenderTexture(ReflectMap);

            ndcQuadMesh.ReloadNDCQuad();
            processingList.AddObject(ndcQuadMesh);
            cubeMesh.ReloadCube();
            processingList.AddObject(cubeMesh);
            cubeWireMesh.ReloadCubeWire();
            processingList.AddObject(cubeWireMesh);

            await ReloadTexture2DNoMip(BRDFLut, processingList, "ms-appx:///Assets/Textures/brdflut.png");
            await ReloadTexture2DNoMip(UI1Texture, processingList, "ms-appx:///Assets/Textures/UI_1.png");

            defaultPassSetting = (PassSetting)xmlSerializer2.Deserialize(await OpenReadStream("ms-appx:///DefaultResources/PassSetting.xml"));
            deferredPassSetting = (PassSetting)xmlSerializer2.Deserialize(await OpenReadStream("ms-appx:///DefaultResources/DeferredPassSetting.xml"));
            RTPassSetting = (PassSetting)xmlSerializer2.Deserialize(await OpenReadStream("ms-appx:///DefaultResources/DeferredRayTracingPassSetting.xml"));
            try
            {
                if (deviceResources.IsRayTracingSupport())
                    await ConfigRayTracing(RTPassSetting);
            }
            catch (Exception e)
            {
                string a = e.ToString();
            }

            RTs["_Output0"] = outputRTV;
            ConfigPassSettings(defaultPassSetting);
            currentPassSetting = defaultPassSetting;
            //ConfigPassSettings(deferredPassSetting);
            //currentPassSetting = deferredPassSetting;
            //ConfigPassSettings(RTPassSetting);
            //currentPassSetting = RTPassSetting;

            Initilized = true;
        }

        public bool ConfigPassSettings(PassSetting passSetting)
        {
            if (!passSetting.Verify()) return false;
            Prepare1(passSetting);

            //foreach (var rt in passSetting.RenderTargets)
            //{
            //    if (!RTs.TryGetValue(rt.Name, out var tex2d))
            //    {
            //        tex2d = new RenderTexture2D();
            //        RTs[rt.Name] = tex2d;
            //    }
            //}
            foreach (var pipelineState in passSetting.pipelineStates)
            {
            }
            foreach (var pass in passSetting.RenderSequence)
            {
                pass.depthSencil = (RenderTexture2D)_GetTex2DByName(pass.DepthStencil);
                var t1 = new List<RenderTexture2D>();
                foreach (var renderTarget in pass.RenderTargets)
                    t1.Add((RenderTexture2D)_GetTex2DByName(renderTarget));
                pass.renderTargets = t1.ToArray();
                VertexShader vs = null;
                PixelShader ps = null;
                if (pass.Pass.VertexShader != null)
                    RPAssetsManager.VSAssets.TryGetValue(pass.Pass.VertexShader, out vs);
                if (pass.Pass.PixelShader != null)
                    RPAssetsManager.PSAssets.TryGetValue(pass.Pass.PixelShader, out ps);
                PSO pso = new PSO();
                pso.Initialize(vs, null, ps);
                pass.PSODefault = pso;
                RPAssetsManager.PSOs[pass.Pass.Name] = pso;
            }
            return true;

        }
        public async Task ConfigRayTracing(PassSetting passSetting)
        {
            if (passSetting.RayTracingStateObject != null)
            {
                //foreach(var rtso in passSetting.RayTracingStateObjects)
                //{

                //}
                var rtso = passSetting.RayTracingStateObject;
                passSetting.RTSO = new RayTracingStateObject();
                passSetting.RTSO.LoadShaderLib(await ReadFile(rtso.Path));
                List<string> exportNames = new List<string>();
                List<string> rayGenShaders = new List<string>();
                List<string> missShaders = new List<string>();
                foreach (var s in rtso.rayGenShaders)
                {
                    exportNames.Add(s.Name);
                    rayGenShaders.Add(s.Name);
                }
                if (rtso.missShaders != null)
                    foreach (var s in rtso.missShaders)
                    {
                        exportNames.Add(s.Name);
                        missShaders.Add(s.Name);
                    }
                foreach (var h in rtso.hitGroups)
                {
                    if (!string.IsNullOrEmpty(h.AnyHitShader))
                        exportNames.Add(h.AnyHitShader);
                    if (!string.IsNullOrEmpty(h.ClosestHitShader))
                        exportNames.Add(h.ClosestHitShader);
                }
                passSetting.RTSO.ExportLib(exportNames.ToArray());

                foreach (var h in rtso.hitGroups)
                {
                    passSetting.RTSO.HitGroupSubobject(h.Name, h.AnyHitShader == null ? "" : h.AnyHitShader, h.ClosestHitShader == null ? "" : h.ClosestHitShader);
                }
                passSetting.RTSO.LocalRootSignature(RPAssetsManager.rtLocal);
                passSetting.RTSO.GlobalRootSignature(RPAssetsManager.rtGlobal);
                passSetting.RTSO.Config(rtso.MaxPayloadSize, rtso.MaxAttributeSize, rtso.MaxRecursionDepth);
                passSetting.RTSO.Create(deviceResources);

                foreach (var item in passSetting.RenderSequence)
                {
                    if (item.Type == "RayTracing")
                    {
                        RTIGroups[item.Name] = new RayTracingInstanceGroup();
                        RTSTs[item.Name] = new RayTracingShaderTable();
                        RTTASs[item.Name] = new RayTracingTopAS();
                        item.RayGenShaders = rayGenShaders.ToArray();
                        item.MissShaders = missShaders.ToArray();
                    }
                }
            }

        }
        public ITexture2D _GetTex2DByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (RTs.TryGetValue(name, out var tex))
            {
                return tex;
            }
            //else if (name == "_Output0")
            //    return outputRTV;
            else if (name == "_BRDFLUT")
                return BRDFLut;
            return null;
        }
        public ITextureCube _GetTexCubeByName(string name)
        {
            if (name == "_SkyBoxReflect")
                return ReflectMap;
            else if (name == "_SkyBoxIrradiance")
                return IrradianceMap;
            else if (name == "_SkyBox")
                return SkyBox;
            return null;
        }
        private async Task ReloadTexture2D(Texture2D texture2D, ProcessingList processingList, string uri)
        {
            Uploader uploader = new Uploader();
            uploader.Texture2D(await FileIO.ReadBufferAsync(await StorageFile.GetFileFromApplicationUriAsync(new Uri(uri))), true, true);
            processingList.AddObject(new Texture2DUploadPack(texture2D, uploader));
        }
        private async Task ReloadTexture2DNoMip(Texture2D texture2D, ProcessingList processingList, string uri)
        {
            Uploader uploader = new Uploader();
            uploader.Texture2D(await FileIO.ReadBufferAsync(await StorageFile.GetFileFromApplicationUriAsync(new Uri(uri))), false, false);
            processingList.AddObject(new Texture2DUploadPack(texture2D, uploader));
        }
        protected async Task<IBuffer> ReadFile(string uri)
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(uri));
            return await FileIO.ReadBufferAsync(file);
        }
        protected async Task<Stream> OpenReadStream(string uri)
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(new Uri(uri));
            return (await file.OpenAsync(FileAccessMode.Read)).AsStreamForRead();
        }
    }
}
