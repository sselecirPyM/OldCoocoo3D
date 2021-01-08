#include "pch.h"
#include "DirectXHelper.h"
#include "ComputePO.h"
#include "TextUtil.h"
using namespace Coocoo3DGraphics;

bool ComputePO::CompileReload1(IBuffer^ file1, Platform::String^ entryPoint, ShaderMacro macro)
{
	Microsoft::WRL::ComPtr<IBufferByteAccess> bufferByteAccess;
	reinterpret_cast<IInspectable*>(file1)->QueryInterface(IID_PPV_ARGS(&bufferByteAccess));
	byte* sourceCode = nullptr;
	if (FAILED(bufferByteAccess->Buffer(&sourceCode)))return false;

	const D3D_SHADER_MACRO* macros = nullptr;
	if (macro == ShaderMacro::DEFINE_COO_SURFACE)macros = MACROS_DEFINE_COO_SURFACE;
	else if (macro == ShaderMacro::DEFINE_COO_PARTICLE)macros = MACROS_DEFINE_COO_PARTICLE;


	int bomOffset = CooBomTest(sourceCode);
	char* entryPointStr = CooGetMStr(entryPoint->Begin());
	HRESULT hr = D3DCompile(
		sourceCode + bomOffset,
		file1->Length - bomOffset,
		nullptr,
		macros,
		D3D_COMPILE_STANDARD_FILE_INCLUDE,
		entryPointStr,
		"cs_5_0",
		0,
		0,
		&byteCode,
		nullptr
	);
	CooFreeMStr(entryPointStr);
	if (FAILED(hr))
		return false;
	else
		return true;
}

void ComputePO::Reload(DeviceResources^ deviceResources, GraphicsSignature^ rootSignature, IBuffer^ data)
{
	Microsoft::WRL::ComPtr<IBufferByteAccess> bufferByteAccess;
	reinterpret_cast<IInspectable*>(data)->QueryInterface(IID_PPV_ARGS(&bufferByteAccess));
	byte* pData = nullptr;
	DX::ThrowIfFailed(bufferByteAccess->Buffer(&pData));

	D3DCreateBlob(data->Length, &byteCode);
	memcpy(byteCode->GetBufferPointer(), pData, data->Length);
	D3D12_COMPUTE_PIPELINE_STATE_DESC desc = {};
	desc.CS.pShaderBytecode = byteCode->GetBufferPointer();
	desc.CS.BytecodeLength = byteCode->GetBufferSize();
	desc.pRootSignature = rootSignature->m_rootSignature.Get();
	DX::ThrowIfFailed(deviceResources->GetD3DDevice()->CreateComputePipelineState(&desc, IID_PPV_ARGS(&m_pipelineState)));
}

bool ComputePO::Upload(DeviceResources^ deviceResources, GraphicsSignature^ rootSignature)
{
	Microsoft::WRL::ComPtr<ID3DBlob> blob = byteCode;//·ÀÖ¹±»ÊÍ·Å
	D3D12_COMPUTE_PIPELINE_STATE_DESC desc = {};
	desc.CS.pShaderBytecode = blob->GetBufferPointer();
	desc.CS.BytecodeLength = blob->GetBufferSize();
	desc.pRootSignature = rootSignature->m_rootSignature.Get();
	if (FAILED(deviceResources->GetD3DDevice()->CreateComputePipelineState(&desc, IID_PPV_ARGS(&m_pipelineState)))) { Status = GraphicsObjectStatus::error; return false; }
	Status = GraphicsObjectStatus::loaded;
	return true;
}

void ComputePO::Reload(IBuffer^ data)
{
	Microsoft::WRL::ComPtr<IBufferByteAccess> bufferByteAccess;
	reinterpret_cast<IInspectable*>(data)->QueryInterface(IID_PPV_ARGS(&bufferByteAccess));
	byte* pData = nullptr;
	DX::ThrowIfFailed(bufferByteAccess->Buffer(&pData));

	D3DCreateBlob(data->Length, &byteCode);
	memcpy(byteCode->GetBufferPointer(), pData, data->Length);
}
