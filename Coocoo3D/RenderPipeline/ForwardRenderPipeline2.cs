using Coocoo3D.Components;
using Coocoo3D.Core;
using Coocoo3D.Present;
using Coocoo3DGraphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using PSO = Coocoo3DGraphics.PObject;

namespace Coocoo3D.RenderPipeline
{
    public class ForwardRenderPipeline2 : RenderPipeline
    {
        public void Reload(DeviceResources deviceResources)
        {
            Ready = true;
        }

        Random random = new Random();

        struct _Counters
        {
            public int material;
            public int vertex;
        }

        bool HasMainLight;
        public override void PrepareRenderData(RenderPipelineContext context, GraphicsContext graphicsContext)
        {
            var deviceResources = context.deviceResources;
            //var cameras = context.dynamicContextRead.cameras;
            var settings = context.dynamicContextRead.settings;
            var rendererComponents = context.dynamicContextRead.rendererComponents;
            var lightings = context.dynamicContextRead.lightings;
            var camera = context.dynamicContextRead.cameras[0];
            var bigBuffer = context.bigBuffer;
            List<LightingData> pointLights = new List<LightingData>();

            #region Lighting
            Matrix4x4 lightCameraMatrix0 = Matrix4x4.Identity;
            Matrix4x4 invLightCameraMatrix0 = Matrix4x4.Identity;
            if (lightings.Count > 0 && lightings[0].LightingType == LightingType.Directional)
            {
                lightCameraMatrix0 = lightings[0].GetLightingMatrix(settings.ExtendShadowMapRange, camera.LookAtPoint, camera.Angle, camera.Distance);
                Matrix4x4.Invert(lightCameraMatrix0, out invLightCameraMatrix0);
                lightCameraMatrix0 = Matrix4x4.Transpose(lightCameraMatrix0);
                invLightCameraMatrix0 = Matrix4x4.Transpose(invLightCameraMatrix0);
                HasMainLight = true;
            }
            else
                HasMainLight = false;
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

            int numofBuffer = 0;
            foreach (var combinedPass in context.dynamicContextRead.currentPassSetting.RenderSequence)
                if (combinedPass.Pass.CBVs != null)
                    numofBuffer += (combinedPass.DrawObjects ? numMaterials : 1) * combinedPass.Pass.CBVs.Count;
            context.XBufferGroup.SetSlienceCount(numofBuffer);

            int matC = 0;
            foreach (var combinedPass in context.dynamicContextRead.currentPassSetting.RenderSequence)
            {
                if (combinedPass.Pass.Camera == "Main")
                {
                }
                else if (combinedPass.Pass.Camera == "ShadowMap")
                {
                    if (!(HasMainLight && settings.EnableShadow)) continue;
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
                                context.XBufferGroup.UpdateSlience(graphicsContext, bigBuffer, 0, ofs, matC);
                                matC++;
                            }
                        }
                }
                else
                {
                    foreach (var cbv in combinedPass.Pass.CBVs)
                    {
                        int ofs = _WriteCBV(cbv, combinedPass, bigBuffer, null, null);
                        context.XBufferGroup.UpdateSlience(graphicsContext, bigBuffer, 0, ofs, matC);
                        matC++;
                    }
                }
            }
            if (matC > 0)
                context.XBufferGroup.UpdateSlienceComplete(graphicsContext);
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
                            if (_pass.renderTargets != null && _pass.renderTargets.Length > 0)
                            {
                                ofs += CooUtility.Write(_buffer, ofs, _pass.renderTargets[0].GetWidth());
                                ofs += CooUtility.Write(_buffer, ofs, _pass.renderTargets[0].GetHeight());
                            }
                            else if (_pass.depthSencil != null)
                            {
                                ofs += CooUtility.Write(_buffer, ofs, _pass.depthSencil.GetWidth());
                                ofs += CooUtility.Write(_buffer, ofs, _pass.depthSencil.GetHeight());
                            }
                            else
                                ofs += sizeof(int) * 2;
                            break;
                        case "Camera":
                            if (_pass.Pass.Camera == "Main")
                            {
                                ofs += CooUtility.Write(_buffer, ofs, Matrix4x4.Transpose(camera.vpMatrix));
                                ofs += CooUtility.Write(_buffer, ofs, Matrix4x4.Transpose(camera.pvMatrix));
                            }
                            else if (_pass.Pass.Camera == "ShadowMap")
                            {
                                if (lightings.Count > 0)
                                {
                                    ofs += CooUtility.Write(_buffer, ofs, lightCameraMatrix0);
                                    ofs += CooUtility.Write(_buffer, ofs, invLightCameraMatrix0);
                                }
                                else
                                    ofs += 128;
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
                        case "RandomValue":
                            ofs += CooUtility.Write(_buffer, ofs, (float)random.NextDouble());
                            break;
                        default:
                            var st = _rc?.stateComponent;
                            if (st != null && st.stringMorphIndexMap.TryGetValue(s, out int _i))
                            {
                                ofs += CooUtility.Write(_buffer, ofs, st.WeightComputed[_i]);
                            }
                            else if (_pass.passParameters1.TryGetValue(s, out float _f1))
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
        public override void RenderCamera(RenderPipelineContext context, GraphicsContext graphicsContext)
        {
            var rendererComponents = context.dynamicContextRead.rendererComponents;
            var settings = context.dynamicContextRead.settings;
            Texture2D texLoading = context.TextureLoading;
            Texture2D texError = context.TextureError;
            ITexture2D _Tex(ITexture2D _tex)
            {
                if (_tex is Texture2D _tex1)
                    return TextureStatusSelect(_tex1, texLoading, texError, texError);
                else if (_tex == null)
                    return texError;
                else
                    return _tex;
            };
            var rpAssets = context.RPAssetsManager;
            var RSBase = rpAssets.rootSignature;
            var deviceResources = context.deviceResources;

            PSO PSOSkinning = rpAssets.PSOs["PSOMMDSkinning"];
            PSO psoLoading = rpAssets.PSOs["Loading"];
            PSO psoError = rpAssets.PSOs["Error"];
            #region Skinning
            graphicsContext.SetRootSignature(rpAssets.rootSignatureSkinning);
            graphicsContext.SetSOMesh(context.SkinningMeshBuffer);
            void EntitySkinning(MMDRendererComponent rendererComponent, CBuffer entityBoneDataBuffer)
            {
                var Materials = rendererComponent.Materials;
                graphicsContext.SetCBVR(entityBoneDataBuffer, 0);
                rendererComponent.shaders.TryGetValue("Skinning", out var shaderSkinning);
                var psoSkinning = PSOSelect(deviceResources, rpAssets.rootSignatureSkinning, ref context.SkinningDesc, shaderSkinning, PSOSkinning, PSOSkinning, PSOSkinning);
                SetPipelineStateVariant(deviceResources, graphicsContext, rpAssets.rootSignatureSkinning, ref context.SkinningDesc, psoSkinning);
                graphicsContext.SetMeshVertex1(rendererComponent.mesh);
                graphicsContext.SetMeshVertex(rendererComponent.meshAppend);
                graphicsContext.Draw(rendererComponent.meshVertexCount, 0);
            }
            for (int i = 0; i < rendererComponents.Count; i++)
                EntitySkinning(rendererComponents[i], context.CBs_Bone[i]);
            graphicsContext.SetSOMeshNone();
            #endregion

            int matC = 0;
            foreach (var combinedPass in context.dynamicContextRead.currentPassSetting.RenderSequence)
            {
                if (combinedPass.Pass.Camera == "Main")
                {
                }
                else if (combinedPass.Pass.Camera == "ShadowMap")
                {
                    if (!(HasMainLight && settings.EnableShadow)) continue;
                }
                graphicsContext.SetRootSignature(RSBase);

                if (combinedPass.renderTargets.Length == 0)
                    graphicsContext.SetDSV(combinedPass.depthSencil, true);
                else if (combinedPass.renderTargets.Length != 0 && combinedPass.depthSencil != null)
                    graphicsContext.SetRTVDSV(combinedPass.renderTargets, combinedPass.depthSencil, Vector4.Zero, false, combinedPass.ClearDepth);
                else if (combinedPass.renderTargets.Length != 0 && combinedPass.depthSencil == null)
                    graphicsContext.SetRTV(combinedPass.renderTargets, Vector4.Zero, false);

                PSODesc passPsoDesc;
                passPsoDesc.blendState = combinedPass.BlendMode;
                passPsoDesc.cullMode = combinedPass.CullMode;
                passPsoDesc.depthBias = combinedPass.DepthBias;
                passPsoDesc.slopeScaledDepthBias = combinedPass.SlopeScaledDepthBias;
                passPsoDesc.dsvFormat = combinedPass.depthSencil == null ? DxgiFormat.DXGI_FORMAT_UNKNOWN : combinedPass.depthSencil.GetFormat();
                passPsoDesc.ptt = ED3D12PrimitiveTopologyType.TRIANGLE;
                passPsoDesc.rtvFormat = combinedPass.renderTargets.Length == 0 ? DxgiFormat.DXGI_FORMAT_UNKNOWN : combinedPass.renderTargets[0].GetFormat();
                passPsoDesc.renderTargetCount = combinedPass.renderTargets.Length;
                passPsoDesc.streamOutput = false;
                passPsoDesc.wireFrame = false;
                _PassSetRes1(null, combinedPass);
                if (combinedPass.DrawObjects)
                {
                    passPsoDesc.inputLayout = EInputLayout.skinned;
                    passPsoDesc.wireFrame = context.dynamicContextRead.settings.Wireframe;
                    graphicsContext.SetMesh(context.SkinningMeshBuffer);
                    _PassRender(rendererComponents, combinedPass);
                }
                else if (combinedPass.Type == "RayTracing")
                {
                    _RayTracing(rendererComponents, combinedPass);
                }
                else
                {
                    foreach (var cbv in combinedPass.Pass.CBVs)
                    {
                        context.XBufferGroup.SetCBVR(graphicsContext, matC, cbv.Index);
                        matC++;
                    }
                    passPsoDesc.inputLayout = EInputLayout.postProcess;
                    SetPipelineStateVariant(deviceResources, graphicsContext, RSBase, ref passPsoDesc, combinedPass.PSODefault);
                    graphicsContext.SetMesh(context.ndcQuadMesh);
                    graphicsContext.DrawIndexed(context.ndcQuadMesh.GetIndexCount(), 0, 0);
                }
                void _PassRender(List<MMDRendererComponent> _rendererComponents, PassMatch1 _combinedPass)
                {
                    _Counters counterX = new _Counters();
                    foreach (var rendererComponent in _rendererComponents)
                    {
                        graphicsContext.SetMeshIndex(rendererComponent.mesh);
                        PSO pso = null;
                        if (rendererComponent.shaders != null)
                            rendererComponent.shaders.TryGetValue(_combinedPass.Name, out pso);

                        var PSODraw = PSOSelect(deviceResources, RSBase, ref passPsoDesc, pso, psoLoading, _combinedPass.PSODefault, psoError);
                        var Materials = rendererComponent.Materials;
                        int indexOffset = 0;
                        foreach (var material in Materials)
                        {
                            if (!FilterObj(context, combinedPass.Filter, rendererComponent, material))
                            {
                                counterX.material++;
                                indexOffset += material.indexCount;
                                continue;
                            }
                            foreach (var cbv in _combinedPass.Pass.CBVs)
                            {
                                context.XBufferGroup.SetCBVR(graphicsContext, matC, cbv.Index);
                                matC++;
                            }
                            _PassSetRes1(material, _combinedPass);
                            if (passPsoDesc.cullMode == ECullMode.notSpecial)
                                passPsoDesc.cullMode = material.DrawFlags.HasFlag(DrawFlag.DrawDoubleFace) ? ECullMode.none : ECullMode.back;
                            SetPipelineStateVariant(deviceResources, graphicsContext, RSBase, ref passPsoDesc, PSODraw);
                            graphicsContext.DrawIndexed(material.indexCount, indexOffset, counterX.vertex);
                            counterX.material++;
                            indexOffset += material.indexCount;
                        }
                        counterX.vertex += rendererComponent.meshVertexCount;
                    }
                }
            }
            void _RayTracing(List<MMDRendererComponent> _rendererComponents, PassMatch1 _combinedPass)
            {
                if (_rendererComponents.Count == 0) return;
                var rtso = context.dynamicContextRead.currentPassSetting.RTSO;
                var rtst = context.RTSTs[_combinedPass.Name];
                var rtis = context.RTIGroups[_combinedPass.Name];
                var rttas = context.RTTASs[_combinedPass.Name];
                var rtTex1 = _combinedPass.renderTargets[0];
                var rtasg = context.RTASGroup;
                graphicsContext.Prepare(rtasg);
                graphicsContext.Prepare(rtis);
                _Counters counterX = new _Counters();

                foreach (var rendererComponent in _rendererComponents)
                {
                    int indexOffset = 0;
                    foreach (var material in rendererComponent.Materials)
                    {
                        graphicsContext.BuildBTAS(rtasg, context.SkinningMeshBuffer, rendererComponent.mesh, counterX.vertex, indexOffset, material.indexCount);
                        graphicsContext.BuildInst(rtis, rtasg, counterX.material, counterX.material, uint.MaxValue);

                        counterX.material++;
                        indexOffset += material.indexCount;
                    }
                    counterX.vertex += rendererComponent.meshVertexCount;
                }
                graphicsContext.TestShaderTable(rtst, rtso, _combinedPass.RayGenShaders, _combinedPass.MissShaders);
                graphicsContext.TestShaderTable2(rtst, rtso, rtasg, new string[] { "Test" });
                graphicsContext.BuildTPAS(rtis, rttas, rtasg);
                graphicsContext.SetRayTracingStateObject(rtso);
                graphicsContext.SetTPAS(rttas, rtso, 0);


                foreach (var cbv in _combinedPass.Pass.CBVs)
                {
                    context.XBufferGroup.SetComputeCBVR(graphicsContext, matC, cbv.Index);
                    matC++;
                }
                graphicsContext.SetComputeUAVTSlot(context.outputRTV, 0);
                graphicsContext.DispatchRay(rtst, rtTex1.GetWidth(), rtTex1.GetHeight(), 1);
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
                            ITexture2D tex2D = null;
                            if (material != null && material.textures.TryGetValue(resd.Resource, out ITexture2D tex))
                                tex2D = tex;
                            if (tex2D == null)
                                tex2D = context._GetTex2DByName(resd.Resource);

                            if (tex2D != null)
                            {
                                graphicsContext.SetSRVTSlot(_Tex(tex2D), resd.Index);
                            }
                        }
                    }
            }
            //void _PassSetResRayTracing(PassMatch1 _combinedPass)
            //{
            //    if (_combinedPass.Pass.SRVs != null)
            //        foreach (var resd in _combinedPass.Pass.SRVs)
            //        {
            //            if (resd.ResourceType == "TextureCube")
            //            {
            //                graphicsContext.SetSRVTSlot(context._GetTexCubeByName(resd.Resource), resd.Index);
            //            }
            //            if (resd.ResourceType == "Texture2D")
            //            {
            //                var tex2D = context._GetTex2DByName(resd.Resource);
            //                if (tex2D != null)
            //                    graphicsContext.SetSRVTSlot(tex2D, resd.Index);
            //            }
            //        }
            //}
        }
        bool FilterObj(RenderPipelineContext context, string filter, MMDRendererComponent renderer, RuntimeMaterial material)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            if (filter == "SelectedObject")
                return context.dynamicContextRead.selectedEntity.rendererComponent == renderer;
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