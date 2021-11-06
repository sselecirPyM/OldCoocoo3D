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
using Vortice.DXGI;
using Vortice.Direct3D12;

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
        public bool RequireResize;
        public Vector2 NewSize;
        public RecordSettings recordSettings;

        public DateTime LatestRenderTime;

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

        public MainCaches mainCaches = new MainCaches();

        public Dictionary<string, VisualChannel> visualChannels = new Dictionary<string, VisualChannel>();

        public VisualChannel currentChannel;

        public Dictionary<string, Texture2D> RTs = new Dictionary<string, Texture2D>();

        public Texture2D TextureLoading = new Texture2D();
        public Texture2D TextureError = new Texture2D();

        public bool SkyBoxChanged = false;
        public TextureCube SkyBox = new TextureCube();
        public TextureCube IrradianceMap = new TextureCube();
        public TextureCube ReflectMap = new TextureCube();

        public MMDMesh ndcQuadMesh = new MMDMesh();
        public int frameRenderCount;

        public RPAssetsManager RPAssetsManager = new RPAssetsManager();
        public GraphicsDevice graphicsDevice = new GraphicsDevice();
        public GraphicsContext graphicsContext = new GraphicsContext();
        public GraphicsContext graphicsContext1 = new GraphicsContext();

        public ReadBackTexture2D ReadBackTexture2D = new ReadBackTexture2D();

        public RenderPipelineDynamicContext dynamicContextRead = new RenderPipelineDynamicContext();
        public RenderPipelineDynamicContext dynamicContextWrite = new RenderPipelineDynamicContext();

        public List<CBuffer> CBs_Bone = new List<CBuffer>();

        public ProcessingList processingList = new ProcessingList();

        public PSODesc SkinningDesc = new PSODesc
        {
            blendState = BlendState.none,
            cullMode = CullMode.Back,
            depthBias = 0,
            slopeScaledDepthBias = 0,
            dsvFormat = Format.Unknown,
            inputLayout = InputLayout.mmd,
            ptt = PrimitiveTopologyType.Point,
            rtvFormat = Format.Unknown,
            renderTargetCount = 0,
            streamOutput = true,
            wireFrame = false,
        };

        public Format gBufferFormat = Format.R16G16B16A16_UNorm;
        public Format outputFormat = Format.R16G16B16A16_Float;
        public Format swapChainFormat = Format.R8G8B8A8_UNorm;

        public string currentPassSetting1 = "ms-appx:///Samples\\samplePasses.coocoox";

        public Int2 screenSize;
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


        public void Reload()
        {
            graphicsContext.Reload(graphicsDevice);
            graphicsContext1.Reload(graphicsDevice);
            currentChannel = AddVisualChannel("main");
            AddVisualChannel("second");

        }

        public VisualChannel AddVisualChannel(string name)
        {
            var visualChannel = new VisualChannel();
            visualChannels[name] = visualChannel;
            visualChannel.Name = name;
            visualChannel.graphicsContext = graphicsContext;
            return visualChannel;
        }

        public void BeginDynamicContext(bool enableDisplay, Settings settings)
        {
            mainCaches.GetPassSetting("ms-appx:///Samples\\samplePasses.coocoox");
            mainCaches.GetPassSetting("ms-appx:///Samples\\sampleDeferredPasses.coocoox");
            dynamicContextWrite.FrameBegin();
            dynamicContextWrite.EnableDisplay = enableDisplay;
            dynamicContextWrite.settings = settings;

            dynamicContextWrite.currentPassSetting = mainCaches.GetPassSetting(currentPassSetting1);

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


        LinearPool<MMDMesh> meshPool = new LinearPool<MMDMesh>();
        public Dictionary<MMDRendererComponent, MMDMesh> meshOverride = new Dictionary<MMDRendererComponent, MMDMesh>();

        public void UpdateGPUResource()
        {
            meshPool.Reset();
            meshOverride.Clear();
            var bigBuffer = MemUtil.MegaBuffer;
            #region Update bone data
            int count = dynamicContextRead.renderers.Count;
            while (CBs_Bone.Count < count)
            {
                CBuffer constantBuffer = new CBuffer();
                graphicsDevice.InitializeCBuffer(constantBuffer, c_entityDataBufferSize);
                CBs_Bone.Add(constantBuffer);
            }
            _Data1 data1 = new _Data1();
            Span<Vector3> d3 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12));
            for (int i = 0; i < count; i++)
            {
                var rendererComponent = dynamicContextRead.renderers[i];
                var mesh = meshPool.Get(() =>
                 {
                     var mesh1 = new MMDMesh();
                     return mesh1;
                 });
                var originModel = GetModelPack(rendererComponent.meshPath);
                mesh.ReloadIndex<int>(originModel.vertexCount, null);
                meshOverride[rendererComponent] = mesh;

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


                //const int parallelSize = 1024;
                //Parallel.For(0, (originModel.vertexCount + parallelSize - 1) / parallelSize, u =>
                //{
                //    Span<Vector3> _d3 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12));
                //    int from = u * parallelSize;
                //    int to = Math.Min(from + parallelSize, originModel.vertexCount);
                //    for (int j = from; j < to; j++)
                //    {
                //        Vector3 pos0 = rendererComponent.meshPosData1[j];
                //        Vector3 pos1 = Vector3.Zero;
                //        int a = 0;
                //        for (int k = 0; k < 4; k++)
                //        {
                //            int boneId = originModel.boneId[j * 4 + k];
                //            if (boneId >= rendererComponent.bones.Count) break;
                //            Matrix4x4 trans = rendererComponent.boneMatricesData[boneId];
                //            float weight = originModel.boneWeights[j * 4 + k];
                //            pos1 += Vector3.Transform(pos0, trans) * weight;
                //            a++;
                //        }
                //        if (a > 0)
                //            _d3[j] = Vector3.Transform(pos1, world);
                //        else
                //            _d3[j] = Vector3.Transform(pos0, world);
                //    }
                //});
                //mesh.AddBuffer<Vector3>(d3.Slice(0, originModel.vertexCount), 0);

                //Parallel.For(0, (originModel.vertexCount + parallelSize - 1) / parallelSize, u =>
                //{
                //    Span<Vector3> _d3 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12));
                //    int from = u * parallelSize;
                //    int to = Math.Min(from + parallelSize, originModel.vertexCount);
                //    for (int j = from; j < to; j++)
                //    {
                //        Vector3 norm0 = originModel.normal[j];
                //        Vector3 norm1 = Vector3.Zero;
                //        int a = 0;
                //        for (int k = 0; k < 4; k++)
                //        {
                //            int boneId = originModel.boneId[j * 4 + k];
                //            if (boneId >= rendererComponent.bones.Count) break;
                //            Matrix4x4 trans = rendererComponent.boneMatricesData[boneId];
                //            float weight = originModel.boneWeights[j * 4 + k];
                //            norm1 += Vector3.TransformNormal(norm0, trans) * weight;
                //            a++;
                //        }
                //        if (a > 0)
                //            _d3[j] = Vector3.Normalize(Vector3.TransformNormal(norm1, world));
                //        else
                //            _d3[j] = Vector3.Normalize(Vector3.TransformNormal(norm0, world));
                //    }
                //});

                //mesh.AddBuffer<Vector3>(d3.Slice(0, originModel.vertexCount), 1);
                //graphicsContext.UploadMesh(mesh);


                //mesh.AddBuffer<Vector3>(rendererComponent.meshPosData1, 0);
                //graphicsContext.UploadMesh(mesh);

                graphicsContext.BeginUpdateMesh(mesh);
                graphicsContext.UpdateMesh<Vector3>(mesh, rendererComponent.meshPosData1, 0);
                graphicsContext.EndUpdateMesh(mesh);
            }
            #endregion
        }

        public void PreConfig()
        {
            if (!Initilized) return;
            ConfigVisualChannels();
            ConfigPassSettings(dynamicContextRead.currentPassSetting);
            foreach (var visualChannel in visualChannels.Values)
            {
                PrepareRenderTarget(dynamicContextRead.currentPassSetting, visualChannel);
            }
        }
        public void PrepareRenderTarget(PassSetting passSetting, VisualChannel visualChannel)
        {
            if (passSetting == null) return;

            var outputSize = visualChannel.outputSize;
            foreach (var rt in passSetting.RenderTargets)
            {
                string rtName = string.Format("SceneView/{0}/{1}", visualChannel.Name, rt.Name);
                if (!RTs.TryGetValue(rtName, out var tex2d))
                {
                    tex2d = new Texture2D();
                    RTs[rtName] = tex2d;
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
                    if (rt.Format == Format.D16_UNorm || rt.Format == Format.D24_UNorm_S8_UInt || rt.Format == Format.D32_Float)
                        tex2d.ReloadAsDepthStencil(x, y, rt.Format);
                    else
                        tex2d.ReloadAsRTVUAV(x, y, rt.Format);
                    graphicsContext.UpdateRenderTexture(tex2d);
                }
            }
        }

        public void ReloadScreenSizeResources()
        {
            screenSize.X = Math.Max((int)Math.Round(graphicsDevice.GetOutputSize().X), 1);
            screenSize.Y = Math.Max((int)Math.Round(graphicsDevice.GetOutputSize().Y), 1);

            dpi = graphicsDevice.GetDpi();
            logicScale = dpi / 96.0f;
        }

        public void ConfigVisualChannels()
        {
            foreach (var visualChannel1 in visualChannels.Values)
            {
                if (visualChannel1.outputSize.X != visualChannel1.FinalOutput.width || visualChannel1.outputSize.Y != visualChannel1.FinalOutput.height)
                {
                    visualChannel1.OutputRTV.ReloadAsRTVUAV(visualChannel1.outputSize.X, visualChannel1.outputSize.Y, outputFormat);
                    graphicsContext.UpdateRenderTexture(visualChannel1.OutputRTV);
                    mainCaches.SetTexture(visualChannel1.GetTexName("Output"), visualChannel1.OutputRTV);

                    visualChannel1.FinalOutput.ReloadAsRTVUAV(visualChannel1.outputSize.X, visualChannel1.outputSize.Y, swapChainFormat);
                    graphicsContext.UpdateRenderTexture(visualChannel1.FinalOutput);
                    mainCaches.SetTexture(visualChannel1.GetTexName("FinalOutput"), visualChannel1.FinalOutput);
                }
            }
        }

        public bool Initilized = false;
        public Task LoadTask;
        public async Task ReloadDefalutResources()
        {
            processingList.AddObject(TextureLoading, 1, 1, new Vector4(0, 1, 1, 1));
            processingList.AddObject(TextureError, 1, 1, new Vector4(1, 0, 1, 1));

            Uploader upTexEnvCube = new Uploader();
            upTexEnvCube.TextureCubePure(32, 32, new Vector4[] { new Vector4(0.4f, 0.32f, 0.32f, 1), new Vector4(0.32f, 0.4f, 0.32f, 1), new Vector4(0.4f, 0.4f, 0.4f, 1), new Vector4(0.32f, 0.4f, 0.4f, 1), new Vector4(0.4f, 0.4f, 0.32f, 1), new Vector4(0.32f, 0.32f, 0.4f, 1) });
            processingList.AddObject(new TextureCubeUploadPack(SkyBox, upTexEnvCube));

            IrradianceMap.ReloadAsRTVUAV(32, 32, 1, Format.R32G32B32A32_Float);
            ReflectMap.ReloadAsRTVUAV(1024, 1024, 7, Format.R16G16B16A16_Float);

            SkyBoxChanged = true;
            graphicsContext.UpdateRenderTexture(IrradianceMap);
            graphicsContext.UpdateRenderTexture(ReflectMap);

            ndcQuadMesh.ReloadNDCQuad();
            processingList.AddObject(ndcQuadMesh);

            foreach (var tex2dDef in RPAssetsManager.defaultResource.texture2Ds)
            {
                ReloadTexture2DNoSrgb(tex2dDef.Path);
            }

            Initilized = true;
        }

        public bool ConfigPassSettings(PassSetting passSetting)
        {
            if (passSetting.configured) return true;
            if (!passSetting.Verify()) return false;
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
                    psoDestroy.DelayDestroy(graphicsDevice);
                pso.Initialize(vs, gs, ps);
                RPAssetsManager.PSOs[pipelineState.Name] = pso;
            }
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
            passSetting.renderTargets = passSetting.RenderTargets.Select(u => u.Name).ToHashSet();
            return true;

        }

        public MMDMesh GetMesh(string path) => mainCaches.ModelPackCaches[path].GetMesh();
        public ModelPack GetModelPack(string path) => mainCaches.ModelPackCaches[path];

        public Texture2D _GetTex2DByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (RTs.TryGetValue(name, out var tex))
            {
                return tex;
            }
            else if (mainCaches.TextureCaches.TryGetValue(name, out var tex2))
            {
                return tex2.texture2D;
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
        private void ReloadTexture2DNoSrgb( string uri)
        {
            mainCaches.Texture("ms-appx:///" + uri, false);
        }
    }
}
