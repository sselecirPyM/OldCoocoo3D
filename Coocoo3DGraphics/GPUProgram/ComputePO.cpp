#include "pch.h"
#include "DirectXHelper.h"
#include "ComputePO.h"
#include "TextUtil.h"
using namespace Coocoo3DGraphics;

bool ComputePO::CompileInitialize1(IBuffer^ file1, Platform::String^ entryPoint, const Platform::Array<MacroEntry^>^ macros)
{
	Microsoft::WRL::ComPtr<IBufferByteAccess> bufferByteAccess;
	reinterpret_cast<IInspectable*>(file1)->QueryInterface(IID_PPV_ARGS(&bufferByteAccess));
	byte* sourceCode = nullptr;
	if (FAILED(bufferByteAccess->Buffer(&sourceCode)))return false;
	std::vector<std::string>  macroStrings;
	std::vector<D3D_SHADER_MACRO> macros1;
	macros1.reserve(macros->Length + 1);
	macroStrings.reserve(macros->Length * 2);
	for (int i = 0; i < macros->Length; i++)
	{
		D3D_SHADER_MACRO macro_ = {};
		macroStrings.push_back(UnicodeToUTF8(macros[i]->Name->Begin()));
		macro_.Name = macroStrings[macroStrings.size() - 1].c_str();
		macroStrings.push_back(UnicodeToUTF8(macros[i]->Value->Begin()));
		macro_.Definition = macroStrings[macroStrings.size() - 1].c_str();
		macros1.push_back(macro_);
	}
	macros1.push_back(D3D_SHADER_MACRO());


	int bomOffset = CooBomTest(sourceCode);
	std::string entryPoint1 = UnicodeToUTF8(std::wstring(entryPoint->Begin()));
	HRESULT hr = D3DCompile(
		sourceCode + bomOffset,
		file1->Length - bomOffset,
		nullptr,
		macros1.data(),
		D3D_COMPILE_STANDARD_FILE_INCLUDE,
		entryPoint1.c_str(),
		"cs_5_0",
		0,
		0,
		&m_byteCode,
		nullptr
	);
	Status = GraphicsObjectStatus::loaded;
	if (FAILED(hr))
		return false;
	else
		return true;
}

void ComputePO::Initialize(DeviceResources^ deviceResources, GraphicsSignature^ rootSignature, IBuffer^ data)
{
	Microsoft::WRL::ComPtr<IBufferByteAccess> bufferByteAccess;
	reinterpret_cast<IInspectable*>(data)->QueryInterface(IID_PPV_ARGS(&bufferByteAccess));
	byte* pData = nullptr;
	DX::ThrowIfFailed(bufferByteAccess->Buffer(&pData));

	D3DCreateBlob(data->Length, &m_byteCode);
	memcpy(m_byteCode->GetBufferPointer(), pData, data->Length);
	D3D12_COMPUTE_PIPELINE_STATE_DESC desc = {};
	desc.CS.pShaderBytecode = m_byteCode->GetBufferPointer();
	desc.CS.BytecodeLength = m_byteCode->GetBufferSize();
	desc.pRootSignature = rootSignature->m_rootSignature.Get();
	DX::ThrowIfFailed(deviceResources->GetD3DDevice()->CreateComputePipelineState(&desc, IID_PPV_ARGS(&m_pipelineState)));
}

bool ComputePO::Verify(DeviceResources^ deviceResources, GraphicsSignature^ rootSignature)
{
	if (!m_byteCode) { Status = GraphicsObjectStatus::error; return false; }
	if (!m_byteCode->GetBufferSize()) { Status = GraphicsObjectStatus::error; return false; }
	if (m_pipelineState)return true;
	Microsoft::WRL::ComPtr<ID3DBlob> blob = m_byteCode;
	D3D12_COMPUTE_PIPELINE_STATE_DESC desc = {};
	desc.CS.pShaderBytecode = blob->GetBufferPointer();
	desc.CS.BytecodeLength = blob->GetBufferSize();
	desc.pRootSignature = rootSignature->m_rootSignature.Get();
	if (FAILED(deviceResources->GetD3DDevice()->CreateComputePipelineState(&desc, IID_PPV_ARGS(&m_pipelineState)))) { Status = GraphicsObjectStatus::error; return false; }
	Status = GraphicsObjectStatus::loaded;
	return true;
}

void ComputePO::Initialize(IBuffer^ data)
{
	Microsoft::WRL::ComPtr<IBufferByteAccess> bufferByteAccess;
	reinterpret_cast<IInspectable*>(data)->QueryInterface(IID_PPV_ARGS(&bufferByteAccess));
	byte* pData = nullptr;
	DX::ThrowIfFailed(bufferByteAccess->Buffer(&pData));

	D3DCreateBlob(data->Length, &m_byteCode);
	memcpy(m_byteCode->GetBufferPointer(), pData, data->Length);
	Status = GraphicsObjectStatus::loaded;
}
