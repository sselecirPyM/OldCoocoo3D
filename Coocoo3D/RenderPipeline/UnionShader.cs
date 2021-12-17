using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Coocoo3D.Components;
using Coocoo3DGraphics;
using Coocoo3D.RenderPipeline.Wrap;
using System.Numerics;

namespace Coocoo3D.RenderPipeline
{
    public delegate bool UnionShader(UnionShaderParam param);
    public class UnionShaderParam
    {
        public RenderPipelineContext rp;
        public RuntimeMaterial material;
        public MMDRendererComponent renderer;
        public PassSetting passSetting;

        public List<MMDRendererComponent> renderers;

        public RenderSequence renderSequence;

        public GraphicsContext graphicsContext;
        public VisualChannel visualChannel;
        public RootSignature rootSignature;
        public PSODesc PSODesc;
        public string passName;
        public string relativePath;
        public GPUWriter GPUWriter;
        public Core.Settings settings;
        public Texture2D[] renderTargets;
        public Texture2D depthStencil;

        public Texture2D texLoading;
        public Texture2D texError;

        public Dictionary<string, object> customValue = new Dictionary<string, object>();

        public Texture2D GetTex2D(string name, RuntimeMaterial material = null)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            if (name == "_Output0") return visualChannel.OutputRTV;
            if (material != null && passSetting.ShowTextures?.ContainsKey(name) == true)
            {
                if (material.textures.TryGetValue(name, out string texPath))
                    return rp._GetTex2DByName(texPath);
                else
                    return null;
            }
            if (passSetting.ShowSettingTextures?.ContainsKey(name) == true)
            {
                if (settings.textures.TryGetValue(name, out string texPath))
                    return rp._GetTex2DByName(texPath);
                else
                    return null;
            }

            Texture2D tex2D;
            if (passSetting.RenderTargets.ContainsKey(name))
                tex2D = rp._GetTex2DByName(visualChannel.GetTexName(name));
            else
                tex2D = rp._GetTex2DByName(name);
            return tex2D;
        }

        public void WriteGPU(List<string> datas, GPUWriter writer)
        {
            if (datas == null || datas.Count == 0) return;
            var camera = visualChannel.cameraData;
            var drp = rp.dynamicContextRead;
            foreach (var s in datas)
            {
                switch (s)
                {
                    case "DeltaTime":
                        writer.Write((float)rp.dynamicContextRead.DeltaTime);
                        break;
                    case "Time":
                        writer.Write((float)rp.dynamicContextRead.Time);
                        break;
                    case "World":
                        writer.Write(renderer.LocalToWorld);
                        break;
                    case "CameraPosition":
                        writer.Write(camera.Position);
                        break;
                    case "Camera":
                        writer.Write(camera.vpMatrix);
                        break;
                    case "CameraInvert":
                        writer.Write(camera.pvMatrix);
                        break;
                    case "WidthHeight":
                        {
                            if (renderTargets != null && renderTargets.Length > 0)
                            {
                                Texture2D renderTarget = renderTargets[0];
                                writer.Write(renderTarget.GetWidth());
                                writer.Write(renderTarget.GetHeight());
                            }
                            else if (depthStencil != null)
                            {
                                writer.Write(depthStencil.GetWidth());
                                writer.Write(depthStencil.GetHeight());
                            }
                            else
                            {
                                writer.Write(0);
                                writer.Write(0);
                            }
                        }
                        break;
                    case "DirectionalLightMatrix0":
                        {
                            if (drp.directionalLights.Count > 0)
                                writer.Write(drp.GetLightMatrix(camera.pvMatrix, 0));
                            else
                                writer.Write(Matrix4x4.Identity);
                        }
                        break;
                    case "DirectionalLightMatrix1":
                        {
                            if (drp.directionalLights.Count > 0)
                                writer.Write(drp.GetLightMatrix(camera.pvMatrix, 1));
                            else
                                writer.Write(Matrix4x4.Identity);
                        }
                        break;
                    case "DirectionalLightMatrix2":
                        {
                            if (drp.directionalLights.Count > 0)
                                writer.Write(drp.GetLightMatrix(camera.pvMatrix, 2));
                            else
                                writer.Write(Matrix4x4.Identity);
                        }
                        break;
                    case "DirectionalLightMatrix3":
                        {
                            if (drp.directionalLights.Count > 0)
                                writer.Write(drp.GetLightMatrix(camera.pvMatrix, 3));
                            else
                                writer.Write(Matrix4x4.Identity);
                        }
                        break;
                    case "DirectionalLight":
                        {
                            var directionalLights = drp.directionalLights;
                            if (directionalLights.Count > 0)
                            {
                                writer.Write(directionalLights[0].Direction);
                                writer.Write((int)0);
                                writer.Write(directionalLights[0].Color);
                            }
                            else
                            {
                                writer.Write(new Vector4());
                                writer.Write(new Vector4());
                            }
                            break;
                        }
                    case "PointLights4":
                        {
                            var pointLights = drp.pointLights;
                            const int lightCount = 4;
                            int count = 0;
                            if (material != null)
                            {
                                for (int i = 0; i < pointLights.Count; i++)
                                {
                                    Vortice.Mathematics.BoundingSphere boundingSphere = new Vortice.Mathematics.BoundingSphere(pointLights[i].Position, pointLights[i].Range);
                                    if (material.boundingBox.Contains(boundingSphere) != Vortice.Mathematics.ContainmentType.Disjoint)
                                    {
                                        writer.Write(pointLights[i].Position);
                                        writer.Write((int)1);
                                        writer.Write(pointLights[i].Color);
                                        writer.Write((int)1);
                                        count++;
                                        if (count >= lightCount) break;
                                    }
                                }
                            }
                            else
                            {
                                for (int i = 0; i < Math.Min(lightCount, pointLights.Count); i++)
                                {
                                    writer.Write(pointLights[i].Position);
                                    writer.Write((int)1);
                                    writer.Write(pointLights[i].Color);
                                    writer.Write((int)1);
                                    count++;
                                }
                            }
                            for (int i = 0; i < lightCount - count; i++)
                            {
                                writer.Write(new Vector4());
                                writer.Write(new Vector4());
                            }
                        }
                        break;

                    default:
                        object settingValue = null;
                        if (material != null)
                            settingValue = drp.GetSettingsValue(material, s);
                        settingValue ??= drp.GetSettingsValue(s);
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
    }
}
