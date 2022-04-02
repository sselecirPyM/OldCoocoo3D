using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Components;
using Coocoo3DGraphics;
using Coocoo3D.RenderPipeline.Wrap;
using System.Numerics;
using Coocoo3D.Present;

namespace Coocoo3D.RenderPipeline
{
    public delegate bool UnionShader(UnionShaderParam param);
    public class UnionShaderParam
    {
        public RenderPipelineContext rp;
        public RenderPipelineDynamicContext drp { get => rp.dynamicContextRead; }
        public RenderMaterial material;
        internal MMDRendererComponent renderer;
        internal MeshRendererComponent meshRenderer;
        public PassSetting passSetting;

        public List<MMDRendererComponent> renderers { get => drp.renderers; }
        public List<MeshRendererComponent> meshRenderers { get => drp.meshRenderers; }

        public List<DirectionalLightData> directionalLights;
        public List<PointLightData> pointLights;

        public RenderSequence renderSequence;
        public Pass pass;

        public GraphicsContext graphicsContext;
        public VisualChannel visualChannel;

        public string passName;
        public string relativePath;
        public GPUWriter GPUWriter;
        public Core.Settings settings;
        public Texture2D[] renderTargets;
        public Texture2D depthStencil;
        public MainCaches mainCaches;

        public Texture2D texLoading;
        public Texture2D texError;

        public GraphicsDevice graphicsDevice { get => rp.graphicsDevice; }

        public string skyBoxFile { get => rp.skyBoxTex; }

        public CameraData camera { get => visualChannel.cameraData; }

        public bool recording { get => rp.recording; }

        internal Dictionary<string, object> customValue = new Dictionary<string, object>();
        internal Dictionary<string, object> gpuValueOverride = new Dictionary<string, object>();

        public T GetCustomValue<T>(string name, T defaultValue)
        {
            if (customValue.TryGetValue(name, out object val) && val is T val1)
                return val1;
            return defaultValue;
        }

        public void SetCustomValue<T>(string name, T value)
        {
            customValue[name] = value;
        }

        public T GetPersistentValue<T>(string name, T defaultValue)
        {
            return rp.GetPersistentValue<T>(name, defaultValue);
        }

        public void SetPersistentValue<T>(string name, T value)
        {
            rp.SetPersistentValue(name, value);
        }

        public T GetGPUValueOverride<T>(string name, T defaultValue)
        {
            if (gpuValueOverride.TryGetValue(name, out object val) && val is T val1)
                return val1;
            return defaultValue;
        }

        public void SetGPUValueOverride<T>(string name, T value)
        {
            gpuValueOverride[name] = value;
        }

        public double deltaTime { get => rp.dynamicContextRead.DeltaTime; }
        public double realDeltaTime { get => rp.dynamicContextRead.RealDeltaTime; }
        public double time { get => rp.dynamicContextRead.Time; }

        public Mesh GetMesh(string path) => mainCaches.GetModel(path).GetMesh();

        public Random _random;

        public Random random { get => _random ??= new Random(rp.dynamicContextRead.frameRenderIndex); }

        public bool CPUSkinning { get => rp.CPUSkinning; set => rp.CPUSkinning = value; }

        public bool IsRayTracingSupport { get => graphicsDevice.IsRayTracingSupport(); }

        public object GetSettingsValue(string name)
        {
            if (!passSetting.ShowSettingParameters.TryGetValue(name, out var parameter))
                return null;
            if (settings.Parameters.TryGetValue(name, out object val) && Validate(parameter, val))
                return val;
            return parameter.defaultValue;
        }

        public object GetSettingsValue(RenderMaterial material, string name)
        {
            if (!passSetting.ShowParameters.TryGetValue(name, out var parameter))
                return null;
            if (material.Parameters.TryGetValue(name, out object val) && Validate(parameter, val))
                return val;
            return parameter.defaultValue;
        }

        static bool Validate(PassParameter parameter, object val)
        {
            if (parameter.Type == "float" || parameter.Type == "sliderFloat" && val is float)
                return true;
            if (parameter.Type == "int" || parameter.Type == "sliderInt" && val is int)
                return true;
            if (parameter.Type == "bool" && val is bool)
                return true;
            if (parameter.Type == "float2" && val is Vector2)
                return true;
            if (parameter.Type == "float3" || parameter.Type == "color3" && val is Vector3)
                return true;
            if (parameter.Type == "float4" || parameter.Type == "color4" && val is Vector4)
                return true;
            return false;
        }

        public CBuffer GetBoneBuffer()
        {
            return rp.GetBoneBuffer(renderer);
        }

        public Texture2D GetTex2D(string name, RenderMaterial material = null)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            if (name == "_Output0") return visualChannel.OutputRTV;

            string path = GetTex2DPath(name, material);
            if (string.IsNullOrEmpty(path))
                return null;
            return rp._GetTex2DByName(path);
        }

        public string GetTex2DPath(string name, RenderMaterial material = null)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            if (material != null && passSetting.ShowTextures?.ContainsKey(name) == true)
            {
                if (material.textures.TryGetValue(name, out string texPath))
                    return texPath;
                else
                    return null;
            }
            if (passSetting.ShowSettingTextures?.ContainsKey(name) == true)
            {
                if (settings.textures.TryGetValue(name, out string texPath))
                    return texPath;
                else
                    return null;
            }

            string tex2DPath;
            if (passSetting.RenderTargets.ContainsKey(name))
            {
                tex2DPath = _getTextureName(name);
            }
            else
            {
                name = passSetting.GetAliases(name);
                tex2DPath = name;
            }
            return tex2DPath;
        }

        public TextureCube GetTexCube(string name, RenderMaterial material = null)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            TextureCube tex2D;
            if (passSetting.RenderTargetCubes.TryGetValue(name, out var renderTarget))
            {
                tex2D = rp._GetTexCubeByName(visualChannel.GetTexName(name, renderTarget));
            }
            else
            {
                name = passSetting.GetAliases(name);

                tex2D = rp._GetTexCubeByName(name);
            }
            return tex2D;
        }

        public string _getBufferName(string name, RenderMaterial material = null)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            if (passSetting.DynamicBuffers.TryGetValue(name, out var renderTarget))
                return visualChannel.GetTexName(name, renderTarget);
            else
                return name;
        }

        public string _getTextureName(string name, RenderMaterial material = null)
        {
            if (string.IsNullOrEmpty(name))
                return name;
            if (passSetting.RenderTargets.TryGetValue(name, out var renderTarget))
                return visualChannel.GetTexName(name, renderTarget);
            else
                return name;
        }

        public GPUBuffer GetBuffer(string name, RenderMaterial material = null)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            return rp._GetBufferByName(_getBufferName(name, material));
        }

        public void WriteCBV(SlotRes cbv)
        {
            WriteGPU(cbv.Datas, GPUWriter);
            GPUWriter.SetBufferImmediately(graphicsContext, false, cbv.Index);
        }

        public byte[] GetCBVData(SlotRes cbv)
        {
            WriteGPU(cbv.Datas, GPUWriter);
            return GPUWriter.GetData();
        }

        public void WriteGPU(List<string> datas, GPUWriter writer)
        {
            if (datas == null || datas.Count == 0) return;
            var camera = visualChannel.cameraData;
            var drp = rp.dynamicContextRead;
            foreach (var s in datas)
            {
                if (gpuValueOverride.TryGetValue(s, out object gpuValue))
                {
                    if (gpuValue is float f1)
                        writer.Write(f1);
                    else if (gpuValue is Vector2 f2)
                        writer.Write(f2);
                    else if (gpuValue is Vector3 f3)
                        writer.Write(f3);
                    else if (gpuValue is Vector4 f4)
                        writer.Write(f4);
                    else if (gpuValue is int i1)
                        writer.Write(i1);
                    else if (gpuValue is Matrix4x4 m)
                        writer.Write(m);
                    continue;
                }
                switch (s)
                {
                    case "RealDeltaTime":
                        writer.Write((float)realDeltaTime);
                        break;
                    case "DeltaTime":
                        writer.Write((float)deltaTime);
                        break;
                    case "Time":
                        writer.Write((float)time);
                        break;
                    case "World":
                        if (renderer != null)
                            writer.Write(renderer.LocalToWorld);
                        else if (meshRenderer != null)
                            writer.Write(meshRenderer.transform.GetMatrix());
                        else
                            writer.Write(Matrix4x4.Identity);
                        break;
                    case "CameraPosition":
                        writer.Write(camera.Position);
                        break;
                    case "Camera":
                        writer.Write(camera.vpMatrix);
                        break;
                    case "CameraInfo":
                        writer.Write(camera.far);
                        writer.Write(camera.near);
                        writer.Write(camera.Fov);
                        writer.Write(camera.AspectRatio);
                        break;
                    case "CameraInvert":
                        writer.Write(camera.pvMatrix);
                        break;
                    case "WidthHeight":
                        if (renderTargets != null && renderTargets.Length > 0)
                        {
                            Texture2D renderTarget = renderTargets[0];
                            writer.Write(renderTarget.width);
                            writer.Write(renderTarget.height);
                        }
                        else if (depthStencil != null)
                        {
                            writer.Write(depthStencil.width);
                            writer.Write(depthStencil.height);
                        }
                        else
                        {
                            writer.Write(0);
                            writer.Write(0);
                        }
                        break;
                    case "DirectionalLightMatrix0":
                        if (drp.directionalLights.Count > 0)
                            writer.Write(drp.GetLightMatrix(camera.pvMatrix, 0));
                        else
                            writer.Write(Matrix4x4.Identity);
                        break;
                    case "DirectionalLightMatrix1":
                        if (drp.directionalLights.Count > 0)
                            writer.Write(drp.GetLightMatrix(camera.pvMatrix, 1));
                        else
                            writer.Write(Matrix4x4.Identity);
                        break;
                    case "DirectionalLight":
                        {
                            var directionalLights = drp.directionalLights;
                            if (directionalLights.Count > 0)
                            {
                                writer.Write(directionalLights[0].Direction);
                                writer.Write((int)0);
                                writer.Write(directionalLights[0].Color);
                                writer.Write((int)0);
                            }
                            else
                            {
                                writer.Write(new Vector4());
                                writer.Write(new Vector4());
                            }
                            break;
                        }
                    case "PointLights":
                        {
                            var pointLights = drp.pointLights;
                            for (int i = 0; i < pointLights.Count; i++)
                            {
                                writer.Write(pointLights[i].Position);
                                writer.Write((int)1);
                                writer.Write(pointLights[i].Color);
                                writer.Write(pointLights[i].Range);
                            }
                        }
                        break;
                    case "RandomI":
                        writer.Write(random.Next());
                        break;
                    case "RandomF":
                        writer.Write((float)random.NextDouble());
                        break;
                    case "RandomF2":
                        writer.Write(new Vector2((float)random.NextDouble(), (float)random.NextDouble()));
                        break;
                    case "RandomF3":
                        writer.Write(new Vector3((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()));
                        break;
                    case "RandomF4":
                        writer.Write(new Vector4((float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble(), (float)random.NextDouble()));
                        break;

                    default:
                        object settingValue = null;
                        if (material != null)
                            settingValue = GetSettingsValue(material, s);
                        settingValue ??= GetSettingsValue(s);
                        if (settingValue != null)
                        {
                            if (settingValue is float f1)
                                writer.Write(f1);
                            if (settingValue is Vector2 f2)
                                writer.Write(f2);
                            if (settingValue is Vector3 f3)
                                writer.Write(f3);
                            if (settingValue is Vector4 f4)
                                writer.Write(f4);
                            if (settingValue is int i1)
                                writer.Write(i1);
                            continue;
                        }
                        break;
                }
            }
        }

        public IEnumerable<MeshRenderable> MeshRenderables(bool setmesh = true)
        {
            var drp = rp.dynamicContextRead;
            foreach (var renderer in renderers)
            {
                var mesh = GetMesh(renderer.meshPath);
                var meshOverride = rp.meshOverride[renderer];
                if (setmesh)
                    graphicsContext.SetMesh(mesh, meshOverride);
                this.renderer = renderer;
                this.meshRenderer = null;
                foreach (var material in renderer.Materials)
                {
                    this.material = material;
                    var renderable = new MeshRenderable()
                    {
                        mesh = mesh,
                        meshOverride = meshOverride,
                        transform = renderer.LocalToWorld,
                        gpuSkinning = renderer.skinning && !drp.CPUSkinning,
                    };
                    WriteRenderable(ref renderable, material);
                    yield return renderable;
                }
            }
            foreach (var renderer in meshRenderers)
            {
                var mesh = GetMesh(renderer.meshPath);
                if (setmesh)
                    graphicsContext.SetMesh(mesh);
                this.renderer = null;
                this.meshRenderer = renderer;
                foreach (var material in renderer.Materials)
                {
                    this.material = material;
                    var renderable = new MeshRenderable()
                    {
                        mesh = mesh,
                        meshOverride = null,
                        transform = renderer.transform.GetMatrix(),
                        gpuSkinning = false,
                    };
                    WriteRenderable(ref renderable, material);
                    yield return renderable;
                }
            }
            this.renderer = null;
            this.meshRenderer = null;
            material = null;
        }

        void WriteRenderable(ref MeshRenderable renderable, RenderMaterial material)
        {
            renderable.material = material;
            renderable.indexStart = material.indexOffset;
            renderable.indexCount = material.indexCount;
            renderable.vertexStart = material.vertexStart;
            renderable.vertexCount = material.vertexCount;
        }

        public IEnumerable<RenderSequence> RenderSequences()
        {
            return passSetting.RenderSequence;
        }

        public IEnumerable<MeshRenderable> EffectRenderables()
        {
            yield break;
            foreach (var effect in rp.dynamicContextRead.particleEffects)
            {
                yield return new MeshRenderable()
                {
                    type = RenderableType.Particle
                };
            }
        }

        public void DrawScreenQuad()
        {
            graphicsContext.SetMesh(rp.quadMesh);
            graphicsContext.DrawIndexed(6, 0, 0);
        }

        public void DrawRenderable(in MeshRenderable renderable)
        {
            graphicsContext.DrawIndexed(renderable.indexCount, renderable.indexStart, renderable.vertexStart);
        }

        public void SetSRVs(List<SlotRes> SRVs, RenderMaterial material = null)
        {
            if (SRVs == null) return;
            foreach (var resd in SRVs)
            {
                if (resd.ResourceType == "TextureCube")
                {
                    graphicsContext.SetSRVTSlot(rp._GetTexCubeByName(resd.Resource), resd.Index);
                }
                else if (resd.ResourceType == "Texture2D")
                {
                    if (resd.Flags.HasFlag(SlotResFlag.Linear))
                        graphicsContext.SetSRVTSlotLinear(TextureFallBack(GetTex2D(resd.Resource, material)), resd.Index);
                    else
                        graphicsContext.SetSRVTSlot(TextureFallBack(GetTex2D(resd.Resource, material)), resd.Index);
                }
                else if (resd.ResourceType == "Buffer")
                {
                    graphicsContext.SetSRVTSlot(GetBuffer(resd.Resource), resd.Index);
                }
            }
        }

        public void SetUAVs(List<SlotRes> UAVs, RenderMaterial material = null)
        {
            if (UAVs == null) return;
            foreach (var resd in UAVs)
            {
                if (resd.ResourceType == "TextureCube")
                {
                    graphicsContext.SetUAVTSlot(rp._GetTexCubeByName(resd.Resource), resd.Index);
                }
                else if (resd.ResourceType == "Texture2D")
                {
                    //if (resd.Flags.HasFlag(SlotResFlag.Linear))
                    //    graphicsContext.SetUAVTSlotLinear(TextureFallBack(GetTex2D(resd.Resource, material)), resd.Index);
                    //else
                    graphicsContext.SetUAVTSlot(TextureFallBack(GetTex2D(resd.Resource, material)), resd.Index);
                }
                else if (resd.ResourceType == "Buffer")
                {
                    graphicsContext.SetUAVTSlot(GetBuffer(resd.Resource), resd.Index);
                }
            }
        }

        public void SRVUAVs(List<SlotRes> SRVUAV, Dictionary<int, object> dict, Dictionary<int, int> flags = null, RenderMaterial material = null)
        {
            if (SRVUAV == null) return;
            foreach (var resd in SRVUAV)
            {
                if (resd.ResourceType == "TextureCube")
                {
                    dict[resd.Index] = rp._GetTexCubeByName(resd.Resource);
                }
                else if (resd.ResourceType == "Texture2D")
                {
                    dict[resd.Index] = TextureFallBack(GetTex2D(resd.Resource, material));

                    if (flags != null && resd.Flags.HasFlag(SlotResFlag.Linear))
                    {
                        flags[resd.Index] = 0;
                    }
                }
                else if (resd.ResourceType == "Buffer")
                {
                    dict[resd.Index] = GetBuffer(resd.Resource);
                }
            }
        }

        public bool SwapBuffer(string buf1, string buf2)
        {
            if (string.IsNullOrEmpty(buf1) || string.IsNullOrEmpty(buf2)) return false;
            return rp.SwapBuffer(_getBufferName(buf1), _getBufferName(buf2));
        }

        public bool SwapTexture(string tex1, string tex2)
        {
            if (string.IsNullOrEmpty(tex1) || string.IsNullOrEmpty(tex2)) return false;
            return rp.SwapTexture(_getTextureName(tex1), _getTextureName(tex2));
        }

        public Texture2D TextureFallBack(Texture2D _tex) => TextureStatusSelect(_tex, texLoading, texError, texError);
        public static Texture2D TextureStatusSelect(Texture2D texture, Texture2D loading, Texture2D unload, Texture2D error)
        {
            if (texture == null) return error;
            if (texture.Status == GraphicsObjectStatus.loaded)
                return texture;
            else if (texture.Status == GraphicsObjectStatus.loading)
                return loading;
            else if (texture.Status == GraphicsObjectStatus.unload)
                return unload;
            else
                return error;
        }

        public PSODesc GetPSODesc()
        {
            PSODesc psoDesc;
            if (renderSequence.RenderTargets == null || renderSequence.RenderTargets.Count == 0)
            {
                psoDesc.rtvFormat = Vortice.DXGI.Format.Unknown;
            }
            else
            {
                psoDesc.rtvFormat = GetTex2D(renderSequence.RenderTargets[0]).GetFormat();
            }
            psoDesc.dsvFormat = depthStencil == null ? Vortice.DXGI.Format.Unknown : depthStencil.GetFormat();

            psoDesc.blendState = pass.BlendMode;
            psoDesc.cullMode = renderSequence.CullMode;
            psoDesc.depthBias = renderSequence.DepthBias;
            psoDesc.slopeScaledDepthBias = renderSequence.SlopeScaledDepthBias;
            psoDesc.primitiveTopologyType = Vortice.Direct3D12.PrimitiveTopologyType.Triangle;
            psoDesc.renderTargetCount = renderSequence.RenderTargets == null ? 0 : renderSequence.RenderTargets.Count;
            psoDesc.wireFrame = false;

            if (renderSequence.Type == null)
            {
                psoDesc.inputLayout = InputLayout.mmd;
                psoDesc.wireFrame = settings.Wireframe;
                if (material != null)
                    psoDesc.cullMode = material.DrawDoubleFace ? Vortice.Direct3D12.CullMode.None : Vortice.Direct3D12.CullMode.Back;
                else
                    psoDesc.cullMode = Vortice.Direct3D12.CullMode.None;
            }
            else
            {
                psoDesc.inputLayout = InputLayout.postProcess;
            }
            return psoDesc;
        }

        public void DispatchPass(RenderSequence sequence)
        {
            renderSequence = sequence;
            HybirdRenderPipeline.DispatchPass(this);
        }
    }
}
