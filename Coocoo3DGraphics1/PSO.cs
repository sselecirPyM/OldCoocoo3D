using System;
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
        public PrimitiveTopologyType primitiveTopologyType;
        public Format rtvFormat;
        public Format dsvFormat;
        public int renderTargetCount;
        public int depthBias;
        public float slopeScaledDepthBias;
        public bool wireFrame;

        public override bool Equals(object obj)
        {
            return obj is PSODesc desc && Equals(desc);
        }

        public bool Equals(PSODesc other)
        {
            return inputLayout == other.inputLayout &&
                   blendState == other.blendState &&
                   cullMode == other.cullMode &&
                   primitiveTopologyType == other.primitiveTopologyType &&
                   rtvFormat == other.rtvFormat &&
                   dsvFormat == other.dsvFormat &&
                   renderTargetCount == other.renderTargetCount &&
                   depthBias == other.depthBias &&
                   slopeScaledDepthBias == other.slopeScaledDepthBias &&
                   wireFrame == other.wireFrame;
        }

        public override int GetHashCode()
        {
            HashCode hash = new HashCode();
            hash.Add(inputLayout);
            hash.Add(blendState);
            hash.Add(cullMode);
            hash.Add(primitiveTopologyType);
            hash.Add(rtvFormat);
            hash.Add(dsvFormat);
            hash.Add(renderTargetCount);
            hash.Add(depthBias);
            hash.Add(slopeScaledDepthBias);
            hash.Add(wireFrame);
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
    public class PSO : IDisposable
    {
        static readonly InputLayoutDescription inputLayoutMMD = new InputLayoutDescription(
            new InputElementDescription("POSITION", 0, Format.R32G32B32_Float, 0, 0),
            new InputElementDescription("NORMAL", 0, Format.R32G32B32_Float, 0, 1),
            new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 0, 2),
            new InputElementDescription("BONES", 0, Format.R16G16B16A16_UInt, 0, 3),
            new InputElementDescription("WEIGHTS", 0, Format.R32G32B32A32_Float, 0, 4),
            new InputElementDescription("TANGENT", 0, Format.R32G32B32_Float, 0, 5)
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
        static readonly InputLayoutDescription inputLayoutImGui = new InputLayoutDescription(
            new InputElementDescription("POSITION", 0, Format.R32G32_Float, 0),
            new InputElementDescription("TEXCOORD", 0, Format.R32G32_Float, 0),
            new InputElementDescription("COLOR", 0, Format.R8G8B8A8_UNorm, 0)
            );
        static readonly BlendDescription blendStateAdd = new BlendDescription(Blend.One, Blend.One);
        static readonly BlendDescription blendStateAlpha = new BlendDescription(Blend.SourceAlpha, Blend.InverseSourceAlpha, Blend.One, Blend.InverseSourceAlpha);

        BlendDescription BlendDescSelect(BlendState blendState)
        {
            if (blendState == BlendState.none)
                return new BlendDescription(Blend.One, Blend.Zero);
            else if (blendState == BlendState.alpha)
                return blendStateAlpha;
            else if (blendState == BlendState.add)
                return blendStateAdd;
            return new BlendDescription();
        }

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
            this.geometryShader = geometryShader;
            this.pixelShader = pixelShader;
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

        public void Dispose()
        {
            foreach (var combine in m_pipelineStates)
            {
                combine.Release();
            }
            m_psoDescs.Clear();
            m_pipelineStates.Clear();
        }


        internal int GetVariantIndex(GraphicsDevice graphicsDevice, RootSignature graphicsSignature, PSODesc psoDesc)
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
                GraphicsPipelineStateDescription state = new GraphicsPipelineStateDescription();

                //state.InputLayout = inputLayout.inputElementDescriptions;

                if (psoDesc.inputLayout == InputLayout.mmd)
                    state.InputLayout = inputLayoutMMD;
                else if (psoDesc.inputLayout == InputLayout.postProcess)
                    state.InputLayout = inputLayoutPosOnly;
                else if (psoDesc.inputLayout == InputLayout.skinned)
                    state.InputLayout = inputLayoutSkinned;
                else if (psoDesc.inputLayout == InputLayout.imgui)
                    state.InputLayout = inputLayoutImGui;

                state.RootSignature = graphicsSignature.rootSignature;
                if (vertexShader != null)
                    state.VertexShader = vertexShader;
                if (geometryShader != null)
                    state.GeometryShader = geometryShader;
                if (pixelShader != null)
                    state.PixelShader = pixelShader;
                state.SampleMask = uint.MaxValue;
                state.PrimitiveTopologyType = psoDesc.primitiveTopologyType;
                state.BlendState = BlendDescSelect(psoDesc.blendState);
                state.SampleDescription = new SampleDescription(1, 0);

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
                if (psoDesc.dsvFormat != Format.Unknown)
                {
                    state.DepthStencilState = new DepthStencilDescription(true, true);
                    state.DepthStencilFormat = psoDesc.dsvFormat;
                    rasterizerDescription.DepthClipEnable = true;
                }
                else
                {
                    state.DepthStencilState = new DepthStencilDescription(false, false);
                }

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
    }
}
