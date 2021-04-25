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

        Random randomGenerator = new Random();

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
            var inShaderSettings = context.dynamicContextRead.inShaderSettings;
            var rendererComponents = context.dynamicContextRead.rendererComponents;
            var lightings = context.dynamicContextRead.lightings;
            var camera = context.dynamicContextRead.cameras[0];
            var bigBuffer = context.bigBuffer;

            #region Lighting
            Matrix4x4 lightCameraMatrix0 = Matrix4x4.Identity;
            if (lightings.Count > 0 && lightings[0].LightingType == LightingType.Directional)
            {
                lightCameraMatrix0 = Matrix4x4.Transpose(lightings[0].GetLightingMatrix(settings.ExtendShadowMapRange, camera.LookAtPoint, camera.Angle, camera.Distance));
                HasMainLight = true;
            }
            else
                HasMainLight = false;

            #endregion

            int numMaterials = 0;
            for (int i = 0; i < rendererComponents.Count; i++)
                numMaterials += rendererComponents[i].Materials.Count;

            int matC = 0;
            int num2 = 0;
            foreach (var combinedPass in context.currentPassSetting.RenderSequence)
            {
                if (combinedPass.Pass.CBVs != null)
                    num2 += (combinedPass.DrawObjects ? numMaterials : 1) * combinedPass.Pass.CBVs.Count;
            }
            context.XBufferGroup.SetSlienceCount(num2);
            foreach (var combinedPass in context.currentPassSetting.RenderSequence)
            {
                if (combinedPass.Pass.Camera == "Main")
                {
                }
                else if (combinedPass.Pass.Camera == "ShadowMap")
                {
                    if (!(HasMainLight && inShaderSettings.EnableShadow)) continue;
                }
                if (combinedPass.Pass.CBVs.Count == 0) continue;
                if (combinedPass.DrawObjects)
                {
                    foreach (var rendererComponent in rendererComponents)
                    {
                        foreach (var material in rendererComponent.Materials)
                        {
                            foreach (var cbv in combinedPass.Pass.CBVs)
                            {
                                int ofs = _WriteCBV(cbv, combinedPass.Pass.Camera, bigBuffer, material);
                                context.XBufferGroup.UpdateSlience(graphicsContext, bigBuffer, 0, ofs, matC);
                                matC++;
                            }
                        }
                    }
                }
                else
                {
                    foreach (var cbv in combinedPass.Pass.CBVs)
                    {
                        int ofs = _WriteCBV(cbv, combinedPass.Pass.Camera, bigBuffer, null);
                        context.XBufferGroup.UpdateSlience(graphicsContext, bigBuffer, 0, ofs, matC);
                        matC++;
                    }
                }
            }
            if (matC > 0)
                context.XBufferGroup.UpdateSlienceComplete(graphicsContext);
            int _WriteCBV(CBVSlotRes cbv, string cam, byte[] _buffer, RuntimeMaterial material)
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
                        case "CameraPosition":
                            ofs += CooUtility.Write(_buffer, ofs, camera.Pos);
                            break;
                        case "Camera":
                            if (cam == "Main")
                            {
                                ofs += CooUtility.Write(_buffer, ofs, Matrix4x4.Transpose(camera.vpMatrix));
                                ofs += CooUtility.Write(_buffer, ofs, Matrix4x4.Transpose(camera.pvMatrix));
                            }
                            else if (cam == "ShadowMap")
                            {
                                if (lightings.Count > 0)
                                {
                                    ofs += CooUtility.Write(_buffer, ofs, lightCameraMatrix0);
                                    ofs += CooUtility.Write(_buffer, ofs, lightCameraMatrix0);
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
                        case "IndirectMultiplier":
                            ofs += CooUtility.Write(_buffer, ofs, inShaderSettings.SkyBoxLightMultiple);
                            break;
                        case "RandomValue":
                            ofs += CooUtility.Write(_buffer, ofs, (float)randomGenerator.NextDouble());
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
            var inShaderSettings = context.dynamicContextRead.inShaderSettings;
            Texture2D texLoading = context.TextureLoading;
            Texture2D texError = context.TextureError;
            Texture2D _Tex(Texture2D _tex) => TextureStatusSelect(_tex, texLoading, texError, texError);
            var rpAssets = context.RPAssetsManager;
            var RSBase = rpAssets.rootSignature;
            var deviceResources = context.deviceResources;

            PSO PSOSkinning = rpAssets.PSOMMDSkinning;
            PSO psoLoading = rpAssets.PSOs["Loading"];
            PSO psoError = rpAssets.PSOs["Error"];

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
                int indexCountAll = rendererComponent.meshVertexCount;
                graphicsContext.Draw(indexCountAll, 0);
            }
            for (int i = 0; i < rendererComponents.Count; i++)
                EntitySkinning(rendererComponents[i], context.CBs_Bone[i]);
            graphicsContext.SetSOMeshNone();

            //graphicsContext.SetRootSignature(RSBase);
            //graphicsContext.SetRTVDSV(context.outputRTV, context.ScreenSizeDSVs[0], Vector4.Zero, false, true);
            int matC = 0;
            foreach (var combinedPass in context.currentPassSetting.RenderSequence)
            {
                if (combinedPass.Pass.Camera == "Main")
                {
                }
                else if (combinedPass.Pass.Camera == "ShadowMap")
                {
                    if (!(HasMainLight && inShaderSettings.EnableShadow)) continue;
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
                passPsoDesc.cullMode = ECullMode.none;
                passPsoDesc.depthBias = combinedPass.DepthBias;
                passPsoDesc.slopeScaledDepthBias = combinedPass.SlopeScaledDepthBias;
                passPsoDesc.dsvFormat = combinedPass.depthSencil == null ? DxgiFormat.DXGI_FORMAT_UNKNOWN : combinedPass.depthSencil.GetFormat();
                passPsoDesc.inputLayout = EInputLayout.skinned;
                passPsoDesc.ptt = ED3D12PrimitiveTopologyType.TRIANGLE;
                passPsoDesc.rtvFormat = combinedPass.renderTargets.Length == 0 ? DxgiFormat.DXGI_FORMAT_UNKNOWN : combinedPass.renderTargets[0].GetFormat();
                passPsoDesc.renderTargetCount = combinedPass.renderTargets.Length;
                passPsoDesc.streamOutput = false;
                passPsoDesc.wireFrame = context.dynamicContextRead.settings.Wireframe;
                _PassSetRes();
                if (combinedPass.DrawObjects)
                {
                    graphicsContext.SetMesh(context.SkinningMeshBuffer);
                    _PassRender(rendererComponents, combinedPass);
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
                    Texture2D albedo = null;
                    foreach (var rendererComponent in _rendererComponents)
                    {
                        graphicsContext.SetMeshIndex(rendererComponent.mesh);
                        PSO pso = null;
                        if (rendererComponent.shaders != null)
                            rendererComponent.shaders.TryGetValue(_combinedPass.Name, out pso);

                        var PSODraw = PSOSelect(deviceResources, RSBase, ref passPsoDesc, pso, psoLoading, _combinedPass.PSODefault, psoError);
                        var Materials = rendererComponent.Materials;
                        List<Texture2D> texs = rendererComponent.textures;
                        int indexOffset = 0;
                        foreach (var material in Materials)
                        {
                            foreach (var cbv in _combinedPass.Pass.CBVs)
                            {
                                context.XBufferGroup.SetCBVR(graphicsContext, matC, cbv.Index);
                                matC++;
                            }
                            if (material.innerStruct.DiffuseColor.W > 0)
                            {
                                if (material.texIndex != -1 && material.texIndex < Materials.Count)
                                    albedo = _Tex(texs[material.texIndex]);

                                _PassSetRes1();
                                passPsoDesc.cullMode = material.DrawFlags.HasFlag(DrawFlag.DrawDoubleFace) ? ECullMode.none : ECullMode.back;
                                SetPipelineStateVariant(deviceResources, graphicsContext, RSBase, ref passPsoDesc, PSODraw);
                                graphicsContext.DrawIndexed(material.indexCount, indexOffset, counterX.vertex);
                            }
                            counterX.material++;
                            indexOffset += material.indexCount;
                        }
                        counterX.vertex += rendererComponent.meshVertexCount;
                    }
                    void _PassSetRes1()
                    {
                        if (_combinedPass.Pass.SRVs != null)
                        {
                            foreach (var resd in _combinedPass.Pass.SRVs)
                            {
                                //if (resd.ResourceType == "TextureCube")
                                //{
                                //    graphicsContext.SetSRVT(context._GetTexCubeByName(resd.Resource), resd.Index + 3);
                                //}
                                if (resd.ResourceType == "Texture2D")
                                {
                                    //var tex2D = context._GetTex2DByName(resd.Resource);
                                    ITexture2D tex2D = null;
                                    if (resd.Resource == "_Albedo")
                                        tex2D = albedo;
                                    if (tex2D != null)
                                        graphicsContext.SetSRVT(tex2D, resd.Index + 3);
                                }
                            }
                        }
                    }
                }
                void _PassSetRes()
                {
                    if (combinedPass.Pass.SRVs != null)
                    {
                        foreach (var resd in combinedPass.Pass.SRVs)
                        {
                            if (resd.ResourceType == "TextureCube")
                            {
                                graphicsContext.SetSRVT(context._GetTexCubeByName(resd.Resource), resd.Index + 3);
                            }
                            if (resd.ResourceType == "Texture2D")
                            {
                                var tex2D = context._GetTex2DByName(resd.Resource);
                                if (tex2D != null)
                                    graphicsContext.SetSRVT(tex2D, resd.Index + 3);
                            }
                        }
                    }
                }
            }
        }
    }
}