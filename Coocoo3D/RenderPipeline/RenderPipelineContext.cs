using Coocoo3D.Core;
using Coocoo3D.Present;
using Coocoo3D.RenderPipeline.Wrap;
using Coocoo3D.ResourceWarp;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

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
        public volatile bool NeedUpdateEntities;
        public volatile bool EnableDisplay;
        public bool Playing;
        public double PlayTime;
        public double DeltaTime;
        public TimeSpan FrameInterval;
        public float PlaySpeed;
        public volatile bool RequireResetPhysics;
        public bool NeedReloadModel;
        public DeviceResources DeviceResources;
        public ProcessingList ProcessingList;
        public bool RequireResize;
        public bool RequireResizeOuter;
        public Windows.Foundation.Size NewSize;
        public float AspectRatio;
        public bool RequireInterruptRender;
        public WICFactory WICFactory;
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
            NeedUpdateEntities |= updateEntities;
            NeedRender = true;
        }

        public void RequireRender()
        {
            NeedRender = true;
        }
    }

    public class RenderPipelineDynamicContext
    {
        public Settings settings;
        public InShaderSettings inShaderSettings;
        public List<MMD3DEntity> entities = new List<MMD3DEntity>();
        public MMD3DEntity selectedEntity;
        public List<LightingData> lightings = new List<LightingData>();
        public List<LightingData> selectedLightings = new List<LightingData>();
        public List<CameraData> cameras = new List<CameraData>();
        public int VertexCount;
        public int frameRenderIndex;
        public int progressiveRenderIndex;
        public double Time;
        public double DeltaTime;
        public bool EnableDisplay;

        public int GetSceneObjectVertexCount()
        {
            int count = 0;
            for (int i = 0; i < entities.Count; i++)
            {
                count += entities[i].rendererComponent.meshVertexCount;
            }
            VertexCount = count;
            return count;
        }

        public void Preprocess()
        {
            lightings.Sort();
        }

        public void ClearCollections()
        {
            entities.Clear();
            lightings.Clear();
            selectedLightings.Clear();
            cameras.Clear();
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
        public const int c_materialDataSize = 768;
        public CBufferGroup MaterialBufferGroup = new CBufferGroup();
        public void DesireMaterialBuffers(int count)
        {
            MaterialBufferGroup.SetSlienceCount(deviceResources, count);
        }

        public RenderTexture2D outputRTV = new RenderTexture2D();
        public RenderTexture2D[] ScreenSizeRenderTextures = new RenderTexture2D[4];
        public RenderTexture2D[] ScreenSizeDSVs = new RenderTexture2D[2];

        public RenderTextureCube ShadowMapCube = new RenderTextureCube();

        public Texture2D TextureLoading = new Texture2D();
        public Texture2D TextureError = new Texture2D();
        public TextureCube SkyBox = new TextureCube();
        public RenderTextureCube IrradianceMap = new RenderTextureCube();
        public RenderTextureCube EnvironmentMap = new RenderTextureCube();

        public MMDMesh ndcQuadMesh = new MMDMesh();
        public MMDMesh cubeMesh = new MMDMesh();
        public MMDMesh cubeWireMesh = new MMDMesh();
        public int ndcQuadMeshIndexCount;
        public int cubeMeshIndexCount;
        public int cubeWireMeshIndexCount;
        public MeshBuffer SkinningMeshBuffer = new MeshBuffer();
        public TwinBuffer LightCacheBuffer = new TwinBuffer();
        public int SkinningMeshBufferSize;
        public int frameRenderCount;

        public RPAssetsManager RPAssetsManager = new RPAssetsManager();
        public DeviceResources deviceResources = new DeviceResources();
        public GraphicsContext graphicsContext = new GraphicsContext();
        public GraphicsContext graphicsContext1 = new GraphicsContext();
        public GraphicsContext[] graphicsContexts;

        public Texture2D UI1Texture = new Texture2D();
        public Texture2D BRDFLut = new Texture2D();
        public Texture2D postProcessBackground = new Texture2D();

        public ReadBackTexture2D ReadBackTexture2D = new ReadBackTexture2D();

        public RenderPipelineDynamicContext dynamicContextRead = new RenderPipelineDynamicContext();
        public RenderPipelineDynamicContext dynamicContextWrite = new RenderPipelineDynamicContext();

        public List<CBuffer> CBs_Bone = new List<CBuffer>();


        public PSODesc SkinningDesc = new PSODesc
        {
            blendState = EBlendState.none,
            cullMode = ECullMode.back,
            depthBias = 0,
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
            depthBias = 2500,
            dsvFormat = DxgiFormat.DXGI_FORMAT_D24_UNORM_S8_UINT,//
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
        public DxgiFormat depthFormat = DxgiFormat.DXGI_FORMAT_D24_UNORM_S8_UINT;

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
            MaterialBufferGroup.Reload(768, 768 * 84);
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
            int count = dynamicContextRead.entities.Count;
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
                var entity = dynamicContextRead.entities[i];
                var rendererComponent = entity.rendererComponent;
                data1.vertexCount = rendererComponent.meshVertexCount;
                data1.indexCount = rendererComponent.meshIndexCount;
                IntPtr ptr1 = Marshal.UnsafeAddrOfPinnedArrayElement(bigBuffer, 0);
                Matrix4x4 world = Matrix4x4.CreateFromQuaternion(entity.Rotation) * Matrix4x4.CreateTranslation(entity.Position);
                Marshal.StructureToPtr(Matrix4x4.Transpose(world), ptr1, true);
                Marshal.StructureToPtr(rendererComponent.amountAB, ptr1 + 64, true);
                Marshal.StructureToPtr(rendererComponent.meshVertexCount, ptr1 + 68, true);
                Marshal.StructureToPtr(rendererComponent.meshIndexCount, ptr1 + 72, true);
                Marshal.StructureToPtr(data1, ptr1 + 80, true);

                graphicsContext.UpdateResource(CBs_Bone[i], bigBuffer, 256, 0);
                graphicsContext.UpdateResourceRegion(CBs_Bone[i], 256, dynamicContextRead.entities[i].boneComponent.boneMatricesData, 65280, 0);
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

        public void ReloadTextureSizeResources(ProcessingList processingList)
        {
            int x = Math.Max((int)Math.Round(deviceResources.GetOutputSize().Width), 1);
            int y = Math.Max((int)Math.Round(deviceResources.GetOutputSize().Height), 1);
            outputRTV.ReloadAsRTVUAV(x, y, outputFormat);
            processingList.UnsafeAdd(outputRTV);
            for (int i = 0; i < ScreenSizeRenderTextures.Length; i++)
            {
                ScreenSizeRenderTextures[i].ReloadAsRTVUAV(x, y, gBufferFormat);
                processingList.UnsafeAdd(ScreenSizeRenderTextures[i]);
            }
            for (int i = 0; i < ScreenSizeDSVs.Length; i++)
            {
                ScreenSizeDSVs[i].ReloadAsDepthStencil(x, y, depthFormat);
                processingList.UnsafeAdd(ScreenSizeDSVs[i]);
            }
            ReadBackTexture2D.Reload(x, y, 4);
            processingList.UnsafeAdd(ReadBackTexture2D);
            screenWidth = x;
            screenHeight = y;
            dpi = deviceResources.GetDpi();
            logicScale = dpi / 96.0f;
        }

        const int c_shadowMapResolutionLow = 2048;
        const int c_shadowMapResolutionHigh = 4096;
        public bool HighResolutionShadowNow;
        public void ChangeShadowMapsQuality(ProcessingList processingList, bool highQuality)
        {
            if (HighResolutionShadowNow == highQuality) return;
            HighResolutionShadowNow = highQuality;
            void _Quality(int x, int y)
            {
                ShadowMapCube.ReloadAsDSV(x, y, depthFormat);
                processingList.UnsafeAdd(ShadowMapCube);
            }
            if (highQuality)
            {
                _Quality(c_shadowMapResolutionHigh, c_shadowMapResolutionHigh);
            }
            else
            {
                _Quality(c_shadowMapResolutionLow, c_shadowMapResolutionLow);
            }
        }

        public bool Initilized = false;
        public Task LoadTask;
        public async Task ReloadDefalutResources(ProcessingList processingList, MiscProcessContext miscProcessContext)
        {
                deviceResources.InitializeCBuffer(CameraDataBuffers, c_presentDataSize);
            deviceResources.InitializeCBuffer(LightCameraDataBuffer, c_lightingBufferSize);

            HighResolutionShadowNow = true;
            ChangeShadowMapsQuality(processingList, false);

            Uploader upTexLoading = new Uploader();
            Uploader upTexError = new Uploader();
            upTexLoading.Texture2DPure(1, 1, new Vector4(0, 1, 1, 1));
            upTexError.Texture2DPure(1, 1, new Vector4(1, 0, 1, 1)); ;
            processingList.AddObject(new Texture2DUploadPack(TextureLoading, upTexLoading));
            processingList.AddObject(new Texture2DUploadPack(TextureError, upTexError));
            Uploader upTexPostprocessBackground = new Uploader();
            upTexPostprocessBackground.Texture2DPure(64, 64, new Vector4(1, 1, 1, 0));
            processingList.AddObject(new Texture2DUploadPack(postProcessBackground, upTexPostprocessBackground));

            Uploader upTexEnvCube = new Uploader();
            upTexEnvCube.TextureCubePure(32, 32, new Vector4[] { new Vector4(0.2f, 0.16f, 0.16f, 1), new Vector4(0.16f, 0.2f, 0.16f, 1), new Vector4(0.2f, 0.2f, 0.2f, 1), new Vector4(0.16f, 0.2f, 0.2f, 1), new Vector4(0.2f, 0.2f, 0.16f, 1), new Vector4(0.16f, 0.16f, 0.2f, 1) });

            IrradianceMap.ReloadAsRTVUAV(32, 32, 1, DxgiFormat.DXGI_FORMAT_R32G32B32A32_FLOAT);
            EnvironmentMap.ReloadAsRTVUAV(1024, 1024, 7, DxgiFormat.DXGI_FORMAT_R16G16B16A16_FLOAT);
            miscProcessContext.Add(new P_Env_Data() { source = SkyBox, IrradianceMap = IrradianceMap, EnvMap = EnvironmentMap, Level = 16 });
            processingList.AddObject(new TextureCubeUploadPack(SkyBox, upTexEnvCube));
            processingList.AddObject(IrradianceMap);
            processingList.AddObject(EnvironmentMap);

            ndcQuadMesh.ReloadNDCQuad();
            ndcQuadMeshIndexCount = ndcQuadMesh.m_indexCount;
            processingList.AddObject(ndcQuadMesh);

            cubeMesh.ReloadCube();
            cubeMeshIndexCount = cubeMesh.m_indexCount;
            processingList.AddObject(cubeMesh);

            cubeWireMesh.ReloadCubeWire();
            cubeWireMeshIndexCount = cubeWireMesh.m_indexCount;
            processingList.AddObject(cubeWireMesh);

            await ReloadTexture2DNoMip(BRDFLut, processingList, "ms-appx:///Assets/Textures/brdflut.png");
            await ReloadTexture2DNoMip(UI1Texture, processingList, "ms-appx:///Assets/Textures/UI_1.png");

            Initilized = true;
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
    }
}
