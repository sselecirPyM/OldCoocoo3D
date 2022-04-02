using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Numerics;
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
    public class RecordSettings
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
            if (updateEntities)
                RequireResetPhysics = true;
            NeedRender = 10;
        }
    }

    public class RenderPipelineContext : IDisposable
    {
        public MainCaches mainCaches = new MainCaches();

        public Dictionary<string, VisualChannel> visualChannels = new();

        public VisualChannel currentChannel;

        private Dictionary<string, Texture2D> RTs = new();
        private Dictionary<string, TextureCube> RTCs = new();
        private Dictionary<string, GPUBuffer> dynamicBuffers = new();

        public Dictionary<string, object> customData = new();

        public bool SkyBoxChanged = false;

        public string skyBoxName = "_SkyBox";
        public string skyBoxTex = "Assets/Textures/adams_place_bridge_2k.jpg";

        public void SetSkyBox(string path)
        {
            if (skyBoxTex == path) return;
            skyBoxTex = path;
            SkyBoxChanged = true;
        }

        public Mesh quadMesh = new Mesh();
        public int frameRenderCount;

        public GraphicsDevice graphicsDevice;
        public GraphicsContext graphicsContext = new GraphicsContext();

        public RenderPipelineDynamicContext dynamicContextRead = new();
        public RenderPipelineDynamicContext dynamicContextWrite = new();

        public List<CBuffer> CBs_Bone = new();

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

        public bool recording = false;

        public bool CPUSkinning = false;

        public void Load()
        {
            graphicsDevice = new GraphicsDevice(swapChainFormat);
            graphicsContext.Reload(graphicsDevice);
            currentChannel = AddVisualChannel("main");

            SkyBoxChanged = true;

            quadMesh.ReloadNDCQuad();
            mainCaches.MeshReadyToUpload.Enqueue(quadMesh);
            DirectoryInfo directoryInfo = new DirectoryInfo("Samples");
            foreach (var file in directoryInfo.GetFiles("*.coocoox"))
                mainCaches.GetPassSetting(file.FullName);
            currentPassSetting1 = Path.GetFullPath(currentPassSetting1);
        }

        Queue<string> delayAddVisualChannel = new();
        Queue<string> delayRemoveVisualChannel = new();
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
            if (visualChannels.Remove(name, out var vc))
            {
                if (vc == currentChannel)
                    currentChannel = visualChannels["main"];
                vc.Dispose();
            }
        }

        public void BeginDynamicContext(Scene scene)
        {
            dynamicContextWrite.FrameBegin();
            dynamicContextWrite.settings = scene.settings.GetClone();

            dynamicContextWrite.currentPassSetting = mainCaches.GetPassSetting(currentPassSetting1);

            dynamicContextWrite.frameRenderIndex = frameRenderCount;
            dynamicContextWrite.CPUSkinning = CPUSkinning;
            frameRenderCount++;
        }

        public CBuffer GetBoneBuffer(MMDRendererComponent rendererComponent)
        {
            return CBs_Bone[dynamicContextRead.findRenderer[rendererComponent]];
        }

        LinearPool<Mesh> meshPool = new();
        public Dictionary<MMDRendererComponent, Mesh> meshOverride = new();
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
                constantBuffer.Mutable = true;
                CBs_Bone.Add(constantBuffer);
            }

            if (CPUSkinning)
            {
                int bufferSize = 0;
                foreach (var renderer in dynamicContextRead.renderers)
                {
                    if (renderer.skinning)
                        bufferSize = Math.Max(GetModelPack(renderer.meshPath).vertexCount, bufferSize);
                }
                bufferSize *= 12;
                if (bufferSize > bigBuffer.Length)
                    bigBuffer = new byte[bufferSize];
            }
            for (int i = 0; i < count; i++)
            {
                var renderer = dynamicContextRead.renderers[i];
                var model = GetModelPack(renderer.meshPath);
                var mesh = meshPool.Get(() => new Mesh());
                mesh.ReloadIndex<int>(model.vertexCount, null);
                meshOverride[renderer] = mesh;
                if (!renderer.skinning) continue;


                if (CPUSkinning)
                {
                    const int parallelSize = 1024;
                    Span<Vector3> d3 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12));
                    Parallel.For(0, (model.vertexCount + parallelSize - 1) / parallelSize, u =>
                    {
                        Span<Vector3> _d3 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12));
                        int from = u * parallelSize;
                        int to = Math.Min(from + parallelSize, model.vertexCount);
                        for (int j = from; j < to; j++)
                        {
                            Vector3 pos0 = renderer.meshPosData1[j];
                            Vector3 pos1 = Vector3.Zero;
                            int a = 0;
                            for (int k = 0; k < 4; k++)
                            {
                                int boneId = model.boneId[j * 4 + k];
                                if (boneId >= renderer.bones.Count) break;
                                Matrix4x4 trans = renderer.boneMatricesData[boneId];
                                float weight = model.boneWeights[j * 4 + k];
                                pos1 += Vector3.Transform(pos0, trans) * weight;
                                a++;
                            }
                            if (a > 0)
                                _d3[j] = pos1;
                            else
                                _d3[j] = pos0;
                        }
                    });
                    //graphicsContext.BeginUpdateMesh(mesh);
                    //graphicsContext.UpdateMesh(mesh, d3.Slice(0, model.vertexCount), 0);
                    mesh.AddBuffer(d3.Slice(0, model.vertexCount), 0);//for compatibility

                    Parallel.For(0, (model.vertexCount + parallelSize - 1) / parallelSize, u =>
                    {
                        Span<Vector3> _d3 = MemoryMarshal.Cast<byte, Vector3>(new Span<byte>(bigBuffer, 0, bigBuffer.Length / 12 * 12));
                        int from = u * parallelSize;
                        int to = Math.Min(from + parallelSize, model.vertexCount);
                        for (int j = from; j < to; j++)
                        {
                            Vector3 norm0 = model.normal[j];
                            Vector3 norm1 = Vector3.Zero;
                            int a = 0;
                            for (int k = 0; k < 4; k++)
                            {
                                int boneId = model.boneId[j * 4 + k];
                                if (boneId >= renderer.bones.Count) break;
                                Matrix4x4 trans = renderer.boneMatricesData[boneId];
                                float weight = model.boneWeights[j * 4 + k];
                                norm1 += Vector3.TransformNormal(norm0, trans) * weight;
                                a++;
                            }
                            if (a > 0)
                                _d3[j] = Vector3.Normalize(norm1);
                            else
                                _d3[j] = Vector3.Normalize(norm0);
                        }
                    });

                    //graphicsContext.UpdateMesh(mesh, d3.Slice(0, model.vertexCount), 1);
                    mesh.AddBuffer(d3.Slice(0, model.vertexCount), 1);//for compatibility

                    //graphicsContext.EndUpdateMesh(mesh);
                    graphicsContext.UploadMesh(mesh);//for compatibility
                    for (int k = 0; k < renderer.boneMatricesData.Length; k++)
                        renderer.boneMatricesData[k] = Matrix4x4.Transpose(renderer.boneMatricesData[k]);
                    graphicsContext.UpdateResource<Matrix4x4>(CBs_Bone[i], renderer.boneMatricesData);
                }
                else
                {
                    if (renderer.meshNeedUpdate)
                    {
                        graphicsContext.BeginUpdateMesh(mesh);
                        graphicsContext.UpdateMesh<Vector3>(mesh, renderer.meshPosData1, 0);
                        graphicsContext.EndUpdateMesh(mesh);
                    }
                    for (int k = 0; k < renderer.boneMatricesData.Length; k++)
                        renderer.boneMatricesData[k] = Matrix4x4.Transpose(renderer.boneMatricesData[k]);
                    graphicsContext.UpdateResource<Matrix4x4>(CBs_Bone[i], renderer.boneMatricesData);
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
                }
            }
            dynamicContextRead.currentPassSetting.Initialize();
            foreach (var visualChannel in visualChannels.Values)
            {
                PrepareRenderTarget(dynamicContextRead.currentPassSetting, visualChannel);
            }
            foreach (var visualChannel in visualChannels.Values)
            {
                visualChannel.Onframe(this);
            }
        }
        public void PrepareRenderTarget(PassSetting passSetting, VisualChannel visualChannel)
        {
            if (passSetting == null) return;
            var settings = dynamicContextRead.settings;

            var outputSize = visualChannel.outputSize;
            if (passSetting.RenderTargets != null)
            {
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
                    if (rt.Source == "OutputSize")
                    {
                        x = (int)(outputSize.X * rt.Multiplier + 0.5f);
                        y = (int)(outputSize.Y * rt.Multiplier + 0.5f);
                    }
                    else if (rt.Source == "ShadowMapSize")
                    {
                        x = (int)(settings.ShadowMapResolution * rt.Multiplier + 0.5f);
                        y = (int)(settings.ShadowMapResolution * rt.Multiplier + 0.5f);
                    }
                    else
                    {
                        x = (int)rt.width;
                        y = (int)rt.height;
                    }
                    if (tex2d.width != x || tex2d.height != y || tex2d.GetFormat() != rt.Format)
                    {
                        if (rt.Format == Format.D16_UNorm || rt.Format == Format.D24_UNorm_S8_UInt || rt.Format == Format.D32_Float)
                            tex2d.ReloadAsDepthStencil(x, y, rt.Format);
                        else
                            tex2d.ReloadAsRTVUAV(x, y, rt.Format);
                        graphicsContext.UpdateRenderTexture(tex2d);
                    }
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
                    if (rt.Source == "OutputSize")
                    {
                        x = (int)(outputSize.X * rt.Multiplier + 0.5f);
                        y = (int)(outputSize.Y * rt.Multiplier + 0.5f);
                    }
                    else if (rt.Source == "ShadowMapSize")
                    {
                        x = (int)(settings.ShadowMapResolution * rt.Multiplier + 0.5f);
                        y = (int)(settings.ShadowMapResolution * rt.Multiplier + 0.5f);
                    }
                    else
                    {
                        x = (int)rt.width;
                        y = (int)rt.height;
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
                    if (rt.width != buffer.size)
                    {
                        buffer.size = (int)rt.width;
                        graphicsContext.UpdateDynamicBuffer(buffer);
                    }
                }
            }
        }

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

        public T GetPersistentValue<T>(string name, T defaultValue)
        {
            if (customData.TryGetValue(name, out object val) && val is T val1)
                return val1;
            return defaultValue;
        }

        public void SetPersistentValue<T>(string name, T value)
        {
            customData[name] = value;
        }

        public void AfterRender()
        {

        }

        public void Dispose()
        {
            foreach (var rt in RTs)
                rt.Value.Dispose();
            RTs.Clear();
        }
    }
}
