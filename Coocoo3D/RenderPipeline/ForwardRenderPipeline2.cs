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
        public override void RenderCamera(RenderPipelineContext context, VisualChannel visualChannel)
        {
            var graphicsContext = visualChannel.graphicsContext;
            var settings = context.dynamicContextRead.settings;
            var rendererComponents = context.dynamicContextRead.renderers;
            var lightings = context.dynamicContextRead.lightings;
            var camera = visualChannel.cameraData;
            var mainCaches = context.mainCaches;
            var passSetting = context.dynamicContextRead.currentPassSetting;
            Texture2D texLoading = mainCaches.GetTexture("Assets/Textures/loading.png");
            Texture2D texError = mainCaches.GetTexture("Assets/Textures/error.png");
            List<LightingData> pointLights = new List<LightingData>();

            #region Lighting
            bool hasMainLight = false;
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

                hasMainLight = true;
            }
            else
                hasMainLight = false;
            for (int i = 0; i < lightings.Count; i++)
            {
                LightingData lighting = lightings[i];
                if (lighting.LightingType == LightingType.Point)
                    pointLights.Add(lighting);
            }
            #endregion

            void _WriteCBV1(CBVSlotRes cbv, PassMatch1 _pass, GPUWriter writer, RuntimeMaterial material, MMDRendererComponent _rc)
            {
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
                        case "Specular":
                            writer.Write(material.innerStruct.Specular);
                            break;
                        case "Diffuse":
                            writer.Write(material.innerStruct.DiffuseColor);
                            break;
                        case "SpecularColor":
                            writer.Write(material.innerStruct.SpecularColor);
                            break;
                        case "AmbientColor":
                            writer.Write(material.innerStruct.AmbientColor);
                            break;
                        case "Fog":
                            writer.Write(settings.FogColor);
                            writer.Write(Math.Max(settings.FogDensity, 0.0001f));
                            writer.Write(settings.FogStartDistance);
                            writer.Write(settings.FogEndDistance);
                            break;
                        case "VolumetricLighting":
                            writer.Write(settings.VolumetricLightingSampleCount);
                            writer.Write(settings.VolumetricLightingDistance);
                            writer.Write(settings.VolumetricLightingIntensity);
                            break;
                        case "CameraPosition":
                            writer.Write(camera.Position);
                            break;
                        case "DeltaTime":
                            writer.Write((float)context.dynamicContextRead.DeltaTime);
                            break;
                        case "Time":
                            writer.Write((float)context.dynamicContextRead.Time);
                            break;
                        case "World":
                            writer.Write(_rc.LocalToWorld);
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
                        case "IndirectMultiplier":
                            writer.Write(settings.SkyBoxLightMultiplier);
                            break;
                        default:
                            var st = _rc?.morphStateComponent;
                            if (st != null && st.stringMorphIndexMap.TryGetValue(s, out int _i))
                            {
                                writer.Write(st.Weights.Computed[_i]);
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
                writer.SetBufferImmediately(graphicsContext, true, cbv.Index);
            }

            Texture2D _Tex(Texture2D _tex)
            {
                if (_tex == null)
                    return texError;
                else
                    return TextureStatusSelect(_tex, texLoading, texError, texError);
            };
            var rpAssets = context.RPAssetsManager;
            var graphicsDevice = context.graphicsDevice;

            UnionShaderParam unionShaderParam = new UnionShaderParam()
            {
                rp = context,
                passSetting = passSetting,
                graphicsContext = graphicsContext,
                visualChannel = visualChannel,
                GPUWriter = new GPUWriter(),
                camera = camera,
                settings = settings,
                relativePath = System.IO.Path.GetDirectoryName(passSetting.path)
            };

            foreach (var combinedPass in passSetting.RenderSequence)
            {
                if (combinedPass.Pass.Camera == "Main")
                {
                }
                else if (combinedPass.Pass.Camera == "ShadowMap")
                {
                    if (!(hasMainLight && settings.EnableShadow)) continue;
                }
                RootSignature rootSignature = mainCaches.GetRootSignature(graphicsDevice, combinedPass.rootSignatureKey);
                unionShaderParam.rootSignature = rootSignature;
                unionShaderParam.passName = combinedPass.Pass.Name;
                graphicsContext.SetRootSignature(rootSignature);

                Texture2D depthStencil = _GetTex2D1(combinedPass.DepthStencil);

                PSODesc passPsoDesc;
                if (combinedPass.RenderTargets == null || combinedPass.RenderTargets.Count == 0)
                {
                    graphicsContext.SetDSV(depthStencil, combinedPass.ClearDepth);
                    passPsoDesc.rtvFormat = Format.Unknown;
                    unionShaderParam.renderTargets = null;
                    unionShaderParam.depthStencil = depthStencil;
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
                    unionShaderParam.renderTargets = renderTargets;
                    unionShaderParam.depthStencil = depthStencil;
                }

                passPsoDesc.blendState = combinedPass.Pass.BlendMode;
                passPsoDesc.cullMode = combinedPass.CullMode;
                passPsoDesc.depthBias = combinedPass.DepthBias;
                passPsoDesc.slopeScaledDepthBias = combinedPass.SlopeScaledDepthBias;
                passPsoDesc.dsvFormat = depthStencil == null ? Format.Unknown : depthStencil.GetFormat();
                passPsoDesc.ptt = PrimitiveTopologyType.Triangle;
                passPsoDesc.renderTargetCount = combinedPass.RenderTargets == null ? 0 : combinedPass.RenderTargets.Count;
                passPsoDesc.wireFrame = false;
                if (combinedPass.DrawObjects)
                {
                    passPsoDesc.inputLayout = InputLayout.mmd;
                    passPsoDesc.wireFrame = context.dynamicContextRead.settings.Wireframe;

                    for (int i = 0; i < rendererComponents.Count; i++)
                    {
                        MMDRendererComponent rendererComponent = rendererComponents[i];
                        graphicsContext.SetMesh(context.GetMesh(rendererComponent.meshPath));
                        graphicsContext.SetMeshVertex(context.meshOverride[rendererComponent]);
                        var PSODraw = combinedPass.PSODefault;
                        unionShaderParam.PSODesc = passPsoDesc;
                        unionShaderParam.PSO = combinedPass.PSODefault;
                        unionShaderParam.renderer = rendererComponent;
                        var Materials = rendererComponent.Materials;
                        foreach (var material in Materials)
                        {
                            if (!FilterObj(context, combinedPass.Filter, rendererComponent, material))
                            {
                                continue;
                            }
                            foreach (var cbv in combinedPass.Pass.CBVs)
                            {
                                _WriteCBV1(cbv, combinedPass, visualChannel.GPUWriter, material, rendererComponent);
                            }
                            _PassSetRes1(material, combinedPass);
                            if (combinedPass.CullMode == 0)
                                passPsoDesc.cullMode = material.DrawFlags.HasFlag(DrawFlag.DrawDoubleFace) ? CullMode.None : CullMode.Back;

                            unionShaderParam.material = material;
                            bool? a = mainCaches.GetUnionShader(passSetting.GetAliases(material.unionShader))?.Invoke(unionShaderParam);
                            if (a != true)
                            {
                                a = mainCaches.GetUnionShader(passSetting.GetAliases(combinedPass.Pass.UnionShader))?.Invoke(unionShaderParam);
                            }
                            if (a != true)
                            {
                                graphicsContext.SetPSO(PSODraw, passPsoDesc);
                                graphicsContext.DrawIndexed(material.indexCount, material.indexOffset, 0);
                            }
                        }
                    }
                }
                else if (combinedPass.Type == "DrawScreen")
                {
                    _PassSetRes1(null, combinedPass);
                    foreach (var cbv in combinedPass.Pass.CBVs)
                    {
                        _WriteCBV1(cbv, combinedPass, visualChannel.GPUWriter, null, null);
                    }

                    passPsoDesc.inputLayout = InputLayout.postProcess;
                    graphicsContext.SetMesh(context.ndcQuadMesh);

                    unionShaderParam.PSODesc = passPsoDesc;
                    unionShaderParam.PSO = combinedPass.PSODefault;

                    unionShaderParam.renderer = null;
                    unionShaderParam.material = null;
                    UnionShader unionShader = mainCaches.GetUnionShader(passSetting.GetAliases(combinedPass.Pass.UnionShader));

                    bool? a = unionShader?.Invoke(unionShaderParam);
                    if (a != true)
                    {
                        graphicsContext.SetPSO(combinedPass.PSODefault, passPsoDesc);
                        graphicsContext.DrawIndexed(context.ndcQuadMesh.GetIndexCount(), 0, 0);
                    }
                    else
                    {

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

                            graphicsContext.SetSRVTSlot(_Tex(tex2D), resd.Index);
                        }
                    }
            }
            Texture2D _GetTex2D(RuntimeMaterial material, string name)
            {
                if (string.IsNullOrEmpty(name))
                    return null;

                if (name == "_Output0") return visualChannel.OutputRTV;
                Texture2D tex2D = null;
                if (material != null && material.textures.TryGetValue(name, out string texPath) && mainCaches.TextureCaches.TryGetValue(texPath, out var texPack))
                    tex2D = texPack.texture2D;

                if (tex2D == null)
                {
                    if (passSetting.RenderTargets.ContainsKey(name))
                        tex2D = context._GetTex2DByName(string.Format("SceneView/{0}/{1}", visualChannel.Name, name));
                    else
                        tex2D = context._GetTex2DByName(name);
                }
                return tex2D;
            }
            Texture2D _GetTex2D1(string name)
            {
                return _GetTex2D(null, name);
            }
        }
        static bool FilterObj(RenderPipelineContext context, string filter, MMDRendererComponent renderer, RuntimeMaterial material)
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

        static void _WriteCBV(CBVSlotRes cbv, UnionShaderParam unionShaderParam, int slot)
        {
            var material = unionShaderParam.material;
            var context = unionShaderParam.rp;
            var writer = unionShaderParam.GPUWriter;
            var camera = unionShaderParam.camera;
            var settings = unionShaderParam.settings;
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
                    case "Specular":
                        writer.Write(material.innerStruct.Specular);
                        break;
                    case "SpecularColor":
                        writer.Write(material.innerStruct.SpecularColor);
                        break;
                    case "AmbientColor":
                        writer.Write(material.innerStruct.AmbientColor);
                        break;
                    case "Transparent":
                        writer.Write(material.Transparent ? 1 : 0);
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
                    case "World":
                        writer.Write(unionShaderParam.renderer.LocalToWorld);
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
                            var depthStencil = unionShaderParam.depthStencil;
                            var renderTargets = unionShaderParam.renderTargets;
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
                    //case "DirectionalLight":
                    //    if (lightings.Count > 0)
                    //    {
                    //        var lstruct = lightings[0].GetLStruct();

                    //        writer.Write(lightCameraMatrix0);
                    //        writer.Write(lstruct.PosOrDir);
                    //        writer.Write((int)lstruct.Type);
                    //        writer.Write(lstruct.Color);
                    //    }
                    //    else
                    //    {
                    //        writer.Write(Matrix4x4.Identity);
                    //        writer.Write(new Vector4());
                    //        writer.Write(new Vector4());
                    //    }
                    //    break;
                    //case "PointLights4":
                    //    {
                    //        int count = Math.Min(pointLights.Count, 4);
                    //        for (int pli = 0; pli < count; pli++)
                    //        {
                    //            var lstruct = pointLights[pli].GetLStruct();
                    //            writer.Write(lstruct.PosOrDir);
                    //            writer.Write((int)lstruct.Type);
                    //            writer.Write(lstruct.Color);
                    //        }
                    //        for (int i = 0; i < 4 - count; i++)
                    //        {
                    //            writer.Write(new Vector4());
                    //            writer.Write(new Vector4());
                    //        }
                    //    }
                    //    break;
                    case "IndirectMultiplier":
                        writer.Write(settings.SkyBoxLightMultiplier);
                        break;
                }
            }
            writer.SetBufferImmediately(unionShaderParam.graphicsContext, false, slot);
        }
    }
}