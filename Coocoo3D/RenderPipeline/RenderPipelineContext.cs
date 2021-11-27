using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Numerics;
using Coocoo3D.Present;
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
using Coocoo3D.Utility;
using Vortice.DXGI;

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
        public RecordSettings recordSettings;

        public long LatestRenderTime;

        public void RequireRender(bool updateEntities)
        {
            NeedRender = 10;
        }

        public void RequireRender()
        {
            NeedRender = 10;
        }
    }

    public class RenderPipelineContext : IDisposable
    {
        const int c_entityDataBufferSize = 65536;

        public MainCaches mainCaches = new MainCaches();

        public Dictionary<string, VisualChannel> visualChannels = new Dictionary<string, VisualChannel>();

        public VisualChannel currentChannel;

        public Dictionary<string, Texture2D> RTs = new Dictionary<string, Texture2D>();

        public bool RequireResize;
        public Vector2 NewSize;
        public bool SkyBoxChanged = false;
        public int skyBoxQuality = 0;
        public string skyBoxName = "_SkyBox";
        public string skyBoxOriTex = "Assets/Textures/adams_place_bridge_2k.jpg";

        public MMDMesh ndcQuadMesh = new MMDMesh();
        public int frameRenderCount;

        public RPAssetsManager RPAssetsManager = new RPAssetsManager();
        public GraphicsDevice graphicsDevice = new GraphicsDevice();
        public GraphicsContext graphicsContext = new GraphicsContext();

        public ReadBackTexture2D ReadBackTexture2D = new ReadBackTexture2D();

        public RenderPipelineDynamicContext dynamicContextRead = new RenderPipelineDynamicContext();
        public RenderPipelineDynamicContext dynamicContextWrite = new RenderPipelineDynamicContext();

        public List<CBuffer> CBs_Bone = new List<CBuffer>();

        public ProcessingList processingList = new ProcessingList();

        public Format gBufferFormat = Format.R16G16B16A16_UNorm;
        public Format outputFormat = Format.R16G16B16A16_Float;
        public Format swapChainFormat = Format.R8G8B8A8_UNorm;

        public string currentPassSetting1 = "Samples\\samplePasses.coocoox";

        public Int2 screenSize;
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
            currentChannel = AddVisualChannel("main");

            SkyBoxChanged = true;

            ndcQuadMesh.ReloadNDCQuad();
            processingList.AddObject(ndcQuadMesh);
            mainCaches.GetPassSetting("Samples\\samplePasses.coocoox");
            mainCaches.GetPassSetting("Samples\\sampleDeferredPasses.coocoox");
            currentPassSetting1 = Path.GetFullPath(currentPassSetting1);
        }

        public void DelayAddVisualChannel(string name)
        {
            delayAddVc.Enqueue(name);
        }
        Queue<string> delayAddVc = new Queue<string>();

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
            dynamicContextWrite.FrameBegin();
            dynamicContextWrite.EnableDisplay = enableDisplay;
            dynamicContextWrite.settings = settings;

            dynamicContextWrite.currentPassSetting = mainCaches.GetPassSetting(currentPassSetting1);
            dynamicContextWrite.passSettingPath = currentPassSetting1;

            dynamicContextWrite.frameRenderIndex = frameRenderCount;
            frameRenderCount++;
        }

        public CBuffer GetBoneBuffer(MMDRendererComponent rendererComponent)
        {
            return CBs_Bone[dynamicContextRead.findRenderer[rendererComponent]];
        }

        LinearPool<MMDMesh> meshPool = new LinearPool<MMDMesh>();
        public Dictionary<MMDRendererComponent, MMDMesh> meshOverride = new Dictionary<MMDRendererComponent, MMDMesh>();

        public void UpdateGPUResource()
        {
            meshPool.Reset();
            meshOverride.Clear();
            #region Update bone data
            int count = dynamicContextRead.renderers.Count;
            while (CBs_Bone.Count < count)
            {
                CBuffer constantBuffer = new CBuffer();
                graphicsDevice.InitializeCBuffer(constantBuffer, c_entityDataBufferSize);
                CBs_Bone.Add(constantBuffer);
            }
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

                Matrix4x4 world = Matrix4x4.CreateFromQuaternion(rendererComponent.rotation) * Matrix4x4.CreateTranslation(rendererComponent.position);

                graphicsContext.UpdateResource<Matrix4x4>(CBs_Bone[i], rendererComponent.boneMatricesData);

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
            screenSize.X = Math.Max((int)Math.Round(graphicsDevice.GetOutputSize().X), 1);
            screenSize.Y = Math.Max((int)Math.Round(graphicsDevice.GetOutputSize().Y), 1);

            if (!Initialized) return;
            if (delayAddVc.TryDequeue(out var vcName))
            {
                AddVisualChannel(vcName);
            }
            ConfigVisualChannels();
            ConfigPassSettings(dynamicContextRead.currentPassSetting, dynamicContextRead.passSettingPath);
            foreach (var visualChannel in visualChannels.Values)
            {
                PrepareRenderTarget(dynamicContextRead.currentPassSetting, visualChannel);
            }
        }
        public void PrepareRenderTarget(PassSetting passSetting, VisualChannel visualChannel)
        {
            if (passSetting == null) return;

            var outputSize = visualChannel.outputSize;
            foreach (var rt in passSetting.RenderTargets.Values)
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

        public void ConfigVisualChannels()
        {
            foreach (var visualChannel1 in visualChannels.Values)
            {
                var outputSize = visualChannel1.outputSize;
                if (outputSize.X != visualChannel1.FinalOutput.width || outputSize.Y != visualChannel1.FinalOutput.height)
                {
                    visualChannel1.OutputRTV.ReloadAsRTVUAV(outputSize.X, outputSize.Y, outputFormat);
                    graphicsContext.UpdateRenderTexture(visualChannel1.OutputRTV);
                    mainCaches.SetTexture(visualChannel1.GetTexName("Output"), visualChannel1.OutputRTV);

                    visualChannel1.FinalOutput.ReloadAsRTVUAV(outputSize.X, outputSize.Y, swapChainFormat);
                    graphicsContext.UpdateRenderTexture(visualChannel1.FinalOutput);
                    mainCaches.SetTexture(visualChannel1.GetTexName("FinalOutput"), visualChannel1.FinalOutput);
                }
            }
        }

        public bool Initialized = false;
        public Task LoadTask;
        public void ReloadDefalutResources()
        {
            RPAssetsManager.LoadAssets();
            foreach (var tex2dDef in RPAssetsManager.defaultResource.texture2Ds)
            {
                mainCaches.Texture(tex2dDef.Path, false);
            }

            Initialized = true;
        }

        public bool ConfigPassSettings(PassSetting passSetting, string passPath)
        {
            if (passSetting.configured) return true;
            if (!passSetting.Verify()) return false;
            passSetting.path = passPath;
            string path1 = Path.GetDirectoryName(passPath);
            if (passSetting.VertexShaders != null)
                foreach (var shader in passSetting.VertexShaders)
                    passSetting.aliases[shader.Name] = Path.GetFullPath(shader.Path, path1);
            if (passSetting.GeometryShaders != null)
                foreach (var shader in passSetting.GeometryShaders)
                    passSetting.aliases[shader.Name] = Path.GetFullPath(shader.Path, path1);
            if (passSetting.PixelShaders != null)
                foreach (var shader in passSetting.PixelShaders)
                    passSetting.aliases[shader.Name] = Path.GetFullPath(shader.Path, path1);
            if (passSetting.UnionShaders != null)
            {
                foreach (var shader in passSetting.UnionShaders)
                {
                    passSetting.aliases[shader.Key] = Path.GetFullPath(shader.Value, path1);
                }
            }
            foreach (var pass in passSetting.RenderSequence)
            {
                if (pass.Type == "Swap") continue;

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
                            stringBuilder.Append('C');
                        count = cbv.Index + 1;
                    }
                }
                if (pass.Pass.SRVs != null)
                {
                    int count = 0;
                    foreach (var srv in pass.Pass.SRVs)
                    {
                        for (int i = count; i < srv.Index + 1; i++)
                            stringBuilder.Append('s');
                        count = srv.Index + 1;
                    }
                }
                if (pass.Pass.UAVs != null)
                {
                    int count = 0;
                    foreach (var uav in pass.Pass.UAVs)
                    {
                        for (int i = count; i < uav.Index + 1; i++)
                            stringBuilder.Append('u');
                        count = uav.Index + 1;
                    }
                }
                pass.rootSignatureKey = stringBuilder.ToString();
                VertexShader vs = null;
                GeometryShader gs = null;
                PixelShader ps = null;
                if (pass.Pass.VertexShader != null)
                    vs = mainCaches.GetVertexShader(passSetting.aliases[pass.Pass.VertexShader]);
                if (pass.Pass.GeometryShader != null)
                    gs = mainCaches.GetGeometryShader(passSetting.aliases[pass.Pass.GeometryShader]);
                if (pass.Pass.PixelShader != null)
                    ps = mainCaches.GetPixelShader(passSetting.aliases[pass.Pass.PixelShader]);
                PSO pso = new PSO();
                pso.Initialize(vs, gs, ps);
                pass.PSODefault = pso;
                RPAssetsManager.PSOs[pass.Pass.Name] = pso;
            }
            passSetting.configured = true;
            return true;

        }

        public MMDMesh GetMesh(string path) => mainCaches.GetModel(path).GetMesh();
        public ModelPack GetModelPack(string path) => mainCaches.GetModel(path);

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
            return mainCaches.GetTextureCube(name);
        }

        public void Dispose()
        {
            foreach (var rt in RTs)
            {
                rt.Value.Dispose();
            }
            RPAssetsManager.Dispose();
        }
    }
}
