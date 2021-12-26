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
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Utility;
using Vortice.DXGI;
using System.Runtime.InteropServices;

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

        public MMDMesh quadMesh = new MMDMesh();
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

        internal Wrap.GPUWriter writerReuse = new Wrap.GPUWriter();

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

        public bool CPUSkinning = true;

        public void Reload()
        {
            graphicsContext.Reload(graphicsDevice);
            currentChannel = AddVisualChannel("main");

            SkyBoxChanged = true;

            quadMesh.ReloadNDCQuad();
            processingList.AddObject(quadMesh);
            mainCaches.GetPassSetting("Samples\\samplePasses.coocoox");
            mainCaches.GetPassSetting("Samples\\sampleDeferredPasses.coocoox");
            currentPassSetting1 = Path.GetFullPath(currentPassSetting1);
        }

        Queue<string> delayAddVisualChannel = new Queue<string>();
        Queue<string> delayRemoveVisualChannel = new Queue<string>();
        public void DelayAddVisualChannel(string name)
        {
            delayAddVisualChannel.Enqueue(name);
        }
        public void DelayRemoveVisualChannel(string name)
        {
            delayRemoveVisualChannel.Enqueue(name);
        }

        public VisualChannel AddVisualChannel(string name)
        {
            var visualChannel = new VisualChannel();
            visualChannels[name] = visualChannel;
            visualChannel.Name = name;
            visualChannel.graphicsContext = graphicsContext;
            return visualChannel;
        }

        public void RemoveVisualChannel(string name)
        {
            visualChannels[name].Dispose();
            visualChannels.Remove(name);
        }

        public void BeginDynamicContext(bool enableDisplay, Scene scene)
        {
            dynamicContextWrite.FrameBegin();
            dynamicContextWrite.EnableDisplay = enableDisplay;
            dynamicContextWrite.settings = scene.settings.GetClone();

            dynamicContextWrite.currentPassSetting = mainCaches.GetPassSetting(currentPassSetting1);
            dynamicContextWrite.currentPassSetting.path = currentPassSetting1;

            dynamicContextWrite.frameRenderIndex = frameRenderCount;
            frameRenderCount++;
        }

        public CBuffer GetBoneBuffer(MMDRendererComponent rendererComponent)
        {
            return CBs_Bone[dynamicContextRead.findRenderer[rendererComponent]];
        }

        LinearPool<MMDMesh> meshPool = new LinearPool<MMDMesh>();
        public Dictionary<MMDRendererComponent, MMDMesh> meshOverride = new Dictionary<MMDRendererComponent, MMDMesh>();
        public byte[] bigBuffer;
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

            int bufferSize = 0;
            foreach (var renderer in dynamicContextRead.renderers)
            {
                bufferSize = Math.Max(GetModelPack(renderer.meshPath).vertexCount, bufferSize);
            }
            bigBuffer = new byte[bufferSize * 12];
            for (int i = 0; i < count; i++)
            {
                var renderer = dynamicContextRead.renderers[i];
                var mesh = meshPool.Get(() =>
                {
                     var mesh1 = new MMDMesh();
                     return mesh1;
                });
                var originModel = GetModelPack(renderer.meshPath);
                mesh.ReloadIndex<int>(originModel.vertexCount, null);
                meshOverride[renderer] = mesh;
                if (!renderer.skinning) continue;


                if (CPUSkinning)
                {
                    Matrix4x4 world = Matrix4x4.CreateFromQuaternion(renderer.rotation) * Matrix4x4.CreateTranslation(renderer.position);
                    const int parallelSize = 1024;
                    Span<Vector3> d3 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12));
                    Parallel.For(0, (originModel.vertexCount + parallelSize - 1) / parallelSize, u =>
                    {
                        Span<Vector3> _d3 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12));
                        int from = u * parallelSize;
                        int to = Math.Min(from + parallelSize, originModel.vertexCount);
                        for (int j = from; j < to; j++)
                        {
                            Vector3 pos0 = renderer.meshPosData1[j];
                            Vector3 pos1 = Vector3.Zero;
                            int a = 0;
                            for (int k = 0; k < 4; k++)
                            {
                                int boneId = originModel.boneId[j * 4 + k];
                                if (boneId >= renderer.bones.Count) break;
                                Matrix4x4 trans = renderer.boneMatricesData[boneId];
                                float weight = originModel.boneWeights[j * 4 + k];
                                pos1 += Vector3.Transform(pos0, trans) * weight;
                                a++;
                            }
                            if (a > 0)
                                _d3[j] = Vector3.Transform(pos1, world);
                            else
                                _d3[j] = Vector3.Transform(pos0, world);
                        }
                    });
                    graphicsContext.BeginUpdateMesh(mesh);
                    graphicsContext.UpdateMesh<Vector3>(mesh, d3.Slice(0, originModel.vertexCount), 0);

                    Parallel.For(0, (originModel.vertexCount + parallelSize - 1) / parallelSize, u =>
                    {
                        Span<Vector3> _d3 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12));
                        int from = u * parallelSize;
                        int to = Math.Min(from + parallelSize, originModel.vertexCount);
                        for (int j = from; j < to; j++)
                        {
                            Vector3 norm0 = originModel.normal[j];
                            Vector3 norm1 = Vector3.Zero;
                            int a = 0;
                            for (int k = 0; k < 4; k++)
                            {
                                int boneId = originModel.boneId[j * 4 + k];
                                if (boneId >= renderer.bones.Count) break;
                                Matrix4x4 trans = renderer.boneMatricesData[boneId];
                                float weight = originModel.boneWeights[j * 4 + k];
                                norm1 += Vector3.TransformNormal(norm0, trans) * weight;
                                a++;
                            }
                            if (a > 0)
                                _d3[j] = Vector3.Normalize(Vector3.TransformNormal(norm1, world));
                            else
                                _d3[j] = Vector3.Normalize(Vector3.TransformNormal(norm0, world));
                        }
                    });

                    graphicsContext.UpdateMesh<Vector3>(mesh, d3.Slice(0, originModel.vertexCount), 1);

                    graphicsContext.EndUpdateMesh(mesh);
                    for (int k = 0; k < renderer.boneMatricesData.Length; k++)
                        renderer.boneMatricesData[k] = Matrix4x4.Transpose(renderer.boneMatricesData[k]);
                    graphicsContext.UpdateResource<Matrix4x4>(CBs_Bone[i], renderer.boneMatricesData);
                }
                else
                {
                    for (int k = 0; k < renderer.boneMatricesData.Length; k++)
                        renderer.boneMatricesData[k] = Matrix4x4.Transpose(renderer.boneMatricesData[k]);
                    graphicsContext.UpdateResource<Matrix4x4>(CBs_Bone[i], renderer.boneMatricesData);
                    if (renderer.meshNeedUpdate)
                    {
                        graphicsContext.BeginUpdateMesh(mesh);
                        graphicsContext.UpdateMesh<Vector3>(mesh, renderer.meshPosData1, 0);
                        graphicsContext.EndUpdateMesh(mesh);
                    }
                }
            }
            #endregion
        }

        public void PreConfig()
        {
            screenSize.X = Math.Max((int)Math.Round(graphicsDevice.GetOutputSize().X), 1);
            screenSize.Y = Math.Max((int)Math.Round(graphicsDevice.GetOutputSize().Y), 1);

            while (delayAddVisualChannel.TryDequeue(out var vcName))
            {
                AddVisualChannel(vcName);
            }
            while (delayRemoveVisualChannel.TryDequeue(out var vcName))
            {
                RemoveVisualChannel(vcName);
            }
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
            foreach (var rt in passSetting.RenderTargets.Values)
            {
                string rtName = visualChannel.GetTexName(rt.Name);
                if (!RTs.TryGetValue(rtName, out var tex2d))
                {
                    tex2d = new Texture2D();
                    RTs[rtName] = tex2d;
                }
                int x;
                int y;
                if (rt.Size.Source == "OutputSize")
                {
                    x = (int)(outputSize.X * rt.Size.Multiplier + 0.5f);
                    y = (int)(outputSize.Y * rt.Size.Multiplier + 0.5f);
                }
                else if (rt.Size.Source == "ShadowMapSize")
                {
                    x = (int)(dynamicContextRead.settings.ShadowMapResolution * rt.Size.Multiplier + 0.5f);
                    y = (int)(dynamicContextRead.settings.ShadowMapResolution * rt.Size.Multiplier + 0.5f);
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

        public void ReloadDefalutResources()
        {
            RPAssetsManager.LoadAssets();
            foreach (var tex2dDef in RPAssetsManager.defaultResource.texture2Ds)
            {
                mainCaches.Texture(tex2dDef.Path, false);
            }
        }

        public bool ConfigPassSettings(PassSetting passSetting)
        {
            if (passSetting.configured) return true;
            if (!passSetting.Verify()) return false;
            string path1 = Path.GetDirectoryName(passSetting.path);
            if (passSetting.VertexShaders != null)
                foreach (var shader in passSetting.VertexShaders)
                    passSetting.aliases[shader.Name] = Path.GetFullPath(shader.Path, path1);
            if (passSetting.GeometryShaders != null)
                foreach (var shader in passSetting.GeometryShaders)
                    passSetting.aliases[shader.Name] = Path.GetFullPath(shader.Path, path1);
            if (passSetting.PixelShaders != null)
                foreach (var shader in passSetting.PixelShaders)
                    passSetting.aliases[shader.Name] = Path.GetFullPath(shader.Path, path1);

            if (passSetting.RayTracingShaders != null)
                foreach (var shader in passSetting.RayTracingShaders)
                    passSetting.aliases[shader.Key] = Path.GetFullPath(shader.Value, path1);

            if (passSetting.UnionShaders != null)
                foreach (var shader in passSetting.UnionShaders)
                    passSetting.aliases[shader.Key] = Path.GetFullPath(shader.Value, path1);

            if (passSetting.Texture2Ds != null)
                foreach (var texture in passSetting.Texture2Ds)
                    passSetting.aliases[texture.Key] = Path.GetFullPath(texture.Value, path1);


            if (passSetting.Dispatcher != null) passSetting.Dispatcher = Path.GetFullPath(passSetting.Dispatcher, path1);
            foreach (var sequence in passSetting.RenderSequence)
            {
                var pass = passSetting.Passes[sequence.Name];
                int SlotComparison(SlotRes x1, SlotRes y1)
                {
                    return x1.Index.CompareTo(y1.Index);
                }
                StringBuilder stringBuilder = new StringBuilder();
                pass.CBVs?.Sort(SlotComparison);
                pass.SRVs?.Sort(SlotComparison);
                pass.UAVs?.Sort(SlotComparison);

                if (pass.CBVs != null)
                {
                    int count = 0;
                    foreach (var cbv in pass.CBVs)
                    {
                        for (int i = count; i < cbv.Index + 1; i++)
                            stringBuilder.Append('C');
                        count = cbv.Index + 1;
                    }
                }
                if (pass.SRVs != null)
                {
                    int count = 0;
                    foreach (var srv in pass.SRVs)
                    {
                        for (int i = count; i < srv.Index + 1; i++)
                            stringBuilder.Append('s');
                        count = srv.Index + 1;
                    }
                }
                if (pass.UAVs != null)
                {
                    int count = 0;
                    foreach (var uav in pass.UAVs)
                    {
                        for (int i = count; i < uav.Index + 1; i++)
                            stringBuilder.Append('u');
                        count = uav.Index + 1;
                    }
                }
                sequence.rootSignatureKey = stringBuilder.ToString();
                VertexShader vs = null;
                GeometryShader gs = null;
                PixelShader ps = null;
                if (pass.VertexShader != null)
                    vs = mainCaches.GetVertexShader(passSetting.aliases[pass.VertexShader]);
                if (pass.GeometryShader != null)
                    gs = mainCaches.GetGeometryShader(passSetting.aliases[pass.GeometryShader]);
                if (pass.PixelShader != null)
                    ps = mainCaches.GetPixelShader(passSetting.aliases[pass.PixelShader]);
                PSO pso = new PSO();
                pso.Initialize(vs, gs, ps);
                sequence.PSODefault = pso;
                RPAssetsManager.PSOs[pass.Name] = pso;
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
            else if (mainCaches.TryGetTexture(name, out var tex2))
            {
                return tex2;
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
                rt.Value.Dispose();
            RTs.Clear();
            RPAssetsManager.Dispose();
        }
    }
}
