using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Numerics;
using Coocoo3D.Present;
using Coocoo3D.Utility;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Coocoo3D.RenderPipeline
{
    public class ForwardRenderPipeline2 : RenderPipeline
    {
        public void Reload()
        {
            Ready = true;
        }
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

            var deviceResources = context.graphicsDevice;
            var graphicsDevice = visualChannel.graphicsContext;
            //var cameras = context.dynamicContextRead.cameras;
            var settings = context.dynamicContextRead.settings;
            var rendererComponents = context.dynamicContextRead.renderers;
            var lightings = context.dynamicContextRead.lightings;
            var camera = visualChannel.cameraData;
            var bigBuffer = context.bigBuffer;
            List<LightingData> pointLights = new List<LightingData>();

            #region Lighting
            VolumeComponent volume = null;
            if (context.dynamicContextRead.volumes.Count > 0)
                volume = context.dynamicContextRead.volumes[0];

            Matrix4x4 lightCameraMatrix0 = Matrix4x4.Identity;
            Matrix4x4 invLightCameraMatrix0 = Matrix4x4.Identity;
            if (lightings.Count > 0 && lightings[0].LightingType == LightingType.Directional)
            {
                if (volume == null)
                    lightCameraMatrix0 = lightings[0].GetLightingMatrix(camera.pvMatrix);
                else
                    lightCameraMatrix0 = lightings[0].GetLightingMatrix(new BoundingBox() { position = volume.Position, extension = volume.Size });


                Matrix4x4.Invert(lightCameraMatrix0, out invLightCameraMatrix0);
                lightCameraMatrix0 = Matrix4x4.Transpose(lightCameraMatrix0);
                invLightCameraMatrix0 = Matrix4x4.Transpose(invLightCameraMatrix0);

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

            int numMaterials = 0;
            foreach (MMDRendererComponent v in rendererComponents)
                numMaterials += v.Materials.Count;

            var XBufferGroup = visualChannel.XBufferGroup;

            //int numofBuffer = 0;
            //foreach (var combinedPass in context.dynamicContextRead.currentPassSetting.RenderSequence)
            //    if (combinedPass.Pass?.CBVs != null)
            //        numofBuffer += (combinedPass.DrawObjects ? numMaterials : 1) * combinedPass.Pass.CBVs.Count;
            //context.XBufferGroup.SetSlienceCount(numofBuffer);

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
                                int ofs = _WriteCBV(cbv, combinedPass, bigBuffer, material, rendererComponent);
                                XBufferGroup.UpdateSlience(graphicsDevice, bigBuffer, 0, ofs, matC);
                                matC++;
                            }
                        }
                }
                else if (combinedPass.Pass != null)
                {
                    foreach (var cbv in combinedPass.Pass.CBVs)
                    {
                        int ofs = _WriteCBV(cbv, combinedPass, bigBuffer, null, null);
                        XBufferGroup.UpdateSlience(graphicsDevice, bigBuffer, 0, ofs, matC);
                        matC++;
                    }
                }
            }
            XBufferGroup.UpdateSlienceComplete(graphicsDevice);

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
            int _WriteCBV(CBVSlotRes cbv, PassMatch1 _pass, byte[] _buffer, RuntimeMaterial material, MMDRendererComponent _rc)
            {
                Array.Clear(_buffer, 0, _buffer.Length);
                int ofs = 0;
                foreach (var s in cbv.Datas)
                {
                    switch (s)
                    {
                        case "Metallic":
                            ofs += CooUtility.Write(_buffer, ofs, material.innerStruct.Metallic);
                            break;
                        case "Roughness":
                            ofs += CooUtility.Write(_buffer, ofs, material.innerStruct.Roughness);
                            break;
                        case "Emission":
                            ofs += CooUtility.Write(_buffer, ofs, material.innerStruct.Emission);
                            break;
                        case "Diffuse":
                            ofs += CooUtility.Write(_buffer, ofs, material.innerStruct.DiffuseColor);
                            break;
                        case "SpecularColor":
                            ofs += CooUtility.Write(_buffer, ofs, material.innerStruct.SpecularColor);
                            break;
                        case "Specular":
                            ofs += CooUtility.Write(_buffer, ofs, material.innerStruct.Specular);
                            break;
                        case "AmbientColor":
                            ofs += CooUtility.Write(_buffer, ofs, material.innerStruct.AmbientColor);
                            break;
                        case "ToonIndex":
                            ofs += CooUtility.Write(_buffer, ofs, material.toonIndex);
                            break;
                        case "TextureIndex":
                            ofs += CooUtility.Write(_buffer, ofs, material.texIndex);
                            break;
                        case "Transparent":
                            ofs += CooUtility.Write(_buffer, ofs, material.Transparent ? 1 : 0);
                            break;
                        case "CameraPosition":
                            ofs += CooUtility.Write(_buffer, ofs, camera.Pos);
                            break;
                        case "DrawFlags":
                            ofs += CooUtility.Write(_buffer, ofs, (int)material.DrawFlags);
                            break;
                        case "DeltaTime":
                            ofs += CooUtility.Write(_buffer, ofs, (float)context.dynamicContextRead.DeltaTime);
                            break;
                        case "Time":
                            ofs += CooUtility.Write(_buffer, ofs, (float)context.dynamicContextRead.Time);
                            break;
                        case "WidthHeight":
                            {
                                var depthStencil = _GetTex2D(material, _pass.DepthStencil);
                                if (_pass.RenderTargets != null && _pass.RenderTargets.Count > 0)
                                {
                                    Texture2D renderTarget = _GetTex2D(material, _pass.RenderTargets[0]);
                                    ofs += CooUtility.Write(_buffer, ofs, renderTarget.GetWidth());
                                    ofs += CooUtility.Write(_buffer, ofs, renderTarget.GetHeight());
                                }
                                else if (!string.IsNullOrEmpty(_pass.DepthStencil))
                                {
                                    ofs += CooUtility.Write(_buffer, ofs, depthStencil.GetWidth());
                                    ofs += CooUtility.Write(_buffer, ofs, depthStencil.GetHeight());
                                }
                                else
                                    ofs += sizeof(int) * 2;
                            }
                            break;
                        case "Camera":
                            if (_pass.Pass.Camera == "Main")
                            {
                                ofs += CooUtility.Write(_buffer, ofs, Matrix4x4.Transpose(camera.vpMatrix));
                            }
                            else if (_pass.Pass.Camera == "ShadowMap")
                            {
                                if (lightings.Count > 0)
                                {
                                    ofs += CooUtility.Write(_buffer, ofs, lightCameraMatrix0);
                                }
                                else
                                    ofs += 64;
                            }
                            break;
                        case "CameraInvert":
                            if (_pass.Pass.Camera == "Main")
                            {
                                ofs += CooUtility.Write(_buffer, ofs, Matrix4x4.Transpose(camera.pvMatrix));
                            }
                            else if (_pass.Pass.Camera == "ShadowMap")
                            {
                                if (lightings.Count > 0)
                                {
                                    ofs += CooUtility.Write(_buffer, ofs, invLightCameraMatrix0);
                                }
                                else
                                    ofs += 64;
                            }
                            break;
                        case "DirectionalLight":
                            if (lightings.Count > 0)
                            {
                                var lstruct = lightings[0].GetLStruct();
                                ofs += CooUtility.Write(_buffer, ofs, lightCameraMatrix0);
                                ofs += CooUtility.Write(_buffer, ofs, lstruct.PosOrDir);
                                ofs += CooUtility.Write(_buffer, ofs, lstruct.Type);
                                ofs += CooUtility.Write(_buffer, ofs, lstruct.Color);
                            }
                            else
                                ofs += 96;
                            break;
                        case "PointLights4":
                            {
                                int ofsa = ofs + 128;
                                int count = Math.Min(pointLights.Count, 4);
                                for (int pli = 0; pli < count; pli++)
                                {
                                    var lstruct = pointLights[pli].GetLStruct();
                                    ofs += CooUtility.Write(_buffer, ofs, lstruct.PosOrDir);
                                    ofs += CooUtility.Write(_buffer, ofs, lstruct.Type);
                                    ofs += CooUtility.Write(_buffer, ofs, lstruct.Color);
                                }
                                ofs = ofsa;
                            }
                            break;
                        case "PointLights8":
                            {
                                int ofsa = ofs + 256;
                                int count = Math.Min(pointLights.Count, 8);
                                for (int pli = 0; pli < count; pli++)
                                {
                                    var lstruct = pointLights[pli].GetLStruct();
                                    ofs += CooUtility.Write(_buffer, ofs, lstruct.PosOrDir);
                                    ofs += CooUtility.Write(_buffer, ofs, lstruct.Type);
                                    ofs += CooUtility.Write(_buffer, ofs, lstruct.Color);
                                }
                                ofs = ofsa;
                            }
                            break;
                        case "IndirectMultiplier":
                            ofs += CooUtility.Write(_buffer, ofs, settings.SkyBoxLightMultiplier);
                            break;
                        case "ShadowVolume":
                            if (volume != null)
                            {
                                ofs += CooUtility.Write(_buffer, ofs, volume.Position);
                                ofs += 4;
                                ofs += CooUtility.Write(_buffer, ofs, volume.Size);
                                ofs += 4;
                            }
                            else
                                ofs += 32;
                            break;
                        case "ReflectVolume":
                            if (volume != null)
                            {
                                ofs += CooUtility.Write(_buffer, ofs, volume.Position);
                                ofs += 4;
                                ofs += CooUtility.Write(_buffer, ofs, volume.Size);
                                ofs += 4;
                            }
                            else
                                ofs += 32;
                            break;
                        case "RandomValue":
                            ofs += CooUtility.Write(_buffer, ofs, (float)random.NextDouble());
                            break;
                        default:
                            var st = _rc?.morphStateComponent;
                            if (st != null && st.stringMorphIndexMap.TryGetValue(s, out int _i))
                            {
                                ofs += CooUtility.Write(_buffer, ofs, st.Weights.Computed[_i]);
                            }
                            else if (_pass.passParameters1 != null && _pass.passParameters1.TryGetValue(s, out float _f1))
                            {
                                ofs += CooUtility.Write(_buffer, ofs, _f1);
                            }
                            else if (material != null && material.textures.ContainsKey(s))
                            {
                                ofs += CooUtility.Write(_buffer, ofs, 1.0f);
                            }
                            else
                                ofs += CooUtility.Write(_buffer, ofs, 0.0f);
                            break;
                    }
                }
                return ofs;
            }
        }
        //you can fold local function in your editor
        public override void RenderCamera(RenderPipelineContext context, VisualChannel visualChannel)
        {
            var graphicsContext = visualChannel.graphicsContext;
            var rendererComponents = context.dynamicContextRead.renderers;
            var settings = context.dynamicContextRead.settings;
            var XBufferGroup = visualChannel.XBufferGroup;
            Texture2D texLoading = context.TextureLoading;
            Texture2D texError = context.TextureError;
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
                //if (combinedPass.Type == "Swap")
                //{
                //    //swap all render target
                //    var a = context.RTs[combinedPass.RenderTargets[0]];
                //    for (int i = 0; i < combinedPass.RenderTargets.Count - 1; i++)
                //    {
                //        context.RTs[combinedPass.RenderTargets[i]] = context.RTs[combinedPass.RenderTargets[i + 1]];
                //    }
                //    context.RTs[combinedPass.RenderTargets[combinedPass.RenderTargets.Count - 1]] = a;
                //    context.RefreshPassesRenderTarget(context.dynamicContextRead.currentPassSetting, visualChannel);

                //    continue;
                //}
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
                Texture2D[] renderTargets = new Texture2D[combinedPass.RenderTargets.Count];
                for (int i = 0; i < combinedPass.RenderTargets.Count; i++)
                {
                    renderTargets[i] = _GetTex2D1(combinedPass.RenderTargets[i]);
                }

                if (combinedPass.RenderTargets.Count == 0)
                    graphicsContext.SetDSV(depthStencil, combinedPass.ClearDepth);
                else if (combinedPass.RenderTargets.Count != 0 && depthStencil != null)
                    graphicsContext.SetRTVDSV(renderTargets, depthStencil, Vector4.Zero, combinedPass.ClearRenderTarget, combinedPass.ClearDepth);
                else if (combinedPass.RenderTargets.Count != 0 && depthStencil == null)
                    graphicsContext.SetRTV(renderTargets, Vector4.Zero, combinedPass.ClearRenderTarget);

                PSODesc passPsoDesc;
                passPsoDesc.blendState = combinedPass.BlendMode;
                passPsoDesc.cullMode = combinedPass.CullMode;
                passPsoDesc.depthBias = combinedPass.DepthBias;
                passPsoDesc.slopeScaledDepthBias = combinedPass.SlopeScaledDepthBias;
                passPsoDesc.dsvFormat = depthStencil == null ? Format.Unknown : depthStencil.GetFormat();
                passPsoDesc.ptt = PrimitiveTopologyType.Triangle;
                passPsoDesc.rtvFormat = combinedPass.RenderTargets.Count == 0 ? Format.Unknown : renderTargets[0].GetFormat();
                passPsoDesc.renderTargetCount = combinedPass.RenderTargets.Count;
                passPsoDesc.streamOutput = false;
                passPsoDesc.wireFrame = false;
                _PassSetRes1(null, combinedPass);
                if (combinedPass.DrawObjects)
                {
                    passPsoDesc.inputLayout = InputLayout.mmd;
                    passPsoDesc.wireFrame = context.dynamicContextRead.settings.Wireframe;

                    //graphicsContext.SetMesh(context.SkinningMeshBuffer);

                    _PassRender(rendererComponents, combinedPass);
                }
                else if (combinedPass.Type == "DrawScreen")
                {
                    foreach (var cbv in combinedPass.Pass.CBVs)
                    {
                        XBufferGroup.SetCBVRSlot(graphicsContext, matC, cbv.Index);
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
                        graphicsContext.SetMeshVertex(rendererComponent.meshAppend);
                        //graphicsContext.m_commandList.IASetPrimitiveTopology(Vortice.Direct3D.PrimitiveTopology.TriangleList);
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
                                XBufferGroup.SetCBVRSlot(graphicsContext, matC, cbv.Index);
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
            //if (filter == "SelectedObject")
            //    return context.dynamicContextRead.selectedEntity.rendererComponent == renderer;
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