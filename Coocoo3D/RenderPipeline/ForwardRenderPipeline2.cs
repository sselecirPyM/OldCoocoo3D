using Coocoo3D.Components;
using Coocoo3D.Numerics;
using Coocoo3D.Present;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using Coocoo3D.RenderPipeline.Wrap;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Coocoo3D.RenderPipeline
{
    public class ForwardRenderPipeline2 : RenderPipeline
    {
        [ThreadStatic]
        static Random random = new Random();

        struct _Counters
        {
            public int material;
            public int vertex;
        }

        public override void PrepareRenderData(RenderPipelineContext context, VisualChannel visualChannel)
        {
            if (random == null)
                random = new Random();

            var graphicsContext = visualChannel.graphicsContext;
            var settings = context.dynamicContextRead.settings;
            var rendererComponents = context.dynamicContextRead.renderers;
            var lightings = context.dynamicContextRead.lightings;
            var camera = visualChannel.cameraData;
            List<LightingData> pointLights = new List<LightingData>();

            #region Lighting

            MiscProcess.Process(context, visualChannel.GPUWriter);
            Matrix4x4 lightCameraMatrix0 = Matrix4x4.Identity;
            Matrix4x4 invLightCameraMatrix0 = Matrix4x4.Identity;
            if (lightings.Count > 0 && lightings[0].LightingType == LightingType.Directional)
            {
                if (context.dynamicContextRead.volumes.Count == 0)
                    lightCameraMatrix0 = lightings[0].GetLightingMatrix(camera.pvMatrix);
                else
                    lightCameraMatrix0 = lightings[0].GetLightingMatrix(visualChannel, context.dynamicContextRead);


                Matrix4x4.Invert(lightCameraMatrix0, out invLightCameraMatrix0);

                visualChannel.customDataInt["mainLight"] = 1;
            }
            else
                visualChannel.customDataInt["mainLight"] = 0;
            for (int i = 1; i < lightings.Count; i++)
            {
                LightingData lighting = lightings[i];
                if (lighting.LightingType == LightingType.Point)
                    pointLights.Add(lighting);
            }
            #endregion

            int matC = 0;
            foreach (var combinedPass in context.dynamicContextRead.currentPassSetting.RenderSequence)
            {
                if (combinedPass.Pass.Camera == "Main")
                {
                }
                else if (combinedPass.Pass.Camera == "ShadowMap")
                {
                    if (!(visualChannel.customDataInt["mainLight"] == 1 && settings.EnableShadow)) continue;
                }
                if (combinedPass.Pass.CBVs.Count == 0) continue;
                if (combinedPass.DrawObjects)
                {
                    foreach (var rendererComponent in rendererComponents)
                        foreach (var material in rendererComponent.Materials)
                        {
                            if (!FilterObj(context, combinedPass.Filter, rendererComponent, material))
                            {
                                continue;
                            }
                            foreach (var cbv in combinedPass.Pass.CBVs)
                            {
                                visualChannel.customDataInt1[matC] = _WriteCBV(cbv, combinedPass, visualChannel.GPUWriter, material, rendererComponent);
                                matC++;
                            }
                        }
                }
                else if (combinedPass.Pass != null)
                {
                    foreach (var cbv in combinedPass.Pass.CBVs)
                    {
                        visualChannel.customDataInt1[matC] = _WriteCBV(cbv, combinedPass, visualChannel.GPUWriter, null, null);
                        matC++;
                    }
                }
            }

            Texture2D _GetTex2D(RuntimeMaterial material, string name)
            {
                Texture2D tex2D = null;
                if (name == "_Output0") return visualChannel.OutputRTV;

                if (material != null && material.textures.TryGetValue(name, out string texPath) && context.mainCaches.TextureCaches.TryGetValue(texPath, out var texPack))
                    tex2D = texPack.texture2D;

                if (tex2D == null)
                {
                    if (context.dynamicContextRead.currentPassSetting.renderTargets.Contains(name))
                        tex2D = context._GetTex2DByName(string.Format("SceneView/{0}/{1}", visualChannel.Name, name));
                    else
                        tex2D = context._GetTex2DByName(name);
                }
                return tex2D;
            }

            //着色器可读取数据
            int _WriteCBV(CBVSlotRes cbv, PassMatch1 _pass, GPUWriter writer, RuntimeMaterial material, MMDRendererComponent _rc)
            {
                int result = writer.BufferBegin();

                foreach (var s in cbv.Datas)
                {
                    switch (s)
                    {
                        case "Metallic":
                            writer.Write(material.innerStruct.Metallic);
                            break;
                        case "Roughness":
                            writer.Write(material.innerStruct.Roughness);
                            break;
                        case "Emission":
                            writer.Write(material.innerStruct.Emission);
                            break;
                        case "Diffuse":
                            writer.Write(material.innerStruct.DiffuseColor);
                            break;
                        case "SpecularColor":
                            writer.Write(material.innerStruct.SpecularColor);
                            break;
                        case "Specular":
                            writer.Write(material.innerStruct.Specular);
                            break;
                        case "AmbientColor":
                            writer.Write(material.innerStruct.AmbientColor);
                            break;
                        case "Transparent":
                            writer.Write(material.Transparent ? 1 : 0);
                            break;
                        case "CameraPosition":
                            writer.Write(camera.Pos);
                            break;
                        case "DrawFlags":
                            writer.Write((int)material.DrawFlags);
                            break;
                        case "DeltaTime":
                            writer.Write((float)context.dynamicContextRead.DeltaTime);
                            break;
                        case "Time":
                            writer.Write((float)context.dynamicContextRead.Time);
                            break;
                        case "WidthHeight":
                            {
                                var depthStencil = _GetTex2D(material, _pass.DepthStencil);
                                if (_pass.RenderTargets != null && _pass.RenderTargets.Count > 0)
                                {
                                    Texture2D renderTarget = _GetTex2D(material, _pass.RenderTargets[0]);
                                    writer.Write(renderTarget.GetWidth());
                                    writer.Write(renderTarget.GetHeight());
                                }
                                else if (!string.IsNullOrEmpty(_pass.DepthStencil))
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
                        case "Camera":
                            if (_pass.Pass.Camera == "Main")
                            {
                                writer.Write(camera.vpMatrix);
                            }
                            else if (_pass.Pass.Camera == "ShadowMap")
                            {
                                if (lightings.Count > 0)
                                    writer.Write(lightCameraMatrix0);
                                else
                                    writer.Write(Matrix4x4.Identity);
                            }
                            break;
                        case "CameraInvert":
                            if (_pass.Pass.Camera == "Main")
                            {
                                writer.Write(camera.pvMatrix);
                            }
                            else if (_pass.Pass.Camera == "ShadowMap")
                            {
                                if (lightings.Count > 0)
                                    writer.Write(invLightCameraMatrix0);
                                else
                                    writer.Write(Matrix4x4.Identity);
                            }
                            break;
                        case "DirectionalLight":
                            if (lightings.Count > 0)
                            {
                                var lstruct = lightings[0].GetLStruct();

                                writer.Write(lightCameraMatrix0);
                                writer.Write(lstruct.PosOrDir);
                                writer.Write((int)lstruct.Type);
                                writer.Write(lstruct.Color);
                            }
                            else
                            {
                                writer.Write(Matrix4x4.Identity);
                                writer.Write(new Vector4());
                                writer.Write(new Vector4());
                            }
                            break;
                        case "PointLights4":
                            {
                                int count = Math.Min(pointLights.Count, 4);
                                for (int pli = 0; pli < count; pli++)
                                {
                                    var lstruct = pointLights[pli].GetLStruct();
                                    writer.Write(lstruct.PosOrDir);
                                    writer.Write((int)lstruct.Type);
                                    writer.Write(lstruct.Color);
                                }
                                for (int i = 0; i < 4 - count; i++)
                                {
                                    writer.Write(new Vector4());
                                    writer.Write(new Vector4());
                                }
                            }
                            break;
                        case "PointLights8":
                            {
                                int count = Math.Min(pointLights.Count, 8);
                                for (int pli = 0; pli < count; pli++)
                                {
                                    var lstruct = pointLights[pli].GetLStruct();
                                    writer.Write(lstruct.PosOrDir);
                                    writer.Write((int)lstruct.Type);
                                    writer.Write(lstruct.Color);
                                }
                                for (int i = 0; i < 8 - count; i++)
                                {
                                    writer.Write(new Vector4());
                                    writer.Write(new Vector4());
                                }
                            }
                            break;
                        case "IndirectMultiplier":
                            writer.Write(settings.SkyBoxLightMultiplier);
                            break;
                        case "RandomValue":
                            writer.Write((float)random.NextDouble());
                            break;
                        default:
                            var st = _rc?.morphStateComponent;
                            if (st != null && st.stringMorphIndexMap.TryGetValue(s, out int _i))
                            {
                                writer.Write(st.Weights.Computed[_i]);
                            }
                            else if (_pass.passParameters1 != null && _pass.passParameters1.TryGetValue(s, out float _f1))
                            {
                                writer.Write(_f1);
                            }
                            else if (material != null && material.textures.ContainsKey(s))
                            {
                                writer.Write(1.0f);
                            }
                            else
                                writer.Write(0.0f);
                            break;
                    }
                }
                return result;
            }

        }
        //you can fold local function in your editor
        public override void RenderCamera(RenderPipelineContext context, VisualChannel visualChannel)
        {
            var graphicsContext = visualChannel.graphicsContext;
            var rendererComponents = context.dynamicContextRead.renderers;
            var settings = context.dynamicContextRead.settings;
            var buffer = visualChannel.GPUWriter.GetBuffer(context.graphicsDevice, graphicsContext, true);
            Texture2D texLoading = context.mainCaches.GetTexture("Assets/Textures/loading.png");
            Texture2D texError = context.mainCaches.GetTexture("Assets/Textures/error.png");
            Texture2D _Tex(Texture2D _tex)
            {
                if (_tex == null)
                    return texError;
                else if (_tex is Texture2D _tex1)
                    return TextureStatusSelect(_tex1, texLoading, texError, texError);
                else
                    return _tex;
            };
            var rpAssets = context.RPAssetsManager;
            var graphicsDevice = context.graphicsDevice;

            PSO psoLoading = rpAssets.PSOs["Loading"];
            PSO psoError = rpAssets.PSOs["Error"];

            int matC = 0;
            foreach (var combinedPass in context.dynamicContextRead.currentPassSetting.RenderSequence)
            {
                if (combinedPass.Pass.Camera == "Main")
                {
                }
                else if (combinedPass.Pass.Camera == "ShadowMap")
                {
                    if (!(visualChannel.customDataInt["mainLight"] == 1 && settings.EnableShadow)) continue;
                }
                RootSignature rootSignature = rpAssets.GetRootSignature(graphicsDevice, combinedPass.rootSignatureKey);

                graphicsContext.SetRootSignature(rootSignature);

                Texture2D depthStencil = _GetTex2D1(combinedPass.DepthStencil);

                PSODesc passPsoDesc;
                if (combinedPass.RenderTargets == null || combinedPass.RenderTargets.Count == 0)
                {
                    graphicsContext.SetDSV(depthStencil, combinedPass.ClearDepth);
                    passPsoDesc.rtvFormat = Format.Unknown;
                }
                else
                {
                    Texture2D[] renderTargets = new Texture2D[combinedPass.RenderTargets.Count];
                    for (int i = 0; i < combinedPass.RenderTargets.Count; i++)
                    {
                        renderTargets[i] = _GetTex2D1(combinedPass.RenderTargets[i]);
                    }
                    if (depthStencil != null)
                        graphicsContext.SetRTVDSV(renderTargets, depthStencil, Vector4.Zero, combinedPass.ClearRenderTarget, combinedPass.ClearDepth);
                    else
                        graphicsContext.SetRTV(renderTargets, Vector4.Zero, combinedPass.ClearRenderTarget);
                    passPsoDesc.rtvFormat = renderTargets[0].GetFormat();
                }

                passPsoDesc.blendState = combinedPass.BlendMode;
                passPsoDesc.cullMode = combinedPass.CullMode;
                passPsoDesc.depthBias = combinedPass.DepthBias;
                passPsoDesc.slopeScaledDepthBias = combinedPass.SlopeScaledDepthBias;
                passPsoDesc.dsvFormat = depthStencil == null ? Format.Unknown : depthStencil.GetFormat();
                passPsoDesc.ptt = PrimitiveTopologyType.Triangle;
                passPsoDesc.renderTargetCount = combinedPass.RenderTargets == null ? 0 : combinedPass.RenderTargets.Count;
                passPsoDesc.streamOutput = false;
                passPsoDesc.wireFrame = false;
                if (combinedPass.DrawObjects)
                {
                    passPsoDesc.inputLayout = InputLayout.mmd;
                    passPsoDesc.wireFrame = context.dynamicContextRead.settings.Wireframe;

                    _PassRender(rendererComponents, combinedPass);
                }
                else if (combinedPass.Type == "DrawScreen")
                {
                    _PassSetRes1(null, combinedPass);
                    foreach (var cbv in combinedPass.Pass.CBVs)
                    {
                        graphicsContext.SetCBVRSlot(buffer, visualChannel.customDataInt1[matC] / 256, 0, cbv.Index);
                        matC++;
                    }
                    passPsoDesc.inputLayout = InputLayout.postProcess;
                    SetPipelineStateVariant(graphicsDevice, graphicsContext, rootSignature, passPsoDesc, combinedPass.PSODefault);
                    graphicsContext.SetMesh(context.ndcQuadMesh);
                    graphicsContext.DrawIndexed(context.ndcQuadMesh.GetIndexCount(), 0, 0);
                }
                void _PassRender(List<MMDRendererComponent> _rendererComponents, PassMatch1 _combinedPass)
                {
                    _Counters counterX = new _Counters();
                    for (int i = 0; i < _rendererComponents.Count; i++)
                    {
                        MMDRendererComponent rendererComponent = _rendererComponents[i];
                        graphicsContext.SetCBVRSlot(context.CBs_Bone[i], 0, 0, 0);
                        graphicsContext.SetMesh(context.GetMesh(rendererComponent.meshPath));
                        graphicsContext.SetMeshVertex(context.meshOverride[rendererComponent]);
                        PSO pso = null;

                        var PSODraw = PSOSelect(graphicsDevice, rootSignature, passPsoDesc, pso, psoLoading, _combinedPass.PSODefault, psoError);
                        var Materials = rendererComponent.Materials;
                        int indexOffset = 0;
                        foreach (var material in Materials)
                        {
                            if (!FilterObj(context, _combinedPass.Filter, rendererComponent, material))
                            {
                                counterX.material++;
                                indexOffset += material.indexCount;
                                continue;
                            }
                            foreach (var cbv in _combinedPass.Pass.CBVs)
                            {
                                graphicsContext.SetCBVRSlot(buffer, visualChannel.customDataInt1[matC] / 256, 0, cbv.Index);
                                matC++;
                            }
                            _PassSetRes1(material, _combinedPass);
                            if (_combinedPass.CullMode == 0)
                                passPsoDesc.cullMode = material.DrawFlags.HasFlag(DrawFlag.DrawDoubleFace) ? CullMode.None : CullMode.Back;
                            SetPipelineStateVariant(graphicsDevice, graphicsContext, rootSignature, passPsoDesc, PSODraw);
                            graphicsContext.DrawIndexed(material.indexCount, indexOffset, 0);
                            counterX.material++;
                            indexOffset += material.indexCount;
                        }
                        counterX.vertex += rendererComponent.meshVertexCount;
                    }
                }
            }

            void _PassSetRes1(RuntimeMaterial material, PassMatch1 _combinedPass)
            {
                if (_combinedPass.Pass.SRVs != null)
                    foreach (var resd in _combinedPass.Pass.SRVs)
                    {
                        if (resd.ResourceType == "TextureCube")
                        {
                            graphicsContext.SetSRVTSlot(context._GetTexCubeByName(resd.Resource), resd.Index);
                        }
                        else if (resd.ResourceType == "Texture2D")
                        {
                            Texture2D tex2D = null;
                            tex2D = _GetTex2D(material, resd.Resource);

                            if (tex2D != null)
                            {
                                graphicsContext.SetSRVTSlot(_Tex(tex2D), resd.Index);
                            }
                        }
                    }
            }
            Texture2D _GetTex2D(RuntimeMaterial material, string name)
            {
                if (string.IsNullOrEmpty(name))
                    return null;

                if (name == "_Output0") return visualChannel.OutputRTV;
                Texture2D tex2D = null;
                if (material != null && material.textures.TryGetValue(name, out string texPath) && context.mainCaches.TextureCaches.TryGetValue(texPath, out var texPack))
                    tex2D = texPack.texture2D;

                if (tex2D == null)
                {
                    if (context.dynamicContextRead.currentPassSetting.renderTargets.Contains(name))
                        tex2D = context._GetTex2DByName(string.Format("SceneView/{0}/{1}", visualChannel.Name, name));
                    else
                        tex2D = context._GetTex2DByName(name);
                }
                return tex2D;
            }
            Texture2D _GetTex2D1(string name)
            {
                if (string.IsNullOrEmpty(name))
                    return null;

                if (name == "_Output0") return visualChannel.OutputRTV;
                Texture2D tex2D = null;

                if (tex2D == null)
                {
                    if (context.dynamicContextRead.currentPassSetting.renderTargets.Contains(name))
                        tex2D = context._GetTex2DByName(string.Format("SceneView/{0}/{1}", visualChannel.Name, name));
                    else
                        tex2D = context._GetTex2DByName(name);
                }
                return tex2D;
            }
        }
        bool FilterObj(RenderPipelineContext context, string filter, MMDRendererComponent renderer, RuntimeMaterial material)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            if (filter == "Transparent")
                return material.Transparent;
            if (filter == "Opaque")
                return !material.Transparent;
            if (material.textures.ContainsKey(filter))
                return true;
            return false;
        }
    }
}