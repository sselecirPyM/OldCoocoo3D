﻿using System;
using System.Collections.Generic;
using System.Text;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Coocoo3DGraphics
{
    struct _PSODesc1 : IEquatable<_PSODesc1>
    {
        public PSODesc desc;
        public ID3D12RootSignature rootSignature;

        public override bool Equals(object obj)
        {
            return obj is _PSODesc1 desc && Equals(desc);
        }

        public bool Equals(_PSODesc1 other)
        {
            return desc.Equals(other.desc) &&
                   EqualityComparer<ID3D12RootSignature>.Default.Equals(rootSignature, other.rootSignature);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(desc, rootSignature);
        }

        public static bool operator ==(_PSODesc1 left, _PSODesc1 right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(_PSODesc1 left, _PSODesc1 right)
        {
            return !(left == right);
        }
    }
    public enum BlendState
    {
        none = 0,
        alpha = 1,
        add = 2,
        None = 0,
        Alpha = 1,
        Add = 2,
    };
    public enum InputLayout
    {
        mmd = 0,
        postProcess = 1,
        skinned = 2,
        imgui = 3,
    };

    public struct PSODesc : IEquatable<PSODesc>
    {
        public InputLayout inputLayout;
        public BlendState blendState;
        public CullMode cullMode;
        public PrimitiveTopologyType ptt;
        public Format rtvFormat;
        public Format dsvFormat;
        public int renderTargetCount;
        public int depthBias;
        public float slopeScaledDepthBias;
        public bool wireFrame;
        public bool streamOutput;

        public override bool Equals(object obj)
        {
            return obj is PSODesc desc && Equals(desc);
        }

        public bool Equals(PSODesc other)
        {
            return inputLayout == other.inputLayout &&
                   blendState == other.blendState &&
                   cullMode == other.cullMode &&
                   ptt == other.ptt &&
                   rtvFormat == other.rtvFormat &&
                   dsvFormat == other.dsvFormat &&
                   renderTargetCount == other.renderTargetCount &&
                   depthBias == other.depthBias &&
                   slopeScaledDepthBias == other.slopeScaledDepthBias &&
                   wireFrame == other.wireFrame &&
                   streamOutput == other.streamOutput;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(inputLayout);
            hash.Add(blendState);
            hash.Add(cullMode);
            hash.Add(ptt);
            hash.Add(rtvFormat);
            hash.Add(dsvFormat);
            hash.Add(renderTargetCount);
            hash.Add(depthBias);
            hash.Add(slopeScaledDepthBias);
            hash.Add(wireFrame);
            hash.Add(streamOutput);
            return hash.ToHashCode();
        }

        public static bool operator ==(PSODesc left, PSODesc right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PSODesc left, PSODesc right)
        {
            return !(left == right);
        }
    }
    public class PSO
    {
        static readonly InputLayoutDescription inputLayoutMMD = new InputLayoutDescription(
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 1),
            new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 0, 0),
            new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 12, 0),
            new InputElementDescription("EDGESCALE", 0, Format.R32_Float, 20, 0),
            new InputElementDescription("BONES", 0, Format.R16G16B16A16_UInt, 24, 0),
            new InputElementDescription("WEIGHTS", 0, Format.R32G32B32A32_Float, 32, 0),
            new InputElementDescription("TANGENT", 0, Format.R32G32B32_Float, 48, 0)
            );
        static readonly InputLayoutDescription inputLayoutSkinned = new InputLayoutDescription(
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0),
            new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 0),
            new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 0),
            new InputElementDescription("TANGENT", 0, Format.R32G32B32_Float, 0),
            new InputElementDescription("EDGESCALE", 0, Format.R32_Float, 0)
            );
        static readonly InputLayoutDescription inputLayoutPosOnly = new InputLayoutDescription(
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0)
            );
        static readonly InputLayoutDescription _inputLayoutImGui = new InputLayoutDescription(
            new InputElementDescription("POSITION", 0, Format.R32G32_Float, 0),
            new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 0),
            new InputElementDescription("COLOR", 0, Format.R8G8B8A8_UNorm, 0)
            );

        BlendDescription BlendDescSelect(BlendState blendState)
        {
            if (blendState == BlendState.none)
                return new BlendDescription(Blend.One, Blend.Zero);
            else if (blendState == BlendState.alpha)
                return blendStateAlpha();
            else if (blendState == BlendState.add)
                return blendStateAdd();
            return new BlendDescription();
        }

        //public List<PSOCombind> pipelineStates = new List<PSOCombind>();
        public byte[] vertexShader;
        public byte[] pixelShader;
        public byte[] geometryShader;
        public string Name;
        public GraphicsObjectStatus Status;
        internal List<_PSODesc1> m_psoDescs = new List<_PSODesc1>();
        internal List<ID3D12PipelineState> m_pipelineStates = new List<ID3D12PipelineState>();

        public PSO()
        {

        }

        public PSO(byte[] vertexShader, byte[] geometryShader, byte[] pixelShader)
        {
            this.vertexShader = vertexShader;
            this.pixelShader = pixelShader;
            this.geometryShader = geometryShader;
        }

        public PSO(VertexShader vertexShader, GeometryShader geometryShader, PixelShader pixelShader)
        {
            Initialize(vertexShader, geometryShader, pixelShader);
        }

        public void Initialize(VertexShader vertexShader, GeometryShader geometryShader, PixelShader pixelShader)
        {
            this.vertexShader = vertexShader?.compiledCode;
            this.geometryShader = geometryShader?.compiledCode;
            this.pixelShader = pixelShader?.compiledCode;
        }

        //public ID3D12PipelineState GetState(GraphicsDevice device, PSODesc desc, RootSignature rootSignature, UnnamedInputLayout inputLayout)
        //{
        //    foreach (var psoCombind in pipelineStates)
        //    {
        //        if (psoCombind.PSODesc == desc && psoCombind.rootSignature == rootSignature && psoCombind.unnamedInputLayout == inputLayout)
        //        {
        //            if (psoCombind.pipelineState == null)
        //                throw new Exception("pipeline state error");
        //            return psoCombind.pipelineState;
        //        }
        //    }
        //    InputLayoutDescription inputLayoutDescription = inputLayout.inputElementDescriptions;

        //    GraphicsPipelineStateDescription graphicsPipelineStateDescription = new GraphicsPipelineStateDescription();
        //    graphicsPipelineStateDescription.RootSignature = rootSignature.rootSignature;
        //    graphicsPipelineStateDescription.VertexShader = vertexShader;
        //    graphicsPipelineStateDescription.GeometryShader = geometryShader;
        //    graphicsPipelineStateDescription.PixelShader = pixelShader;
        //    graphicsPipelineStateDescription.PrimitiveTopologyType = PrimitiveTopologyType.Triangle;
        //    graphicsPipelineStateDescription.InputLayout = inputLayoutDescription;
        //    graphicsPipelineStateDescription.DepthStencilFormat = desc.dsvFormat;
        //    graphicsPipelineStateDescription.RenderTargetFormats = new Format[desc.renderTargetCount];
        //    for (int i = 0; i < graphicsPipelineStateDescription.RenderTargetFormats.Length; i++)
        //    {
        //        graphicsPipelineStateDescription.RenderTargetFormats[i] = desc.rtvFormat;
        //    }

        //    if (desc.blendState == "Alpha")
        //        graphicsPipelineStateDescription.BlendState = blendStateAlpha();
        //    else if (desc.blendState == "Add")
        //        graphicsPipelineStateDescription.BlendState = BlendDescription.Additive;
        //    else
        //        graphicsPipelineStateDescription.BlendState = BlendDescription.Opaque;


        //    graphicsPipelineStateDescription.DepthStencilState = new DepthStencilDescription(desc.dsvFormat != Format.Unknown, desc.dsvFormat != Format.Unknown);
        //    graphicsPipelineStateDescription.SampleMask = uint.MaxValue;
        //    var RasterizerState = new RasterizerDescription(CullMode.None, FillMode.Solid);
        //    RasterizerState.DepthBias = desc.depthBias;
        //    RasterizerState.SlopeScaledDepthBias = desc.slopeScaledDepthBias;
        //    graphicsPipelineStateDescription.RasterizerState = RasterizerState;

        //    var pipelineState = device.device.CreateGraphicsPipelineState<ID3D12PipelineState>(graphicsPipelineStateDescription);
        //    if (pipelineState == null)
        //        throw new Exception("pipeline state error");
        //    pipelineStates.Add(new PSOCombind { PSODesc = desc, pipelineState = pipelineState, rootSignature = rootSignature, unnamedInputLayout = inputLayout });
        //    return pipelineState;
        //}

        BlendDescription blendStateAlpha()
        {
            BlendDescription blendDescription = new BlendDescription(Blend.SourceAlpha, Blend.InverseSourceAlpha, Blend.One, Blend.InverseSourceAlpha);
            return blendDescription;
        }

        BlendDescription blendStateAdd()
        {
            BlendDescription blendDescription = new BlendDescription(Blend.One, Blend.One);
            return blendDescription;
        }

        public void Dispose()
        {
            //foreach (var combine in pipelineStates)
            //{
            //    combine.pipelineState.Dispose();
            //}
            //pipelineStates.Clear();
        }


        public int GetVariantIndex(GraphicsDevice graphicsDevice, RootSignature graphicsSignature, PSODesc psoDesc)
        {
            _PSODesc1 _psoDesc1;
            _psoDesc1.desc = psoDesc;
            _psoDesc1.rootSignature = graphicsSignature.rootSignature;
            int index = -1;
            for (int i = 0; i < m_psoDescs.Count; i++)
            {
                if (m_psoDescs[i] == _psoDesc1)
                {
                    index = i;
                }
            }
            if (index == -1)
            {
                StreamOutputElement[] declarations = new StreamOutputElement[5];
                declarations[0].SemanticName = "POSITION";
                declarations[0].ComponentCount = 3;
                declarations[1].SemanticName = "NORMAL";
                declarations[1].ComponentCount = 3;
                declarations[2].SemanticName = "TEXCOORD";
                declarations[2].ComponentCount = 2;
                declarations[3].SemanticName = "TANGENT";
                declarations[3].ComponentCount = 3;
                declarations[4].SemanticName = "EDGESCALE";
                declarations[4].ComponentCount = 1;


                GraphicsPipelineStateDescription state = new GraphicsPipelineStateDescription();
                if (psoDesc.inputLayout == InputLayout.mmd)
                    state.InputLayout = inputLayoutMMD;

                else if (psoDesc.inputLayout == InputLayout.postProcess)
                    state.InputLayout = inputLayoutPosOnly;

                else if (psoDesc.inputLayout == InputLayout.skinned)
                    state.InputLayout = inputLayoutSkinned;

                else if (psoDesc.inputLayout == InputLayout.imgui)
                    state.InputLayout = _inputLayoutImGui;
                state.RootSignature = graphicsSignature.rootSignature;
                if (vertexShader != null)
                    state.VertexShader = vertexShader;
                if (geometryShader != null)
                    state.GeometryShader = geometryShader;
                if (pixelShader != null)
                    state.PixelShader = pixelShader;
                if (psoDesc.dsvFormat != Format.Unknown)
                {
                    state.DepthStencilState = new DepthStencilDescription(true, true);
                    state.DepthStencilFormat = psoDesc.dsvFormat;
                }
                state.SampleMask = uint.MaxValue;
                state.PrimitiveTopologyType = psoDesc.ptt;
                if (psoDesc.streamOutput)
                {
                    int[] bufferStrides = { 64 };
                    state.StreamOutput = new StreamOutputDescription(declarations);
                    state.StreamOutput.Strides = bufferStrides;
                }
                else
                {
                    state.BlendState = BlendDescSelect(psoDesc.blendState);
                    state.SampleDescription = new SampleDescription(1, 0);
                }

                //state.NumRenderTargets = psoDesc.renderTargetCount;

                state.RenderTargetFormats = new Format[psoDesc.renderTargetCount];
                for (int i = 0; i < psoDesc.renderTargetCount; i++)
                {
                    state.RenderTargetFormats[i] = psoDesc.rtvFormat;
                }
                CullMode cullMode = psoDesc.cullMode;
                if (cullMode == 0) cullMode = CullMode.None;
                RasterizerDescription rasterizerDescription = new RasterizerDescription(cullMode, psoDesc.wireFrame ? FillMode.Wireframe : FillMode.Solid);
                rasterizerDescription.DepthBias = psoDesc.depthBias;
                rasterizerDescription.SlopeScaledDepthBias = psoDesc.slopeScaledDepthBias;
                rasterizerDescription.DepthClipEnable = psoDesc.streamOutput ? false : true;

                state.RasterizerState = rasterizerDescription;
                ID3D12PipelineState pipelineState;
                if (graphicsDevice.device.CreateGraphicsPipelineState(state, out pipelineState).Failure)
                {
                    Status = GraphicsObjectStatus.error;
                    return -1;
                }
                m_psoDescs.Add(_psoDesc1);
                m_pipelineStates.Add(pipelineState);
                return (int)m_psoDescs.Count - 1;
            }
            return index;
        }

        public void DelayDestroy(GraphicsDevice graphicsDevice)
        {
            for (int i = 0; i < m_pipelineStates.Count; i++)
            {
                graphicsDevice.ResourceDelayRecycle(m_pipelineStates[i]);
            }
        }
    }
}
