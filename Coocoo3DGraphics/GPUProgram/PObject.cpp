#include "pch.h"
#include "PObject.h"
#include "DirectXHelper.h"
using namespace Coocoo3DGraphics;
static const D3D12_INPUT_ELEMENT_DESC inputLayoutMMD[] =
{
	{ "POSITION", 0, DXGI_FORMAT_R32G32B32_FLOAT, 1, 0, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA, 0 },
	{ "POSITION", 1, DXGI_FORMAT_R32G32B32_FLOAT, 2, 0, D3D12_INPUT_CLASSIFICATION_PER_VERTEX_DATA, 0 },
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

void PObject::Initialize(DeviceResources^ deviceResources, GraphicsSignature^ graphicsSignature, EInputLayout type, EBlendState blendState, VertexShader^ vertexShader, GeometryShader^ geometryShader, PixelShader^ pixelShader, DxgiFormat rtvFormat, DxgiFormat depthFormat)
{
	Initialize(deviceResources, graphicsSignature, type, blendState, vertexShader, geometryShader, pixelShader, rtvFormat, depthFormat, ED3D12PrimitiveTopologyType::TRIANGLE);
}

void PObject::Initialize(DeviceResources^ deviceResources, GraphicsSignature^ graphicsSignature, EInputLayout type, EBlendState blendState, VertexShader^ vertexShader, GeometryShader^ geometryShader, PixelShader^ pixelShader, DxgiFormat rtvFormat, DxgiFormat depthFormat, ED3D12PrimitiveTopologyType primitiveTopologyType)
{
	Unload();
	m_vertexShader = vertexShader;
	m_geometryShader = geometryShader;
	m_pixelShader = pixelShader;
	auto d3dDevice = deviceResources->GetD3DDevice();
	m_primitiveTopologyType = (D3D12_PRIMITIVE_TOPOLOGY_TYPE)primitiveTopologyType;

	D3D12_GRAPHICS_PIPELINE_STATE_DESC state = {};
	if (type == EInputLayout::mmd)
		state.InputLayout = { inputLayoutMMD, _countof(inputLayoutMMD) };
	else if (type == EInputLayout::postProcess)
		state.InputLayout = { inputLayoutPosOnly, _countof(inputLayoutPosOnly) };
	else if (type == EInputLayout::skinned)
		state.InputLayout = { inputLayoutSkinned, _countof(inputLayoutSkinned) };
	state.pRootSignature = graphicsSignature->m_rootSignature.Get();
	state.VS = CD3DX12_SHADER_BYTECODE(vertexShader->byteCode.Get());
	if (m_geometryShader != nullptr)
		state.GS = CD3DX12_SHADER_BYTECODE(m_geometryShader->byteCode.Get());
	state.PS = CD3DX12_SHADER_BYTECODE(pixelShader->byteCode.Get());
	state.BlendState = BlendDescSelect(blendState);
	if ((DXGI_FORMAT)depthFormat != DXGI_FORMAT_UNKNOWN)
	{
		state.DepthStencilState = CD3DX12_DEPTH_STENCIL_DESC(D3D12_DEFAULT);
		state.DSVFormat = (DXGI_FORMAT)depthFormat;
	}
	state.SampleMask = UINT_MAX;
	state.PrimitiveTopologyType = m_primitiveTopologyType;
	state.NumRenderTargets = 1;
	state.RTVFormats[0] = (DXGI_FORMAT)rtvFormat;
	state.SampleDesc.Count = 1;

	state.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_FILL_MODE_SOLID, D3D12_CULL_MODE_NONE, false, 0, 0.0f, 0.0f, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
	DX::ThrowIfFailed(d3dDevice->CreateGraphicsPipelineState(&state, IID_PPV_ARGS(&m_pipelineState[0])));
	state.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_FILL_MODE_SOLID, D3D12_CULL_MODE_FRONT, false, 0, 0.0f, 0.0f, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
	DX::ThrowIfFailed(d3dDevice->CreateGraphicsPipelineState(&state, IID_PPV_ARGS(&m_pipelineState[1])));
	state.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_FILL_MODE_SOLID, D3D12_CULL_MODE_BACK, false, 0, 0.0f, 0.0f, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
	DX::ThrowIfFailed(d3dDevice->CreateGraphicsPipelineState(&state, IID_PPV_ARGS(&m_pipelineState[2])));

	state.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_FILL_MODE_WIREFRAME, D3D12_CULL_MODE_NONE, false, 0, 0.0f, 0.0f, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
	DX::ThrowIfFailed(d3dDevice->CreateGraphicsPipelineState(&state, IID_PPV_ARGS(&m_pipelineState[3])));
	state.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_FILL_MODE_WIREFRAME, D3D12_CULL_MODE_FRONT, false, 0, 0.0f, 0.0f, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
	DX::ThrowIfFailed(d3dDevice->CreateGraphicsPipelineState(&state, IID_PPV_ARGS(&m_pipelineState[4])));
	state.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_FILL_MODE_WIREFRAME, D3D12_CULL_MODE_BACK, false, 0, 0.0f, 0.0f, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
	DX::ThrowIfFailed(d3dDevice->CreateGraphicsPipelineState(&state, IID_PPV_ARGS(&m_pipelineState[5])));
}

void PObject::InitializeSkinning(VertexShader^ vs, GeometryShader^ gs)
{
	ClearState();
	m_vertexShader = vs;
	m_geometryShader = gs;
	m_pixelShader = nullptr;
	m_renderTargetFormat = DXGI_FORMAT_UNKNOWN;
	m_blendState = EBlendState::none;
	m_useStreamOutput = true;
	m_primitiveTopologyType = D3D12_PRIMITIVE_TOPOLOGY_TYPE_POINT;
}

void PObject::Initialize(VertexShader^ vs, GeometryShader^ gs, PixelShader^ ps)
{
	ClearState();
	m_vertexShader = vs;
	m_geometryShader = gs;
	m_pixelShader = ps;
	Status = GraphicsObjectStatus::loaded;
}

bool PObject::Upload(DeviceResources^ deviceResources, GraphicsSignature^ graphicsSignature)
{
	Unload();
#define _PipelineState(_INDEX) if (FAILED(deviceResources->GetD3DDevice()->CreateGraphicsPipelineState(&state, IID_PPV_ARGS(&m_pipelineState[_INDEX])))) { Status = GraphicsObjectStatus::error; return false; }
	if (m_useStreamOutput)
	{
		struct PipelineStateStream
		{
			CD3DX12_PIPELINE_STATE_STREAM_ROOT_SIGNATURE pROOTSIGNATURE;
			CD3DX12_PIPELINE_STATE_STREAM_INPUT_LAYOUT INPUTLAYOUT;
			CD3DX12_PIPELINE_STATE_STREAM_PRIMITIVE_TOPOLOGY PRIMITIVETOPOLOGY;
			CD3DX12_PIPELINE_STATE_STREAM_VS VS;
			CD3DX12_PIPELINE_STATE_STREAM_GS GS;
			CD3DX12_PIPELINE_STATE_STREAM_STREAM_OUTPUT STREAMOUT;
		} pipelineStateStream;
		pipelineStateStream.pROOTSIGNATURE = graphicsSignature->m_rootSignature.Get();
		pipelineStateStream.INPUTLAYOUT = { inputLayoutMMD, _countof(inputLayoutMMD) };
		if (m_vertexShader != nullptr)
			pipelineStateStream.VS = CD3DX12_SHADER_BYTECODE(m_vertexShader->byteCode.Get());
		if (m_geometryShader != nullptr)
			pipelineStateStream.GS = CD3DX12_SHADER_BYTECODE(m_geometryShader->byteCode.Get());
		D3D12_SO_DECLARATION_ENTRY declarations[] =
		{
			{0,"POSITION",0,0,3,0},
			{0,"NORMAL",0,0,3,0},
			{0,"TEXCOORD",0,0,2,0},
			{0,"TANGENT",0,0,3,0},
			{0,"EDGESCALE",0,0,1,0},
		};
		UINT bufferStrides[] = { 64 };
		pipelineStateStream.STREAMOUT = { declarations ,_countof(declarations),bufferStrides,_countof(bufferStrides),0 };
		pipelineStateStream.PRIMITIVETOPOLOGY = m_primitiveTopologyType;
		D3D12_PIPELINE_STATE_STREAM_DESC state2 = { sizeof(pipelineStateStream),&pipelineStateStream };
		if (FAILED(deviceResources->GetD3DDevice()->CreatePipelineState(&state2, IID_PPV_ARGS(&m_pipelineState[c_indexPipelineStateSkinning]))))
		{
			Status = GraphicsObjectStatus::error;
			return false;
		}

		Status = GraphicsObjectStatus::loaded;

		return true;
	}
	else
	{
		D3D12_GRAPHICS_PIPELINE_STATE_DESC state = {};
		state.InputLayout = { inputLayoutSkinned, _countof(inputLayoutSkinned) };
		state.pRootSignature = graphicsSignature->m_rootSignature.Get();
		if (m_vertexShader != nullptr)
			state.VS = CD3DX12_SHADER_BYTECODE(m_vertexShader->byteCode.Get());
		if (m_geometryShader != nullptr)
			state.GS = CD3DX12_SHADER_BYTECODE(m_geometryShader->byteCode.Get());
		if (m_pixelShader != nullptr)
			state.PS = CD3DX12_SHADER_BYTECODE(m_pixelShader->byteCode.Get());
		state.BlendState = BlendDescSelect(m_blendState);
		state.DepthStencilState = CD3DX12_DEPTH_STENCIL_DESC(D3D12_DEFAULT);
		state.DSVFormat = m_depthFormat;
		state.SampleMask = UINT_MAX;
		state.PrimitiveTopologyType = m_primitiveTopologyType;
		state.SampleDesc.Count = 1;

		if (!m_isDepthOnly)
		{
			state.NumRenderTargets = m_renderTargetCount;
			for (int i = 0; i < m_renderTargetCount; i++)
			{
				state.RTVFormats[i] = m_renderTargetFormat;
			}
			state.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_FILL_MODE_SOLID, D3D12_CULL_MODE_NONE, false, 0, 0.0f, 0.0f, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
			_PipelineState(0);
			state.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_FILL_MODE_SOLID, D3D12_CULL_MODE_FRONT, false, 0, 0.0f, 0.0f, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
			_PipelineState(1);
			state.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_FILL_MODE_SOLID, D3D12_CULL_MODE_BACK, false, 0, 0.0f, 0.0f, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
			_PipelineState(2);

			state.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_FILL_MODE_WIREFRAME, D3D12_CULL_MODE_NONE, false, 0, 0.0f, 0.0f, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
			_PipelineState(3);
			state.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_FILL_MODE_WIREFRAME, D3D12_CULL_MODE_FRONT, false, 0, 0.0f, 0.0f, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
			_PipelineState(4);
			state.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_FILL_MODE_WIREFRAME, D3D12_CULL_MODE_BACK, false, 0, 0.0f, 0.0f, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
			_PipelineState(5);
		}
		else
		{
			state.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_FILL_MODE_SOLID, D3D12_CULL_MODE_NONE, false, m_depthBias, 0.0f, 1.5f, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
			_PipelineState(0);
			state.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_FILL_MODE_SOLID, D3D12_CULL_MODE_FRONT, false, m_depthBias, 0.0f, 1.5f, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
			_PipelineState(1);
			state.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_FILL_MODE_SOLID, D3D12_CULL_MODE_BACK, false, m_depthBias, 0.0f, 1.5f, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
			_PipelineState(2);

			state.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_FILL_MODE_WIREFRAME, D3D12_CULL_MODE_NONE, false, m_depthBias, 0.0f, 1.5f, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
			_PipelineState(3);
			state.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_FILL_MODE_WIREFRAME, D3D12_CULL_MODE_FRONT, false, m_depthBias, 0.0f, 1.5f, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
			_PipelineState(4);
			state.RasterizerState = CD3DX12_RASTERIZER_DESC(D3D12_FILL_MODE_WIREFRAME, D3D12_CULL_MODE_BACK, false, m_depthBias, 0.0f, 1.5f, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
			_PipelineState(5);
		}

		Status = GraphicsObjectStatus::loaded;

		return true;
	}
}

void PObject::Unload()
{
	Status = GraphicsObjectStatus::unload;
	for (int i = 0; i < _countof(m_pipelineState); i++)
	{
		m_pipelineState[i].Reset();
	}
}

int PObject::GetVariantIndex(DeviceResources^ deviceResources, GraphicsSignature^ graphicsSignature, PSODesc psoDesc)
{
	int index = -1;
	for (int i = 0; i < m_psoDescs.size(); i++)
	{
		if (memcmp(&m_psoDescs[i], &psoDesc, sizeof(PSODesc)) == 0)
		{
			index = i;
		}
	}
	if (index == -1)
	{

		D3D12_GRAPHICS_PIPELINE_STATE_DESC state = {};
		state.InputLayout = { inputLayoutSkinned, _countof(inputLayoutSkinned) };
		state.pRootSignature = graphicsSignature->m_rootSignature.Get();
		if (m_vertexShader != nullptr)
			state.VS = CD3DX12_SHADER_BYTECODE(m_vertexShader->byteCode.Get());
		if (m_geometryShader != nullptr)
			state.GS = CD3DX12_SHADER_BYTECODE(m_geometryShader->byteCode.Get());
		if (m_pixelShader != nullptr)
			state.PS = CD3DX12_SHADER_BYTECODE(m_pixelShader->byteCode.Get());
		state.BlendState = BlendDescSelect(psoDesc.blendState);
		if ((DXGI_FORMAT)psoDesc.dsvFormat != DXGI_FORMAT_UNKNOWN)
		{
			state.DepthStencilState = CD3DX12_DEPTH_STENCIL_DESC(D3D12_DEFAULT);
			state.DSVFormat = (DXGI_FORMAT)psoDesc.dsvFormat;
		}
		state.SampleMask = UINT_MAX;
		state.PrimitiveTopologyType = (D3D12_PRIMITIVE_TOPOLOGY_TYPE)psoDesc.ptt;
		state.SampleDesc.Count = 1;

		state.NumRenderTargets = psoDesc.renderTargetCount;
		for (int i = 0; i < psoDesc.renderTargetCount; i++)
		{
			state.RTVFormats[i] = (DXGI_FORMAT)psoDesc.rtvFormat;
		}
		state.RasterizerState = CD3DX12_RASTERIZER_DESC(psoDesc.wireFrame ? D3D12_FILL_MODE_WIREFRAME : D3D12_FILL_MODE_SOLID, (D3D12_CULL_MODE)((int)psoDesc.cullMode + 1), false, psoDesc.depthBias, 0.0f, 1.5f, true, false, false, 0, D3D12_CONSERVATIVE_RASTERIZATION_MODE_OFF);
		Microsoft::WRL::ComPtr<ID3D12PipelineState> pipelineState;
		if (FAILED(deviceResources->GetD3DDevice()->CreateGraphicsPipelineState(&state, IID_PPV_ARGS(&pipelineState))))
		{
			return -1;
		}
		m_psoDescs.push_back(psoDesc);
		m_pipelineStates.push_back(pipelineState);
		return (int)m_psoDescs.size() - 1;
	}
	return index;
}

