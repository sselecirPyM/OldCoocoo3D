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

        private Dictionary<string, Texture2D> RTs = new Dictionary<string, Texture2D>();
        private Dictionary<string, TextureCube> RTCs = new Dictionary<string, TextureCube>();
        private Dictionary<string, GPUBuffer> dynamicBuffers = new Dictionary<string, GPUBuffer>();

        public Dictionary<string, object> customData = new Dictionary<string, object>();

        public bool RequireResize;
        public Vector2 NewSize;
        public bool SkyBoxChanged = false;

        public string skyBoxName = "_SkyBox";
        public string skyBoxOriTex = "Assets/Textures/adams_place_bridge_2k.jpg";

        public MMDMesh quadMesh = new MMDMesh();
        public int frameRenderCount;

        public RPAssetsManager RPAssetsManager = new RPAssetsManager();
        public GraphicsDevice graphicsDevice = new GraphicsDevice();
        public GraphicsContext graphicsContext = new GraphicsContext();

        public RenderPipelineDynamicContext dynamicContextRead = new RenderPipelineDynamicContext();
        public RenderPipelineDynamicContext dynamicContextWrite = new RenderPipelineDynamicContext();

        public List<CBuffer> CBs_Bone = new List<CBuffer>();

        public ProcessingList processingList = new ProcessingList();

        //public Format outputFormat = Format.R16G16B16A16_Float;
        public Format outputFormat = Format.R8G8B8A8_UNorm;
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

        public bool CPUSkinning = false;

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
            var vc = visualChannels[name];
            if (vc == currentChannel)
                currentChannel = visualChannels["main"];
            vc.Dispose();
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
        public byte[] bigBuffer = new byte[0];
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

            if (CPUSkinning)
            {
                int bufferSize = 0;
                foreach (var renderer in dynamicContextRead.renderers)
                {
                    bufferSize = Math.Max(GetModelPack(renderer.meshPath).vertexCount, bufferSize);
                }
                bufferSize *= 12;
                if (bufferSize > bigBuffer.Length)
                    bigBuffer = new byte[bufferSize];
            }
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
                                _d3[j] = pos1;
                            else
                                _d3[j] = pos0;
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
                                _d3[j] = Vector3.Normalize(norm1);
                            else
                                _d3[j] = Vector3.Normalize(norm0);
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
                if (outputSize.X != visualChannel1.OutputRTV.width || outputSize.Y != visualChannel1.OutputRTV.height)
                {
                    visualChannel1.OutputRTV.ReloadAsRTVUAV(outputSize.X, outputSize.Y, outputFormat);
                    graphicsContext.UpdateRenderTexture(visualChannel1.OutputRTV);
                    mainCaches.SetTexture(visualChannel1.GetTexName("Output"), visualChannel1.OutputRTV);

                    //visualChannel1.FinalOutput.ReloadAsRTVUAV(outputSize.X, outputSize.Y, swapChainFormat);
                    //graphicsContext.UpdateRenderTexture(visualChannel1.FinalOutput);
                    //mainCaches.SetTexture(visualChannel1.GetTexName("FinalOutput"), visualChannel1.FinalOutput);
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
            foreach (var rt1 in passSetting.RenderTargets)
            {
                string rtName = visualChannel.GetTexName(rt1.Key, rt1.Value);
                var rt = rt1.Value;
                if (!RTs.TryGetValue(rtName, out var tex2d))
                {
                    tex2d = new Texture2D();
                    tex2d.Name = rtName;
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
                if (tex2d.width != x || tex2d.height != y)
                {
                    if (rt.Format == Format.D16_UNorm || rt.Format == Format.D24_UNorm_S8_UInt || rt.Format == Format.D32_Float)
                        tex2d.ReloadAsDepthStencil(x, y, rt.Format);
                    else
                        tex2d.ReloadAsRTVUAV(x, y, rt.Format);
                    graphicsContext.UpdateRenderTexture(tex2d);
                }
            }
            if (passSetting.RenderTargetCubes != null)
            {
                foreach (var rt1 in passSetting.RenderTargetCubes)
                {
                    string rtName = visualChannel.GetTexName(rt1.Key, rt1.Value);
                    var rt = rt1.Value;
                    if (!RTCs.TryGetValue(rtName, out var texCube))
                    {
                        texCube = new TextureCube();
                        texCube.Name = rtName;
                        RTCs[rtName] = texCube;
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
                    if (texCube.width != x || texCube.height != y)
                    {
                        if (rt.Format == Format.D16_UNorm || rt.Format == Format.D24_UNorm_S8_UInt || rt.Format == Format.D32_Float)
                            texCube.ReloadAsDSV(x, y, rt.Format);
                        else
                            texCube.ReloadAsRTVUAV(x, y, rt.Format);
                        graphicsContext.UpdateRenderTexture(texCube);
                    }
                }
            }
            if (passSetting.DynamicBuffers != null)
            {
                foreach (var rt1 in passSetting.DynamicBuffers)
                {
                    string rtName = visualChannel.GetTexName(rt1.Key, rt1.Value);
                    var rt = rt1.Value;
                    if (!dynamicBuffers.TryGetValue(rtName, out var buffer))
                    {
                        buffer = new GPUBuffer();
                        buffer.Name = rtName;
                        dynamicBuffers[rtName] = buffer;
                    }
                    if (rt.Size.x != buffer.size)
                    {
                        buffer.size = rt.Size.x;
                        graphicsContext.UpdateDynamicBuffer(buffer);
                    }
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

            if (passSetting.RayTracingShaders != null)
                foreach (var shader in passSetting.RayTracingShaders)
                    passSetting.aliases[shader.Key] = Path.GetFullPath(shader.Value, path1);

            if (passSetting.UnionShaders != null)
                foreach (var shader in passSetting.UnionShaders)
                    passSetting.aliases[shader.Key] = Path.GetFullPath(shader.Value, path1);

            if (passSetting.Texture2Ds != null)
                foreach (var texture in passSetting.Texture2Ds)
                    passSetting.aliases[texture.Key] = Path.GetFullPath(texture.Value, path1);

            if (passSetting.Dispatcher != null)
                passSetting.Dispatcher = Path.GetFullPath(passSetting.Dispatcher, path1);
            else
                Console.WriteLine("Missing dispacher.");
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
            if (string.IsNullOrEmpty(name)) return null;
            if (RTCs.TryGetValue(name, out var tex))
            {
                return tex;
            }
            return mainCaches.GetTextureCube(name);
        }
        public GPUBuffer _GetBufferByName(string name)
        {
            if (dynamicBuffers.TryGetValue(name, out var buffer))
            {
                return buffer;
            }
            return null;
        }

        public bool SwapBuffer(string buf1, string buf2)
        {
            if (dynamicBuffers.TryGetValue(buf1, out var buffer1) && dynamicBuffers.TryGetValue(buf2, out var buffer2))
            {
                if (buffer1.size != buffer2.size)
                    return false;
                dynamicBuffers[buf2] = buffer1;
                dynamicBuffers[buf1] = buffer2;
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool SwapTexture(string tex1, string tex2)
        {
            if (RTs.TryGetValue(tex1, out var buffer1) && RTs.TryGetValue(tex2, out var buffer2))
            {
                if (buffer1.width != buffer2.width ||
                    buffer1.height != buffer2.height ||
                    buffer1.format != buffer2.format ||
                    buffer1.dsvFormat != buffer2.dsvFormat ||
                    buffer1.rtvFormat != buffer2.rtvFormat ||
                    buffer1.uavFormat != buffer2.uavFormat)
                    return false;
                RTs[tex2] = buffer1;
                RTs[tex1] = buffer2;
                return true;
            }
            else
            {
                return false;
            }
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
