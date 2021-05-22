#include "pch.h"
#include "PSO.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;
static const D3D12_INPUT_ELEMENT_DESC inputLayoutMMD[] =
{
	{ "POSITION", 0, DXGI_FORMAT_R32G32B32_FLOAT, 1, 0, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA, 0 },
	{ "NORMAL", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 0, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA, 0 },
	{ "TEXCOORD", 0, DXGI_FORMAT_R32G32_FLOAT, 0, 12, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA, 0 },
	{ "EDGESCALE", 0, DXGI_FORMAT_R32_FLOAT, 0, 20, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA, 0 },
	{ "BONES", 0, DXGI_FORMAT_R16G16B16A16_UINT, 0, 24, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA, 0 },
	{ "WEIGHTS", 0, DXGI_FORMAT_R32G32B32A32_FLOAT, 0, 32, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA, 0 },
	{ "TANGENT", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 48, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA, 0 },
};
static const D3D12_INPUT_ELEMENT_DESC inputLayoutSkinned[] =
{
	{ "POSITION", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 0, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA, 0 },
	{ "NORMAL", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 12, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA, 0 },
	{ "TEXCOORD", 0, DXGI_FORMAT_R32G32_FLOAT, 0, 24, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA, 0 },
	{ "TANGENT", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 32, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA, 0 },
	{ "EDGESCALE", 0, DXGI_FORMAT_R32_FLOAT, 0, 44, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA, 0 },
};
static const D3D12_INPUT_ELEMENT_DESC inputLayoutPosOnly[] =
{
	{ "POSITION", 0, DXGI_FORMAT_R32G32B32_FLOAT, 0, 0, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA, 0 },
};
inline D3D12_BLEND_DESC BlendDescAlpha()
{
	D3D12_BLEND_DESC blendDescAlpha = {};
	blendDescAlpha.RenderTarget[0].BlendEnable = true;
	blendDescAlpha.RenderTarget[0].SrcBlend = D3D12_BLEND_SRC_ALPHA;
	blendDescAlpha.RenderTarget[0].DestBlend = D3D12_BLEND_INV_SRC_ALPHA;
	blendDescAlpha.RenderTarget[0].BlendOp = D3D12_BLEND_OP_ADD;
	blendDescAlpha.RenderTarget[0].SrcBlendAlpha = D3D12_BLEND_ONE;
	blendDescAlpha.RenderTarget[0].DestBlendAlpha = D3D12_BLEND_INV_SRC_ALPHA;
	blendDescAlpha.RenderTarget[0].BlendOpAlpha = D3D12_BLEND_OP_ADD;
	blendDescAlpha.RenderTarget[0].RenderTargetWriteMask = D3D12_COLOR_WRITE_ENABLE_ALL;
	return blendDescAlpha;
}
inline D3D12_BLEND_DESC BlendDescAdd()
{
	D3D12_BLEND_DESC blendDescAlpha = {};
	blendDescAlpha.RenderTarget[0].BlendEnable = true;
	blendDescAlpha.RenderTarget[0].SrcBlend = D3D12_BLEND_ONE;
	blendDescAlpha.RenderTarget[0].DestBlend = D3D12_BLEND_ONE;
	blendDescAlpha.RenderTarget[0].BlendOp = D3D12_BLEND_OP_ADD;
	blendDescAlpha.RenderTarget[0].SrcBlendAlpha = D3D12_BLEND_ONE;
	blendDescAlpha.RenderTarget[0].DestBlendAlpha = D3D12_BLEND_INV_SRC_ALPHA;
	blendDescAlpha.RenderTarget[0].BlendOpAlpha = D3D12_BLEND_OP_ADD;
	blendDescAlpha.RenderTarget[0].RenderTargetWriteMask = D3D12_COLOR_WRITE_ENABLE_ALL;
	return blendDescAlpha;
}
inline D3D12_BLEND_DESC BlendDescSelect(EBlendState blendState)
{
	if (blendState == EBlendState::none)
		return CD3DX12_BLEND_DESC(D3D12_DEFAULT);
	else if (blendState == EBlendState::alpha)
		return BlendDescAlpha();
	else if (blendState == EBlendState::add)
		return BlendDescAdd();
	return D3D12_BLEND_DESC{};
}
inline const D3D12_INPUT_LAYOUT_DESC& inputLayoutSelect()
{
	return D3D12_INPUT_LAYOUT_DESC{ inputLayoutSkinned,_countof(inputLayoutSkinned) };
}

void PSO::Initialize(VertexShader^ vs, GeometryShader^ gs, PixelShader^ ps)
{
	ClearState();
	m_vertexShader = vs;
	m_geometryShader = gs;
	m_pixelShader = ps;
	Status = GraphicsObjectStatus::loaded;
}

void PSO::Unload()
{
	ClearState();
	Status = GraphicsObjectStatus::unload;
}

int PSO::GetVariantIndex(DeviceResources^ deviceResources, GraphicsSignature^ graphicsSignature, PSODesc psoDesc)
{
	_PSODesc1 _psoDesc1;
	_psoDesc1.desc = psoDesc;
	_psoDesc1.rootSignature = graphicsSignature->m_rootSignature.Get();
	int index = -1;
	for (int i = 0; i < m_psoDescs.size(); i++)
	{
		if (memcmp(&m_psoDescs[i], &_psoDesc1, sizeof(_psoDesc1)) == 0)
		{
			index = i;
		}
	}
	if (index == -1)
	{
		D3D12_SO_DECLARATION_ENTRY declarations[] =
		{
			{0,"POSITION",0,0,3,0},
			{0,"NORMAL",0,0,3,0},
			{0,"TEXCOORD",0,0,2,0},
			{0,"TANGENT",0,0,3,0},
			{0,"EDGESCALE",0,0,1,0},
		};
		UINT bufferStrides[] = { 64 };

		D3D12_GRAPHICS_PIPELINE_STATE_DESC state = {};
		if (psoDesc.inputLayout == EInputLayout::mmd)
			state.InputLayout = { inputLayoutMMD, _countof(inputLayoutMMD) };
		else if (psoDesc.inputLayout == EInputLayout::postProcess)
			state.InputLayout = { inputLayoutPosOnly, _countof(inputLayoutPosOnly) };
		else if (psoDesc.inputLayout == EInputLayout::skinned)
			state.InputLayout = { inputLayoutSkinned, _countof(inputLayoutSkinned) };
		state.pRootSignature = graphicsSignature->m_rootSignature.Get();
		if (m_vertexShader != nullptr)
			state.VS = CD3DX12_SHADER_BYTECODE(m_vertexShader->byteCode.Get());
		if (m_geometryShader != nullptr)
			state.GS = CD3DX12_SHADER_BYTECODE(m_geometryShader->byteCode.Get());
		if (m_pixelShader != nullptr)
			state.PS = CD3DX12_SHADER_BYTECODE(m_pixelShader->byteCode.Get());
		if ((DXGI_FORMAT)psoDesc.dsvFormat != DXGI_FORMAT_UNKNOWN)
		{
			state.DepthStencilState = CD3DX12_DEPTH_STENCIL_DESC(D3D12_DEFAULT);
			state.DSVFormat = (DXGI_FORMAT)psoDesc.dsvFormat;
		}
		state.SampleMask = UINT_MAX;
		state.PrimitiveTopologyType = (D3D12_PRIMITIVE_TOPOLOGY_TYPE)psoDesc.ptt;
		if (psoDesc.streamOutput)
		{
			state.StreamOutput.pSODeclaration = declarations;
			state.StreamOutput.NumEntries = _countof(declarations);
			state.StreamOutput.pBufferStrides = bufferStrides;
			state.StreamOutput.NumStrides = _countof(bufferStrides);
		}
		else
		{
			state.BlendState = BlendDescSelect(psoDesc.blendState);
			state.SampleDesc.Count = 1;
		}

		state.NumRenderTargets = psoDesc.renderTargetCount;
		for (int i = 0; i < psoDesc.renderTargetCount; i++)
		{
			state.RTVFormats[i] = (DXGI_FORMAT)psoDesc.rtvFormat;
		}
		D3D12_CULL_MODE cullMode = (D3D12_CULL_MODE)((int)psoDesc.cullMode);
		if (cullMode == 0)cullMode = D3D12_CULL_MODE_NONE;
		state.RasterizerState = CD3DX12_RASTERIZER_DESC(psoDesc.wireFrame ? D3D12_FILL_MODE_WIREFRAME : D3D12_FILL_MODE_SOLID, cullMode, false, psoDesc.depthBias, 0.0f, psoDesc.slopeScaledDepthBias, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
		Microsoft::WRL::ComPtr<ID3D12PipelineState> pipelineState;
		if (FAILED(deviceResources->GetD3DDevice()->CreateGraphicsPipelineState(&state, IID_PPV_ARGS(&pipelineState))))
		{
			Status = GraphicsObjectStatus::error;
			return -1;
		}
		m_psoDescs.push_back(_psoDesc1);
		m_pipelineStates.push_back(pipelineState);
		return (int)m_psoDescs.size() - 1;
	}
	return index;
}

void PSO::DelayDestroy(DeviceResources^ deviceResources)
{
	for (int i = 0; i < m_pipelineStates.size(); i++)
	{
		deviceResources->ResourceDelayRecycle(m_pipelineStates[i]);
	}
}

