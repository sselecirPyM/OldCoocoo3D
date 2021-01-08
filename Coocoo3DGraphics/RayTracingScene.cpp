#include "pch.h"
#include "RayTracingScene.h"
#include "DirectXHelper.h"
#include "RayTracing/DirectXRaytracingHelper.h"
using namespace Coocoo3DGraphics;
using namespace DX;

void RayTracingScene::ReloadLibrary(IBuffer^ rtShader)
{
	Microsoft::WRL::ComPtr<IBufferByteAccess> bufferByteAccess;
	reinterpret_cast<IInspectable*>(rtShader)->QueryInterface(IID_PPV_ARGS(&bufferByteAccess));
	byte* data = nullptr;
	DX::ThrowIfFailed(bufferByteAccess->Buffer(&data));
	m_byteCode = CD3DX12_SHADER_BYTECODE(data, rtShader->Length);
}

inline void RayTracingScene::SubobjectHitGroup(CD3DX12_HIT_GROUP_SUBOBJECT* hitGroupSubobject, LPCWSTR hitGroupName, LPCWSTR anyHitShaderName, LPCWSTR closestHitShaderName)
{
	hitGroupSubobject->SetAnyHitShaderImport(anyHitShaderName);
	hitGroupSubobject->SetClosestHitShaderImport(closestHitShaderName);
	hitGroupSubobject->SetHitGroupExport(hitGroupName);
	hitGroupSubobject->SetHitGroupType(D3D12_HIT_GROUP_TYPE_TRIANGLES);
}

void RayTracingScene::ReloadPipelineStates(DeviceResources^ deviceResources, const Platform::Array<Platform::String^>^ exportNames, const Platform::Array<HitGroupDesc^>^ hitGroups, RayTracingSceneSettings settings)
{
	CD3DX12_STATE_OBJECT_DESC raytracingStateObjectDesc;

	raytracingStateObjectDesc = { D3D12_STATE_OBJECT_TYPE_RAYTRACING_PIPELINE };

	auto lib = raytracingStateObjectDesc.CreateSubobject<CD3DX12_DXIL_LIBRARY_SUBOBJECT>();
	lib->SetDXILLibrary(&m_byteCode);
	for (int i = 0; i < exportNames->Length; i++)
	{
		lib->DefineExport(exportNames[i]->Begin());
	}

	m_rayTypeCount = settings.rayTypeCount;
	auto device = deviceResources->GetD3DDevice5();
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

		D3D12_STATIC_SAMPLER_DESC staticSamplerDescs[3] = { staticSamplerDesc,staticSamplerDesc,staticSamplerDesc };

		staticSamplerDescs[0].ShaderRegister = 0;
		staticSamplerDescs[1].ShaderRegister = 1;
		staticSamplerDescs[1].MaxAnisotropy = 16;
		staticSamplerDescs[1].Filter = D3D12_FILTER_ANISOTROPIC;


		staticSamplerDescs[2].ShaderRegister = 2;
		staticSamplerDescs[2].ComparisonFunc = D3D12_COMPARISON_FUNC_LESS;
		staticSamplerDescs[2].Filter = D3D12_FILTER_COMPARISON_MIN_MAG_MIP_LINEAR;

		CD3DX12_DESCRIPTOR_RANGE UAVDescriptor;
		UAVDescriptor.Init(D3D12_DESCRIPTOR_RANGE_TYPE_UAV, 1, 0);
		CD3DX12_DESCRIPTOR_RANGE SRVDescriptors[4];
		SRVDescriptors[0].Init(D3D12_DESCRIPTOR_RANGE_TYPE_SRV, 1, 1);
		SRVDescriptors[1].Init(D3D12_DESCRIPTOR_RANGE_TYPE_SRV, 1, 2);
		SRVDescriptors[2].Init(D3D12_DESCRIPTOR_RANGE_TYPE_SRV, 1, 3);
		SRVDescriptors[3].Init(D3D12_DESCRIPTOR_RANGE_TYPE_SRV, 1, 4);
		CD3DX12_ROOT_PARAMETER rootParameters[10];
		rootParameters[0].InitAsDescriptorTable(1, &UAVDescriptor);
		rootParameters[1].InitAsShaderResourceView(0);
		rootParameters[2].InitAsConstantBufferView(0);
		rootParameters[3].InitAsDescriptorTable(1, &SRVDescriptors[0]);
		rootParameters[4].InitAsDescriptorTable(1, &SRVDescriptors[1]);
		rootParameters[5].InitAsDescriptorTable(1, &SRVDescriptors[2]);
		rootParameters[6].InitAsDescriptorTable(1, &SRVDescriptors[3]);
		rootParameters[7].InitAsShaderResourceView(1, 2);
		rootParameters[8].InitAsUnorderedAccessView(0, 2);
		rootParameters[9].InitAsShaderResourceView(0, 2);
		CD3DX12_ROOT_SIGNATURE_DESC globalRootSignatureDesc(ARRAYSIZE(rootParameters), rootParameters, _countof(staticSamplerDescs), staticSamplerDescs);

		Microsoft::WRL::ComPtr<ID3DBlob> blob;
		Microsoft::WRL::ComPtr<ID3DBlob> error;

		DX::ThrowIfFailed(D3D12SerializeRootSignature(&globalRootSignatureDesc, D3D_ROOT_SIGNATURE_VERSION_1, &blob, &error));
		DX::ThrowIfFailed(device->CreateRootSignature(1, blob->GetBufferPointer(), blob->GetBufferSize(), IID_PPV_ARGS(&m_rootSignatures[0])));
	}

	{
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
		DX::ThrowIfFailed(device->CreateRootSignature(1, blob->GetBufferPointer(), blob->GetBufferSize(), IID_PPV_ARGS(&m_rootSignatures[1])));
	}


	for (int i = 0; i < hitGroups->Length; i++)
	{
		SubobjectHitGroup(raytracingStateObjectDesc.CreateSubobject<CD3DX12_HIT_GROUP_SUBOBJECT>(),
			hitGroups[i]->HitGroupName->Begin(),
			hitGroups[i]->AnyHitName->Begin() != hitGroups[i]->AnyHitName->End() ? hitGroups[i]->AnyHitName->Begin() : nullptr,
			hitGroups[i]->ClosestHitName->Begin() != hitGroups[i]->ClosestHitName->End() ? hitGroups[i]->ClosestHitName->Begin() : nullptr);
	}

	auto localRootSignature = raytracingStateObjectDesc.CreateSubobject<CD3DX12_LOCAL_ROOT_SIGNATURE_SUBOBJECT>();
	localRootSignature->SetRootSignature(m_rootSignatures[1].Get());
	// Shader association
	auto rootSignatureAssociation = raytracingStateObjectDesc.CreateSubobject<CD3DX12_SUBOBJECT_TO_EXPORTS_ASSOCIATION_SUBOBJECT>();
	rootSignatureAssociation->SetSubobjectToAssociate(*localRootSignature);
	for (int i = 0; i < hitGroups->Length; i++)
	{
		rootSignatureAssociation->AddExport(hitGroups[i]->HitGroupName->Begin());
	}

	raytracingStateObjectDesc.CreateSubobject<CD3DX12_GLOBAL_ROOT_SIGNATURE_SUBOBJECT>()->SetRootSignature(m_rootSignatures[0].Get());

	auto shaderConfig = raytracingStateObjectDesc.CreateSubobject<CD3DX12_RAYTRACING_SHADER_CONFIG_SUBOBJECT>();
	shaderConfig->Config(settings.payloadSize, settings.attributeSize);

	auto pipelineConfig = raytracingStateObjectDesc.CreateSubobject<CD3DX12_RAYTRACING_PIPELINE_CONFIG_SUBOBJECT>();
	pipelineConfig->Config(settings.maxRecursionDepth);


	DX::ThrowIfFailed(device->CreateStateObject(raytracingStateObjectDesc, IID_PPV_ARGS(&m_dxrStateObject)));
}

void RayTracingScene::ReloadAllocScratchAndInstance(DeviceResources^ deviceResources, UINT scratchSize, UINT maxIinstanceCount)
{
	asLastUpdateIndex = 0;
	auto device = deviceResources->GetD3DDevice();

	CD3DX12_HEAP_PROPERTIES defaultHeapProperties(D3D12_HEAP_TYPE_DEFAULT);
	for (int i = 0; i < c_frameCount; i++)
	{
		DX::ThrowIfFailed(device->CreateCommittedResource(
			&defaultHeapProperties,
			D3D12_HEAP_FLAG_NONE,
			&CD3DX12_RESOURCE_DESC::Buffer(scratchSize, D3D12_RESOURCE_FLAG_ALLOW_UNORDERED_ACCESS),
			D3D12_RESOURCE_STATE_UNORDERED_ACCESS,
			nullptr,
			IID_PPV_ARGS(&m_scratchResource[i])));
		NAME_D3D12_OBJECT(m_scratchResource[i]);

	}

	CD3DX12_HEAP_PROPERTIES uploadHeapProperties(D3D12_HEAP_TYPE_UPLOAD);
	for (int i = 0; i < c_frameCount; i++)
	{
		DX::ThrowIfFailed(device->CreateCommittedResource(
			&uploadHeapProperties,
			D3D12_HEAP_FLAG_NONE,
			&CD3DX12_RESOURCE_DESC::Buffer(sizeof(D3D12_RAYTRACING_INSTANCE_DESC) * maxIinstanceCount),
			D3D12_RESOURCE_STATE_GENERIC_READ,
			nullptr,
			IID_PPV_ARGS(&instanceDescs[i])));
		NAME_D3D12_OBJECT(instanceDescs[i]);
	}
	if (pArgumentCache)
		free(pArgumentCache);
	pArgumentCache = malloc(maxIinstanceCount * c_argumentCacheStride);
	ZeroMemory(pArgumentCache, maxIinstanceCount * c_argumentCacheStride);
}

void RayTracingScene::NextASIndex(int meshCount)
{
	asLastUpdateIndex = (asLastUpdateIndex + 1) % c_frameCount;
	m_bottomLevelASs[asLastUpdateIndex].clear();
	m_bottomLevelASs[asLastUpdateIndex].reserve(meshCount);
	m_instanceDescs.clear();
}

void RayTracingScene::NextSTIndex()
{
	stLastUpdateIndex = (stLastUpdateIndex + 1) % c_frameCount;
}

void RayTracingScene::BuildShaderTable(DeviceResources^ deviceResources, const Platform::Array<Platform::String^>^ raygenShaderNames, const Platform::Array<Platform::String^>^ missShaderNames, const Platform::Array <Platform::String^>^ hitGroupNames, int instances)
{
	auto device = deviceResources->GetD3DDevice5();

	Microsoft::WRL::ComPtr<ID3D12StateObjectProperties> stateObjectProperties;
	DX::ThrowIfFailed(m_dxrStateObject.As(&stateObjectProperties));

	UINT shaderIdentifierSize = D3D12_SHADER_IDENTIFIER_SIZE_IN_BYTES;
	UINT argumentSize = sizeof(CooRayTracingParamLocal1);

	// Ray gen shader table
	{
		UINT numShaderRecords = raygenShaderNames->Length;
		UINT shaderRecordSize = Align(shaderIdentifierSize + argumentSize, 64);
		ShaderTable rayGenShaderTable(device, numShaderRecords, shaderRecordSize, L"RayGenShaderTable");
		for (int i = 0; i < numShaderRecords; i++)
		{
			rayGenShaderTable.push_back(ShaderRecord(stateObjectProperties->GetShaderIdentifier(raygenShaderNames[i]->Begin()), shaderIdentifierSize));
		}
		m_rayGenShaderTable[stLastUpdateIndex] = rayGenShaderTable.GetResource();
		m_rayGenerateShaderTableStrideInBytes = rayGenShaderTable.GetShaderRecordSize();
	}

	// Miss shader table
	{
		UINT numShaderRecords = missShaderNames->Length;
		UINT shaderRecordSize = shaderIdentifierSize + argumentSize;
		ShaderTable missShaderTable(device, numShaderRecords, shaderRecordSize, L"MissShaderTable");
		for (int i = 0; i < numShaderRecords; i++)
		{
			missShaderTable.push_back(ShaderRecord(stateObjectProperties->GetShaderIdentifier(missShaderNames[i]->Begin()), shaderIdentifierSize));
		}
		m_missShaderTable[stLastUpdateIndex] = missShaderTable.GetResource();
		m_missShaderTableStrideInBytes = missShaderTable.GetShaderRecordSize();
	}

	// Hit group shader table
	{
		UINT numShaderRecords = instances * hitGroupNames->Length;
		UINT shaderRecordSize = shaderIdentifierSize + argumentSize;
		ShaderTable hitGroupShaderTable(device, numShaderRecords, shaderRecordSize, L"HitGroupShaderTable");
		for (int i = 0; i < instances; i++)
		{
			for (int j = 0; j < hitGroupNames->Length; j++)
			{
				hitGroupShaderTable.push_back(ShaderRecord(stateObjectProperties->GetShaderIdentifier(hitGroupNames[j]->Begin()), shaderIdentifierSize, (byte*)pArgumentCache + (i * c_argumentCacheStride), argumentSize));
			}
		}
		m_hitGroupShaderTable[stLastUpdateIndex] = hitGroupShaderTable.GetResource();
		m_hitGroupShaderTableStrideInBytes = hitGroupShaderTable.GetShaderRecordSize();
	}
}

RayTracingScene::~RayTracingScene()
{
	if (pArgumentCache)
		free(pArgumentCache);
	pArgumentCache = nullptr;
}
