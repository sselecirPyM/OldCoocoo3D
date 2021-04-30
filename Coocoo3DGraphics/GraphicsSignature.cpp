#include "pch.h"
#include "GraphicsSignature.h"
#include "DirectXHelper.h"

using namespace Coocoo3DGraphics;
#define SizeOfInUint32(obj) ((sizeof(obj) - 1) / sizeof(UINT32) + 1)
inline D3D12_STATIC_SAMPLER_DESC _StaticSamplerDesc()
{
	D3D12_STATIC_SAMPLER_DESC staticSamplerDesc = {};
	staticSamplerDesc.AddressU = D3D12_TEXTURE_ADDRESS_MODE_CLAMP;
	staticSamplerDesc.AddressV = D3D12_TEXTURE_ADDRESS_MODE_CLAMP;
	staticSamplerDesc.AddressW = D3D12_TEXTURE_ADDRESS_MODE_CLAMP;
	staticSamplerDesc.BorderColor = D3D12_STATIC_BORDER_COLOR_OPAQUE_BLACK;
	staticSamplerDesc.ComparisonFunc = D3D12_COMPARISON_FUNC_NEVER;
	staticSamplerDesc.Filter = D3D12_FILTER_MIN_MAG_MIP_LINEAR;
	staticSamplerDesc.MipLODBias = 0;
	staticSamplerDesc.MaxAnisotropy = 0;
	staticSamplerDesc.MinLOD = 0;
	staticSamplerDesc.MaxLOD = D3D12_FLOAT32_MAX;
	staticSamplerDesc.ShaderVisibility = D3D12_SHADER_VISIBILITY_ALL;
	staticSamplerDesc.RegisterSpace = 0;
	return staticSamplerDesc;
}
#define STATIC_SAMPLER_CODE_FRAG \
	D3D12_STATIC_SAMPLER_DESC staticSamplerDescs[4] = { _StaticSamplerDesc(),_StaticSamplerDesc(),_StaticSamplerDesc(),_StaticSamplerDesc() };\
	staticSamplerDescs[0].ShaderRegister = 0;\
	staticSamplerDescs[1].ShaderRegister = 1;\
	staticSamplerDescs[1].MaxAnisotropy = 16;\
	staticSamplerDescs[1].Filter = D3D12_FILTER_ANISOTROPIC;\
	staticSamplerDescs[2].ShaderRegister = 2;\
	staticSamplerDescs[2].ComparisonFunc = D3D12_COMPARISON_FUNC_LESS;\
	staticSamplerDescs[2].Filter = D3D12_FILTER_COMPARISON_MIN_MAG_MIP_LINEAR;\
	staticSamplerDescs[3].ShaderRegister = 3;\
	staticSamplerDescs[3].Filter = D3D12_FILTER_MIN_MAG_MIP_POINT


void GraphicsSignature::ReloadSkinning(DeviceResources^ deviceResources)
{
	D3D12_FEATURE_DATA_ROOT_SIGNATURE featherData;
	featherData.HighestVersion = D3D_ROOT_SIGNATURE_VERSION_1_1;
	if (FAILED(deviceResources->GetD3DDevice()->CheckFeatureSupport(D3D12_FEATURE_ROOT_SIGNATURE, &featherData, sizeof(featherData))))
	{
		featherData.HighestVersion = D3D_ROOT_SIGNATURE_VERSION_1_0;
	}

	CD3DX12_ROOT_PARAMETER1 parameter[1];
	parameter[0].InitAsConstantBufferView(0, 0, D3D12_ROOT_DESCRIPTOR_FLAG_DATA_STATIC_WHILE_SET_AT_EXECUTE);

	CD3DX12_VERSIONED_ROOT_SIGNATURE_DESC rootSignatureDesc;
	rootSignatureDesc.Init_1_1(_countof(parameter), parameter, 0, nullptr, D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT | D3D12_ROOT_SIGNATURE_FLAG_ALLOW_STREAM_OUTPUT | D3D12_ROOT_SIGNATURE_FLAG_DENY_PIXEL_SHADER_ROOT_ACCESS);

	Microsoft::WRL::ComPtr<ID3DBlob> signature;
	Microsoft::WRL::ComPtr<ID3DBlob> error;
	DX::ThrowIfFailed(D3DX12SerializeVersionedRootSignature(&rootSignatureDesc, featherData.HighestVersion, &signature, &error));
	DX::ThrowIfFailed(deviceResources->GetD3DDevice()->CreateRootSignature(0, signature->GetBufferPointer(), signature->GetBufferSize(), IID_PPV_ARGS(&m_rootSignature)));
}

void GraphicsSignature::Sign1(DeviceResources^ deviceResources, const Platform::Array<GraphicSignatureDesc>^ Descs, Microsoft::WRL::ComPtr<ID3D12RootSignature>& m_sign, D3D12_ROOT_SIGNATURE_FLAGS flags)
{
	m_cbv.clear();
	m_srv.clear();
	m_uav.clear();
	D3D12_FEATURE_DATA_ROOT_SIGNATURE featherData;
	//featherData.HighestVersion = D3D_ROOT_SIGNATURE_VERSION_1_1;
	//if (FAILED(deviceResources->GetD3DDevice()->CheckFeatureSupport(D3D12_FEATURE_ROOT_SIGNATURE, &featherData, sizeof(featherData))))
	//{
		featherData.HighestVersion = D3D_ROOT_SIGNATURE_VERSION_1_0;
	//}

	UINT descCount = Descs->Length;
	std::vector< CD3DX12_ROOT_PARAMETER1>m1;
	std::vector< CD3DX12_DESCRIPTOR_RANGE1>m2;
	m1.resize(descCount);
	m2.resize(descCount);
	CD3DX12_ROOT_PARAMETER1* parameters = m1.data();
	CD3DX12_DESCRIPTOR_RANGE1* ranges = m2.data();

	int cbvCount = 0;
	int srvCount = 0;
	int uavCount = 0;

	for (int i = 0; i < descCount; i++)
	{
		if (Descs[i] == GraphicSignatureDesc::CBV)
		{
			parameters[i].InitAsConstantBufferView(cbvCount);
			m_cbv[cbvCount] = i;
			cbvCount++;
		}
		else if (Descs[i] == GraphicSignatureDesc::SRV)
		{
			parameters[i].InitAsShaderResourceView(srvCount);
			m_srv[srvCount] = i;
			srvCount++;
		}
		else if (Descs[i] == GraphicSignatureDesc::UAV)
		{
			parameters[i].InitAsUnorderedAccessView(uavCount);
			m_uav[cbvCount] = i;
			uavCount++;
		}
		else if (Descs[i] == GraphicSignatureDesc::CBVTable)
		{
			ranges[i].Init(D3D12_DESCRIPTOR_RANGE_TYPE_CBV, 1, cbvCount);
			parameters[i].InitAsDescriptorTable(1, &ranges[i]);
			m_cbv[cbvCount] = i;
			cbvCount++;
		}
		else if (Descs[i] == GraphicSignatureDesc::SRVTable)
		{
			ranges[i].Init(D3D12_DESCRIPTOR_RANGE_TYPE_SRV, 1, srvCount, 0, D3D12_DESCRIPTOR_RANGE_FLAG_DATA_VOLATILE);
			parameters[i].InitAsDescriptorTable(1, &ranges[i]);
			m_srv[srvCount] = i;
			srvCount++;
		}
		else if (Descs[i] == GraphicSignatureDesc::UAVTable)
		{
			ranges[i].Init(D3D12_DESCRIPTOR_RANGE_TYPE_UAV, 1, uavCount);
			parameters[i].InitAsDescriptorTable(1, &ranges[i]);
			m_uav[cbvCount] = i;
			uavCount++;
		}
	}

	STATIC_SAMPLER_CODE_FRAG;

	CD3DX12_VERSIONED_ROOT_SIGNATURE_DESC rootSignatureDesc;
	rootSignatureDesc.Init_1_1(descCount, parameters, _countof(staticSamplerDescs), staticSamplerDescs, flags);

	Microsoft::WRL::ComPtr<ID3DBlob> signature;
	Microsoft::WRL::ComPtr<ID3DBlob> error;
	DX::ThrowIfFailed(D3DX12SerializeVersionedRootSignature(&rootSignatureDesc, featherData.HighestVersion, &signature, &error));
	DX::ThrowIfFailed(deviceResources->GetD3DDevice()->CreateRootSignature(0, signature->GetBufferPointer(), signature->GetBufferSize(), IID_PPV_ARGS(&m_sign)));

}


//void GraphicsSignature::Sign1(DeviceResources^ deviceResources, const Platform::Array<GraphicSignatureDesc>^ Descs, Microsoft::WRL::ComPtr<ID3D12RootSignature>& m_sign, D3D12_ROOT_SIGNATURE_FLAGS flags)
//{
//	m_cbv.clear();
//	m_srv.clear();
//	m_uav.clear();
//	D3D12_FEATURE_DATA_ROOT_SIGNATURE featherData;
//	featherData.HighestVersion = D3D_ROOT_SIGNATURE_VERSION_1_1;
//	if (FAILED(deviceResources->GetD3DDevice()->CheckFeatureSupport(D3D12_FEATURE_ROOT_SIGNATURE, &featherData, sizeof(featherData))))
//	{
//		featherData.HighestVersion = D3D_ROOT_SIGNATURE_VERSION_1_0;
//	}
//
//	UINT descCount = Descs->Length;
//	std::vector< CD3DX12_ROOT_PARAMETER>m1;
//	std::vector< CD3DX12_DESCRIPTOR_RANGE>m2;
//	m1.resize(descCount);
//	m2.resize(descCount);
//	CD3DX12_ROOT_PARAMETER* parameters = m1.data();
//	CD3DX12_DESCRIPTOR_RANGE* ranges = m2.data();
//
//	int cbvCount = 0;
//	int srvCount = 0;
//	int uavCount = 0;
//
//	for (int i = 0; i < descCount; i++)
//	{
//		if (Descs[i] == GraphicSignatureDesc::CBV)
//		{
//			parameters[i].InitAsConstantBufferView(cbvCount);
//			m_cbv[cbvCount] = i;
//			cbvCount++;
//		}
//		else if (Descs[i] == GraphicSignatureDesc::SRV)
//		{
//			parameters[i].InitAsShaderResourceView(srvCount);
//			m_srv[srvCount] = i;
//			srvCount++;
//		}
//		else if (Descs[i] == GraphicSignatureDesc::UAV)
//		{
//			parameters[i].InitAsUnorderedAccessView(uavCount);
//			m_uav[cbvCount] = i;
//			uavCount++;
//		}
//		else if (Descs[i] == GraphicSignatureDesc::CBVTable)
//		{
//			ranges[i].Init(D3D12_DESCRIPTOR_RANGE_TYPE_CBV, 1, cbvCount);
//			parameters[i].InitAsDescriptorTable(1, &ranges[i]);
//			m_cbv[cbvCount] = i;
//			cbvCount++;
//		}
//		else if (Descs[i] == GraphicSignatureDesc::SRVTable)
//		{
//			ranges[i].Init(D3D12_DESCRIPTOR_RANGE_TYPE_SRV, 1, srvCount, 0, D3D12_DESCRIPTOR_RANGE_FLAG_DATA_VOLATILE);
//			parameters[i].InitAsDescriptorTable(1, &ranges[i]);
//			m_srv[srvCount] = i;
//			srvCount++;
//		}
//		else if (Descs[i] == GraphicSignatureDesc::UAVTable)
//		{
//			ranges[i].Init(D3D12_DESCRIPTOR_RANGE_TYPE_UAV, 1, uavCount);
//			parameters[i].InitAsDescriptorTable(1, &ranges[i]);
//			m_uav[cbvCount] = i;
//			uavCount++;
//		}
//	}
//
//	STATIC_SAMPLER_CODE_FRAG;
//
//	CD3DX12_ROOT_SIGNATURE_DESC rootSignatureDesc;
//	rootSignatureDesc.Init(descCount, parameters, _countof(staticSamplerDescs), staticSamplerDescs, flags);
//
//		Microsoft::WRL::ComPtr<ID3DBlob> signature;
//	Microsoft::WRL::ComPtr<ID3DBlob> error;
//	DX::ThrowIfFailed(D3D12SerializeRootSignature(&rootSignatureDesc, D3D_ROOT_SIGNATURE_VERSION_1_0, &signature, &error));
//	DX::ThrowIfFailed(deviceResources->GetD3DDevice()->CreateRootSignature(0, signature->GetBufferPointer(), signature->GetBufferSize(), IID_PPV_ARGS(&m_sign)));
//
//}

void GraphicsSignature::Reload(DeviceResources^ deviceResources, const Platform::Array<GraphicSignatureDesc>^ Descs)
{
	Sign1(deviceResources, Descs, m_rootSignature, D3D12_ROOT_SIGNATURE_FLAG_ALLOW_INPUT_ASSEMBLER_INPUT_LAYOUT | D3D12_ROOT_SIGNATURE_FLAG_ALLOW_STREAM_OUTPUT);
}

void GraphicsSignature::ReloadCompute(DeviceResources^ deviceResources, const Platform::Array<GraphicSignatureDesc>^ Descs)
{
	Sign1(deviceResources, Descs, m_rootSignature, D3D12_ROOT_SIGNATURE_FLAG_NONE);
}

void GraphicsSignature::RayTracingLocal(DeviceResources^ deviceResources)
{
	auto device = deviceResources->GetD3DDevice5();
	CD3DX12_ROOT_PARAMETER rootParameters[8];
	CD3DX12_DESCRIPTOR_RANGE range[5];
	for (int i = 0; i < 5; i++)
	{
		range[i].Init(D3D12_DESCRIPTOR_RANGE_TYPE_SRV, 1, i + 2, 1);
	}
	rootParameters[0].InitAsConstantBufferView(3);
	rootParameters[1].InitAsShaderResourceView(0, 1);
	rootParameters[2].InitAsShaderResourceView(1, 1);
	for (int i = 3; i < 8; i++)
	{
		rootParameters[i].InitAsDescriptorTable(1, &range[i - 3]);
	}


	CD3DX12_ROOT_SIGNATURE_DESC localRootSignatureDesc(ARRAYSIZE(rootParameters), rootParameters);
	localRootSignatureDesc.Flags = D3D12_ROOT_SIGNATURE_FLAG_LOCAL_ROOT_SIGNATURE;
	Microsoft::WRL::ComPtr<ID3DBlob> blob;
	Microsoft::WRL::ComPtr<ID3DBlob> error;

	DX::ThrowIfFailed(D3D12SerializeRootSignature(&localRootSignatureDesc, D3D_ROOT_SIGNATURE_VERSION_1, &blob, &error));
	DX::ThrowIfFailed(device->CreateRootSignature(1, blob->GetBufferPointer(), blob->GetBufferSize(), IID_PPV_ARGS(&m_rootSignature)));
}

void GraphicsSignature::Unload()
{
	m_rootSignature.Reset();
}
