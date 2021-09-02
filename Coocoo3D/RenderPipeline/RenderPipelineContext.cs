using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Numerics;
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
using Coocoo3D.Utility;

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
        public int NeedRender;
        public volatile bool EnableDisplay;
        public bool Playing;
        public double PlayTime;
        public double DeltaTime;
        public float FrameInterval;
        public float PlaySpeed;
        public volatile bool RequireResetPhysics;
        public bool NeedReloadModel;
        //public bool RequireResize;
        public bool RequireResizeOuter;
        public Windows.Foundation.Size NewSize;
        public float AspectRatio;
        public RecordSettings recordSettings;

        public DateTime LatestRenderTime;

        public void ReqireReloadModel()
        {
            NeedReloadModel = true;
            NeedRender = 10;
        }

        public void RequireRender(bool updateEntities)
        {
            NeedRender = 10;
        }

        public void RequireRender()
        {
            NeedRender = 10;
        }
    }

    public class RenderPipelineContext
    {
        const int c_entityDataBufferSize = 65536;
        public byte[] bigBuffer = new byte[65536];
        GCHandle _bigBufferHandle;

        public CBufferGroup XBufferGroup = new CBufferGroup();
        public MainCaches mainCaches = new MainCaches();

        public Texture2D outputRTV = new Texture2D();
        public Texture2D finalOutput = new Texture2D();

        public Dictionary<string, VisualChannel> visualChannels = new Dictionary<string, VisualChannel>();

        public VisualChannel currentChannel;

        public Dictionary<string, Texture2D> RTs = new Dictionary<string, Texture2D>();

        //public RayTracingASGroup RTASGroup = new RayTracingASGroup();
        //public Dictionary<string, RayTracingShaderTable> RTSTs = new Dictionary<string, RayTracingShaderTable>();
        //public Dictionary<string, RayTracingInstanceGroup> RTIGroups = new Dictionary<string, RayTracingInstanceGroup>();
        //public Dictionary<string, RayTracingTopAS> RTTASs = new Dictionary<string, RayTracingTopAS>();

        public Texture2D TextureLoading = new Texture2D();
        public Texture2D TextureError = new Texture2D();

        public bool SkyBoxChanged = false;
        public TextureCube SkyBox = new TextureCube();
        public TextureCube IrradianceMap = new TextureCube();
        public TextureCube ReflectMap = new TextureCube();

        public MMDMesh ndcQuadMesh = new MMDMesh();
        public MMDMesh cubeMesh = new MMDMesh();
        public MMDMesh cubeWireMesh = new MMDMesh();
        public MeshBuffer SkinningMeshBuffer = new MeshBuffer();
        public int SkinningMeshBufferSize;
        public int frameRenderCount;

        public RPAssetsManager RPAssetsManager = new RPAssetsManager();
        public DeviceResources deviceResources = new DeviceResources();
        public GraphicsContext graphicsContext = new GraphicsContext();
        public GraphicsContext graphicsContext1 = new GraphicsContext();

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

        public DxgiFormat gBufferFormat = DxgiFormat.DXGI_FORMAT_R16G16B16A16_UNORM;
        public DxgiFormat outputFormat = DxgiFormat.DXGI_FORMAT_R16G16B16A16_FLOAT;
        public DxgiFormat swapChainFormat = DxgiFormat.DXGI_FORMAT_R8G8B8A8_UNORM;

        public XmlSerializer PassSettingSerializer = new XmlSerializer(typeof(PassSetting));
        public PassSetting defaultPassSetting;
        public PassSetting deferredPassSetting;
        public PassSetting RTPassSetting;
        public PassSetting currentPassSetting;
        public PassSetting customPassSetting;

        public Int2 screenSize;
        public Int2 outputSize;
        public Int2 sceneViewSize = new Int2(100, 100);
        public float dpi = 96.0f;
        public float logicScale = 1;
        public GameDriverContext gameDriverContext = new GameDriverContext()
        {
            FrameInterval = 1 / 240.0f,
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
        }
        ~RenderPipelineContext()
        {
            _bigBufferHandle.Free();
        }
        public void Reload()
        {
            graphicsContext.Reload(deviceResources);
            graphicsContext1.Reload(deviceResources);
            XBufferGroup.Reload(deviceResources, 1024, 1024 * 256);
            var visualChannel = new VisualChannel();
            visualChannels["main"] = visualChannel;
            visualChannel.Name = "main";
            visualChannel.graphicsContext = graphicsContext;
        }

        public void BeginDynamicContext(bool enableDisplay, Settings settings)
        {
            dynamicContextWrite.FrameBegin();
            dynamicContextWrite.EnableDisplay = enableDisplay;
            dynamicContextWrite.settings = settings;
            dynamicContextWrite.currentPassSetting = currentPassSetting;
            dynamicContextWrite.frameRenderIndex = frameRenderCount;
            frameRenderCount++;
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
            int count = dynamicContextRead.renderers.Count;
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
                var rendererComponent = dynamicContextRead.renderers[i];
                data1.vertexCount = rendererComponent.meshVertexCount;
                data1.indexCount = rendererComponent.meshIndexCount;
                Matrix4x4 world = Matrix4x4.CreateFromQuaternion(rendererComponent.rotation) * Matrix4x4.CreateTranslation(rendererComponent.position);

                CooUtility.Write(bigBuffer, 0, Matrix4x4.Transpose(world));

                CooUtility.Write(bigBuffer, 68, rendererComponent.meshVertexCount);
                CooUtility.Write(bigBuffer, 72, rendererComponent.meshIndexCount);

                MemoryMarshal.Write(new Span<byte>(bigBuffer, 80, 16), ref data1);
                MemoryMarshal.Cast<Matrix4x4, byte>(rendererComponent.boneMatricesData).CopyTo(new Span<byte>(bigBuffer, 256, 65280));
                graphicsContext.UpdateResource(CBs_Bone[i], bigBuffer, 65536, 0);
                data1.vertexStart += rendererComponent.meshVertexCount;
                data1.indexStart += rendererComponent.meshIndexCount;


                if (rendererComponent.meshNeedUpdate)
                {
                    graphicsContext.UpdateVerticesPos(rendererComponent.meshAppend, rendererComponent.meshPosData1);
                    rendererComponent.meshNeedUpdate = false;
                }
            }
            #endregion
        }

        public void PreConfig()
        {
            if (!Initilized) return;
            ConfigPassSettings(dynamicContextRead.currentPassSetting);
            PrepareRenderTarget(dynamicContextRead.currentPassSetting);
            int SceneObjectVertexCount = dynamicContextRead.GetSceneObjectVertexCount();
            if (SceneObjectVertexCount > SkinningMeshBufferSize)
            {
                SkinningMeshBufferSize = SceneObjectVertexCount;
                deviceResources.InitializeMeshBuffer(SkinningMeshBuffer, SceneObjectVertexCount);
            }
        }
        public void PrepareRenderTarget(PassSetting passSetting)
        {
            if (passSetting == null) return;

            foreach (var rt in passSetting.RenderTargets)
            {
                if (!RTs.TryGetValue(rt.Name, out var tex2d))
                {
                    tex2d = new Texture2D();
                    RTs[rt.Name] = tex2d;
                }
                int x;
                int y;

                if (rt.Size.Source == "OutputSize")
                {
                    x = (int)(outputSize.X * rt.Size.Multiplier);
                    y = (int)(outputSize.Y * rt.Size.Multiplier);
                }
                else if (rt.Size.Source == "ShadowMapSize")
                {
                    x = (int)(dynamicContextRead.settings.ShadowMapResolution * rt.Size.Multiplier);
                    y = (int)(dynamicContextRead.settings.ShadowMapResolution * rt.Size.Multiplier);
                }
                else
                {
                    x = rt.Size.x;
                    y = rt.Size.y;
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

        public void ReloadScreenSizeResources()
        {
            int x = Math.Max((int)Math.Round(deviceResources.GetOutputSize().Width), 1);
            int y = Math.Max((int)Math.Round(deviceResources.GetOutputSize().Height), 1);
            screenSize.X = x;
            screenSize.Y = y;
            if (outputRTV.GetWidth() != outputSize.X || outputRTV.GetHeight() != outputSize.Y)
            {
                outputRTV.ReloadAsRTVUAV(outputSize.X, outputSize.Y, outputFormat);
                graphicsContext.UpdateRenderTexture(outputRTV);

                finalOutput.ReloadAsRTVUAV(outputSize.X, outputSize.Y, swapChainFormat);
                graphicsContext.UpdateRenderTexture(finalOutput);

                ReadBackTexture2D.Reload(outputSize.X, outputSize.Y, 4);
                graphicsContext.UpdateReadBackTexture(ReadBackTexture2D);
            }
            dpi = deviceResources.GetDpi();
            logicScale = dpi / 96.0f;
        }

        public bool Initilized = false;
        public Task LoadTask;
        public async Task ReloadDefalutResources()
        {
            processingList.AddObject(Texture2DUploadPack.Pure(TextureLoading, 1, 1, new Vector4(0, 1, 1, 1)));
            processingList.AddObject(Texture2DUploadPack.Pure(TextureError, 1, 1, new Vector4(1, 0, 1, 1)));

            Uploader upTexEnvCube = new Uploader();
            upTexEnvCube.TextureCubePure(32, 32, new Vector4[] { new Vector4(0.4f, 0.32f, 0.32f, 1), new Vector4(0.32f, 0.4f, 0.32f, 1), new Vector4(0.4f, 0.4f, 0.4f, 1), new Vector4(0.32f, 0.4f, 0.4f, 1), new Vector4(0.4f, 0.4f, 0.32f, 1), new Vector4(0.32f, 0.32f, 0.4f, 1) });
            processingList.AddObject(new TextureCubeUploadPack(SkyBox, upTexEnvCube));

            IrradianceMap.ReloadAsRTVUAV(32, 32, 1, DxgiFormat.DXGI_FORMAT_R32G32B32A32_FLOAT);
            ReflectMap.ReloadAsRTVUAV(1024, 1024, 7, DxgiFormat.DXGI_FORMAT_R16G16B16A16_FLOAT);

            SkyBoxChanged = true;
            graphicsContext.UpdateRenderTexture(IrradianceMap);
            graphicsContext.UpdateRenderTexture(ReflectMap);

            ndcQuadMesh.ReloadNDCQuad();
            processingList.AddObject(ndcQuadMesh);
            cubeMesh.ReloadCube();
            processingList.AddObject(cubeMesh);
            cubeWireMesh.ReloadCubeWire();
            processingList.AddObject(cubeWireMesh);

            foreach (var tex2dDef in RPAssetsManager.defaultResource.texture2Ds)
            {
                Texture2D tex2d = new Texture2D();
                await ReloadTexture2DNoMip(tex2d, processingList, tex2dDef.Path);
                RPAssetsManager.texture2ds.Add(tex2dDef.Name, tex2d);
            }

            defaultPassSetting = (PassSetting)PassSettingSerializer.Deserialize(await OpenReadStream("ms-appx:///Samples/samplePasses.coocoox"));
            deferredPassSetting = (PassSetting)PassSettingSerializer.Deserialize(await OpenReadStream("ms-appx:///Samples/sampleDeferredPasses.coocoox"));
            RTPassSetting = (PassSetting)PassSettingSerializer.Deserialize(await OpenReadStream("ms-appx:///DefaultResources/DeferredRayTracingPassSetting.xml"));
            try
            {
                //if (deviceResources.IsRayTracingSupport())
                //    await ConfigRayTracing(RTPassSetting);
            }
            catch (Exception e)
            {
                string a = e.ToString();
            }
            RTs["_Output0"] = outputRTV;
            RPAssetsManager.texture2ds["_Final0"] = finalOutput;

            SetCurrentPassSetting(defaultPassSetting);

            Initilized = true;
        }

        public void SetCurrentPassSetting(PassSetting passSetting)
        {
            currentPassSetting = passSetting;
        }

        public bool ConfigPassSettings(PassSetting passSetting)
        {
            if (passSetting.configured) return true;
            if (!passSetting.Verify()) return false;
            PrepareRenderTarget(passSetting);

            foreach (var pipelineState in passSetting.PipelineStates)
            {
                PSO pso = new PSO();
                VertexShader vs = null;
                GeometryShader gs = null;
                PixelShader ps = null;
                if (pipelineState.VertexShader != null)
                    vs = RPAssetsManager.VSAssets[pipelineState.VertexShader];
                if (pipelineState.GeometryShader != null)
                    gs = RPAssetsManager.GSAssets[pipelineState.GeometryShader];
                if (pipelineState.PixelShader != null)
                    ps = RPAssetsManager.PSAssets[pipelineState.PixelShader];
                if (RPAssetsManager.PSOs.TryGetValue(pipelineState.Name, out var psoDestroy))
                    psoDestroy.DelayDestroy(deviceResources);
                pso.Initialize(vs, gs, ps);
                RPAssetsManager.PSOs[pipelineState.Name] = pso;
            }
            RefreshPassesRenderTarget(passSetting);
            foreach (var pass in passSetting.RenderSequence)
            {
                if (pass.Type == "Swap") continue;

                if (pass.passParameters != null)
                {
                    pass.passParameters1 = new Dictionary<string, float>();
                    foreach (var v in pass.passParameters)
                        pass.passParameters1[v.Name] = v.Value;
                }

                int SlotComparison(SRVUAVSlotRes x1, SRVUAVSlotRes y1)
                {
                    return x1.Index.CompareTo(y1.Index);
                }
                int SlotComparison1(CBVSlotRes x1, CBVSlotRes y1)
                {
                    return x1.Index.CompareTo(y1.Index);
                }
                StringBuilder stringBuilder = new StringBuilder();
                pass.Pass.CBVs?.Sort(SlotComparison1);
                pass.Pass.SRVs?.Sort(SlotComparison);
                pass.Pass.UAVs?.Sort(SlotComparison);

                if (pass.Pass.CBVs != null)
                {
                    int count = 0;
                    foreach (var cbv in pass.Pass.CBVs)
                    {
                        for (int i = count; i < cbv.Index + 1; i++)
                            stringBuilder.Append("C");
                        count = cbv.Index + 1;
                    }
                }
                if (pass.Pass.SRVs != null)
                {
                    int count = 0;
                    foreach (var srv in pass.Pass.SRVs)
                    {
                        for (int i = count; i < srv.Index + 1; i++)
                            stringBuilder.Append("s");
                        count = srv.Index + 1;
                    }
                }
                if (pass.Pass.UAVs != null)
                {
                    int count = 0;
                    foreach (var uav in pass.Pass.UAVs)
                    {
                        for (int i = count; i < uav.Index + 1; i++)
                            stringBuilder.Append("u");
                        count = uav.Index + 1;
                    }
                }
                pass.rootSignatureKey = stringBuilder.ToString();

                VertexShader vs = null;
                GeometryShader gs = null;
                PixelShader ps = null;
                if (pass.Pass.VertexShader != null)
                    RPAssetsManager.VSAssets.TryGetValue(pass.Pass.VertexShader, out vs);
                if (pass.Pass.GeometryShader != null)
                    RPAssetsManager.GSAssets.TryGetValue(pass.Pass.GeometryShader, out gs);
                if (pass.Pass.PixelShader != null)
                    RPAssetsManager.PSAssets.TryGetValue(pass.Pass.PixelShader, out ps);
                PSO pso = new PSO();
                pso.Initialize(vs, gs, ps);
                pass.PSODefault = pso;
                RPAssetsManager.PSOs[pass.Pass.Name] = pso;
            }
            passSetting.configured = true;
            return true;

        }

        public MMDMesh GetMesh(string path) => mainCaches.ModelPackCaches[path].GetMesh();

        public void RefreshPassesRenderTarget(PassSetting passSetting)
        {
            foreach (var _pass1 in passSetting.RenderSequence)
            {
                if (_pass1.Type == "Swap") continue;

                _pass1.depthStencil = _GetTex2DByName(_pass1.DepthStencil);
                var t1 = new Texture2D[_pass1.RenderTargets.Count];
                for (int i = 0; i < _pass1.RenderTargets.Count; i++)
                {
                    string renderTarget = _pass1.RenderTargets[i];
                    t1[i] = _GetTex2DByName(renderTarget);
                }
                _pass1.renderTargets = t1;
            }
        }
        //public async Task ConfigRayTracing(PassSetting passSetting)
        //{
        //    if (passSetting.RayTracingStateObject != null)
        //    {
        //        //foreach(var rtso in passSetting.RayTracingStateObjects)
        //        //{

        //        //}
        //        var rtso = passSetting.RayTracingStateObject;
        //        passSetting.RTSO = new RayTracingStateObject();
        //        passSetting.RTSO.LoadShaderLib(await ReadFile(rtso.Path));
        //        List<string> exportNames = new List<string>();
        //        List<string> rayGenShaders = new List<string>();
        //        List<string> missShaders = new List<string>();
        //        foreach (var s in rtso.rayGenShaders)
        //        {
        //            exportNames.Add(s.Name);
        //            rayGenShaders.Add(s.Name);
        //        }
        //        if (rtso.missShaders != null)
        //            foreach (var s in rtso.missShaders)
        //            {
        //                exportNames.Add(s.Name);
        //                missShaders.Add(s.Name);
        //            }
        //        foreach (var h in rtso.hitGroups)
        //        {
        //            if (!string.IsNullOrEmpty(h.AnyHitShader))
        //                exportNames.Add(h.AnyHitShader);
        //            if (!string.IsNullOrEmpty(h.ClosestHitShader))
        //                exportNames.Add(h.ClosestHitShader);
        //        }
        //        passSetting.RTSO.ExportLib(exportNames.ToArray());

        //        foreach (var h in rtso.hitGroups)
        //        {
        //            passSetting.RTSO.HitGroupSubobject(h.Name, h.AnyHitShader == null ? "" : h.AnyHitShader, h.ClosestHitShader == null ? "" : h.ClosestHitShader);
        //        }
        //        passSetting.RTSO.LocalRootSignature(RPAssetsManager.rtLocal);
        //        passSetting.RTSO.GlobalRootSignature(RPAssetsManager.rtGlobal);
        //        passSetting.RTSO.Config(rtso.MaxPayloadSize, rtso.MaxAttributeSize, rtso.MaxRecursionDepth);
        //        passSetting.RTSO.Create(deviceResources);

        //        foreach (var item in passSetting.RenderSequence)
        //        {
        //            if (item.Type == "RayTracing")
        //            {
        //                RTIGroups[item.Name] = new RayTracingInstanceGroup();
        //                RTSTs[item.Name] = new RayTracingShaderTable();
        //                RTTASs[item.Name] = new RayTracingTopAS();
        //                item.RayGenShaders = rayGenShaders.ToArray();
        //                item.MissShaders = missShaders.ToArray();
        //            }
        //        }
        //    }

        //}
        public Texture2D _GetTex2DByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (RTs.TryGetValue(name, out var tex))
            {
                return tex;
            }
            else if (RPAssetsManager.texture2ds.TryGetValue(name, out var tex2))
            {
                return tex2;
            }
            return null;
        }
        public TextureCube _GetTexCubeByName(string name)
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
            Uploader uploader = await Texture2DPack.UploaderTex2D(uri);
            processingList.AddObject(new Texture2DUploadPack(texture2D, uploader));
        }
        private async Task ReloadTexture2DNoMip(Texture2D texture2D, ProcessingList processingList, string uri)
        {
            Uploader uploader = await Texture2DPack.UploaderTex2DNoMip(uri);
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
